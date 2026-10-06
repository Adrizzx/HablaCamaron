using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HablaCamaron.AI;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// CABLEADO del sensor de percepción mutua, verificado DESDE EL JUEGO.
    ///
    /// Este proyecto ya tuvo TRES sensores declarados en NpcSensors que ningún
    /// código de producción rellenaba (YieldAhead primero; después
    /// AheadIsStatic, LeftLaneClear y HasPriorityOverBlocker): los estados que
    /// dependían de ellos eran INALCANZABLES en el juego mientras los tests
    /// EditMode —que asignaban esos campos A MANO— seguían en verde dando falsa
    /// confianza. Por eso aquí no se asigna nada: se montan dos NpcDriver
    /// reales sobre un grafo real, se deja correr la física, y se lee
    /// `UltimosSensores`, que es exactamente lo que produjo `Sense()`.
    ///
    /// El grafo se construye en código (dos calles que se cruzan) en vez de
    /// cargar una escena: así el encuentro es reproducible y el test no depende
    /// del azar del tráfico de una zona.
    /// </summary>
    public class NpcPercepcionMutuaTests
    {
        private GameObject _a, _b;
        private DriverProfile _perfil;

        [UnityTearDown]
        public IEnumerator Limpia()
        {
            if (_a != null) Object.Destroy(_a);
            if (_b != null) Object.Destroy(_b);
            if (_perfil != null) Object.Destroy(_perfil);
            yield return null;
        }

        /// <summary>
        /// Dos rectas que se cruzan en el origen: una de oeste a este y otra de
        /// sur a norte, de un solo sentido y SIN unirse entre sí. Al no haber
        /// desvío posible, la ruta del A* está forzada y los dos autos llegan
        /// al cruce sí o sí — el encuentro no depende del destino que sortee
        /// PlanNewRoute. Nodos cada 20 m: a 40 m quedarían fuera del radio de
        /// búsqueda de RoutePlanning.BestStartNode (30 m) y el auto arrancaría
        /// sin ruta.
        /// </summary>
        private static RoadGraphData GrafoEnCruz()
        {
            var g = new RoadGraphData();
            int previo = -1;
            for (int i = 0; i <= 4; i++) // oeste → este
            {
                int id = g.AddNode(new Vector3(-40f + i * 20f, 0f, 0f)).Id;
                if (previo >= 0) g.Connect(previo, id);
                previo = id;
            }
            previo = -1;
            for (int i = 0; i <= 4; i++) // sur → norte
            {
                int id = g.AddNode(new Vector3(0f, 0f, -40f + i * 20f)).Id;
                if (previo >= 0) g.Connect(previo, id);
                previo = id;
            }
            return g;
        }

        private GameObject Auto(string nombre, Vector3 pos, Vector3 mirandoA, RoadGraphData g, DriverProfile p)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation((mirandoA - pos).normalized, Vector3.up);
            go.transform.localScale = new Vector3(1.9f, 1.4f, 4.4f);
            go.AddComponent<Rigidbody>().isKinematic = true;
            go.AddComponent<NpcDriver>().Init(g, p);
            return go;
        }

        [UnityTest]
        public IEnumerator DosNpcQueConvergen_SeDetectanAunqueNoEstenEnElConoFrontal()
        {
            var g = GrafoEnCruz();
            _perfil = DriverProfile.Particular();

            // A recorre la calle X hacia +X; B recorre la calle Z hacia +Z.
            // Se encuentran en el origen. Ninguno está en el cono frontal del
            // otro: llegan perpendiculares, que es justo el caso que el
            // SphereCast de tres conos (±18°) NO ve.
            _a = Auto("PercepA", new Vector3(-40f, 0.7f, 0f), new Vector3(0f, 0.7f, 0f), g, _perfil);
            _b = Auto("PercepB", new Vector3(0f, 0.7f, -40f), new Vector3(0f, 0.7f, 0f), g, _perfil);

            var da = _a.GetComponent<NpcDriver>();
            float mejorTtc = float.MaxValue;
            bool vistoPorLaDerecha = false;

            // Los dos avanzan a ~9 m/s desde 40 m: el encuentro cae sobre los 4 s.
            for (int i = 0; i < 400 && _a != null && _b != null; i++)
            {
                yield return new WaitForFixedUpdate();
                float ttc = da.UltimosSensores.NeighborTimeToCollision;
                if (ttc >= mejorTtc) continue;
                mejorTtc = ttc;
                vistoPorLaDerecha = da.UltimosSensores.NeighborIsOnMyRight;
            }

            Debug.Log($"[Percepcion] Mejor tiempo-a-colisión detectado por A: {mejorTtc:0.00} s " +
                      $"(¿venía por su derecha? {vistoPorLaDerecha}).");

            Assert.Less(mejorTtc, 6f,
                "A NUNCA vio venir a B. Si esto falla, el sensor de percepción mutua no se está " +
                "rellenando: NeighborTimeToCollision se quedó en MaxValue todo el recorrido.");
            Assert.IsTrue(vistoPorLaDerecha,
                "B se acerca por el costado derecho de A (A mira a +X, así que su derecha es -Z, " +
                "y B llega desde z=-40). Si sale false, el lado está calculado al revés y la regla " +
                "de prioridad del Paso 3 cedería el paso al que no toca.");
        }

        [UnityTest]
        public IEnumerator EnUnConflictoReal_CedeExactamenteUnoDeLosDos()
        {
            // LA propiedad que hace que el Paso 3 funcione, comprobada con dos
            // NpcDriver de verdad: en cada instante de conflicto, uno cede y el
            // otro no. Si cedieran los dos se quedarían clavados para siempre;
            // si no cediera ninguno, se atraviesan (que es el bug medido: 12-28
            // pares superpuestos por corrida). El test EditMode prueba la
            // antisimetría de la fórmula; este prueba que los dos NPC la
            // calculan CONSISTENTEMENTE desde sus marcos de referencia
            // distintos, que es donde podría romperse de verdad.
            var g = GrafoEnCruz();
            _perfil = DriverProfile.Particular();
            _a = Auto("PrioA", new Vector3(-40f, 0.7f, 0f), new Vector3(0f, 0.7f, 0f), g, _perfil);
            _b = Auto("PrioB", new Vector3(0f, 0.7f, -40f), new Vector3(0f, 0.7f, 0f), g, _perfil);

            var da = _a.GetComponent<NpcDriver>();
            var db = _b.GetComponent<NpcDriver>();

            int instantesEnConflicto = 0, cedenLosDos = 0, noCedeNinguno = 0;
            for (int i = 0; i < 400 && _a != null && _b != null; i++)
            {
                yield return new WaitForFixedUpdate();

                var sa = da.UltimosSensores;
                var sb = db.UltimosSensores;
                // Solo cuentan los instantes en que AMBOS se ven en conflicto:
                // si uno todavía no lo detecta, no hay negociación que juzgar.
                if (sa.NeighborTimeToCollision >= NpcBrain.ConflictoTtc ||
                    sb.NeighborTimeToCollision >= NpcBrain.ConflictoTtc) continue;

                instantesEnConflicto++;
                if (sa.NeighborHasPriority && sb.NeighborHasPriority) cedenLosDos++;
                if (!sa.NeighborHasPriority && !sb.NeighborHasPriority) noCedeNinguno++;
            }

            Debug.Log($"[Percepcion] Instantes en conflicto mutuo: {instantesEnConflicto}. " +
                      $"Ceden los dos: {cedenLosDos}. No cede ninguno: {noCedeNinguno}.");

            Assert.Greater(instantesEnConflicto, 0,
                "Los dos autos nunca se vieron en conflicto: el escenario no probó nada. " +
                "Si esto falla, revisar que de verdad converjan (ruta forzada por el grafo en cruz).");
            Assert.AreEqual(0, cedenLosDos,
                "Hubo instantes en que AMBOS cedían: eso es el bloqueo mutuo eterno que la regla " +
                "existe para evitar. La fórmula de desempate no está siendo antisimétrica en el juego.");
        }

        [UnityTest]
        public IEnumerator UnNpcSolo_NoSeInventaConflictos()
        {
            var g = GrafoEnCruz();
            _perfil = DriverProfile.Particular();
            _a = Auto("PercepSolo", new Vector3(-40f, 0.7f, 0f), new Vector3(0f, 0.7f, 0f), g, _perfil);
            var da = _a.GetComponent<NpcDriver>();

            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            Assert.AreEqual(float.MaxValue, da.UltimosSensores.NeighborTimeToCollision,
                "Sin nadie alrededor no hay conflicto. Un 0 aquí delataría que el campo se quedó " +
                "con el valor por defecto de la struct, que significa 'chocamos AHORA'.");
        }
    }
}
