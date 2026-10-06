using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HablaCamaron.AI;
using HablaCamaron.World;
using HablaCamaron.Missions;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// MEDICIÓN (Tarea 12, pedido del dueño): verificar que los NPC de tráfico
    /// se mantienen en su carril, sin invadir el contrario. Carga N1_CiudadToon
    /// real (a diferencia de la Zona Sur, tiene avenidas de DOBLE SENTIDO, no
    /// solo el anillo de un redondel), reparte tráfico real por TODO el grafo
    /// —no solo cerca del jugador, como haría el [TrafficManager] ambiental,
    /// que además dejaría sin cubrir medio mapa— y corre 30 s simulados
    /// muestreando cada 0.5 s el mismo LaneJudge que usa el juego para juzgar
    /// al jugador (RoadDiscipline lo consulta igual, ver LaneJudge.cs): así no
    /// se inventa un criterio paralelo.
    /// RUMBO = VELOCIDAD, NO EL MORRO: en cada paso de física se guarda el
    /// desplazamiento real (posición actual menos la del paso anterior) y el
    /// veredicto de cada muestra usa ESE vector, no transform.forward — con
    /// NpcTurnLimit limitando cuánto gira el auto por paso, en una curva
    /// cerrada la carrocería puede quedar unos grados "adelantada" respecto a
    /// hacia dónde se mueve de verdad en ese instante.
    /// EXCLUSIÓN DELIBERADA — ADELANTAMIENTO: mientras el NPC está en
    /// NpcState.Overtaking se corre a propósito ~3.2 m hacia el carril
    /// izquierdo para rodear a uno detenido (NpcDriver.OvertakeOffset, cambio
    /// reciente de otro desarrollador). Invadir el carril contrario ahí es EL
    /// DISEÑO, no el bug que mide este test: las muestras tomadas con
    /// CurrentState == Overtaking se descartan por completo (ni cuentan como
    /// contravía ni como a favor).
    /// Criterio del spec: 0 NPC con más del 10% de sus muestras (ya
    /// descontado el adelantamiento) en contravía — un roce puntual al cruzar
    /// una intersección es legítimo (LaneJudge ya lo permite: cruzar
    /// perpendicular da OnRoad, solo castiga la oposición franca y sostenida);
    /// circular de frente por el carril de enfrente no lo es.
    /// </summary>
    public class NpcCarrilDiagTests
    {
        private const float SegundosSim = 30f;
        private const float IntervaloMuestra = 0.5f;
        private const float UmbralContravia = 0.10f; // 10% de las muestras de UN NPC
        private const int NpcObjetivo = 40;           // repartidos por toda la ciudad

        private class Muestreo
        {
            public GameObject Go;
            public NpcDriver Npc;
            public string Nombre;      // copia aparte: Go puede ser destruido (reciclaje) antes de loguear
            public int NodoOrigenId;
            public Vector3 UltimaPos;
            public Vector3 UltimaVel;
            public int Muestras;
            public int Contravia;
            public Vector3 PeorPos;
        }

        [UnityTearDown]
        public IEnumerator Limpia()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Core.GameAudioSettings.GameplayMuted = false;
            var s = SceneManager.GetSceneByName("N1_CiudadToon");
            if (s.IsValid() && s.isLoaded)
            {
                var vacia = SceneManager.CreateScene("PostDiagCarril");
                SceneManager.SetActiveScene(vacia);
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // El timeout POR DEFECTO de [UnityTest] es 180000 ms (3 min); con
        // timeScale=4 los 30 s simulados corren en pocos segundos de reloj
        // real, pero se sube explícito por el mismo motivo que
        // SemaforosNpcDiagTests: margen ante una corrida lenta del runner.
        [Timeout(600000)]
        [UnityTest]
        public IEnumerator NingunNpcCirculaEnContraviaMasDelDiezPorCientoDelTiempo()
        {
            SceneManager.LoadScene("N1_CiudadToon");
            yield return null;
            yield return null; // Awake/Start de [RoadGraph] y demás controladores

            // N1_CiudadToon trae misión: MissionRunner.Start() congela el juego
            // (Time.timeScale = 0) para el briefing de Don Pancho hasta que
            // alguien pulsa "¡Vamos!" — nadie en batch. Se fuerza (mismo patrón
            // que NpcRedondelDiagTests/SemaforosNpcDiagTests) y se acelera ×4:
            // Time.fixedDeltaTime NO cambia con timeScale (solo la frecuencia
            // de FixedUpdate respecto al reloj de pared), así que la física y
            // el conteo de pasos siguen siendo exactos, solo más rápidos de medir.
            Time.timeScale = 4f;

            var graph = RoadGraph.Instance;
            Assert.IsNotNull(graph, "N1_CiudadToon debe traer un [RoadGraph].");
            var nodes = graph.Data.Nodes;
            Assert.Greater(nodes.Count, 1, "El grafo de N1_CiudadToon está vacío.");

            // Neutraliza el tráfico ambiental (lo auto-crea MissionSystemBootstrap):
            // se controla a mano quién y dónde nace, repartido por TODO el
            // grafo — su anillo de spawn (45-110 m del jugador) dejaría medio
            // mapa, y las avenidas de doble sentido del otro lado, sin cubrir.
            var ambiental = TrafficManager.Instance;
            if (ambiental != null) ambiental.MaxNpcs = 0;

            var perfiles = new[] { DriverProfile.Particular(), DriverProfile.Taxista(), DriverProfile.Buseta() };

            var muestreos = new List<Muestreo>();
            int stride = Mathf.Max(1, nodes.Count / NpcObjetivo);
            int idx = 0;
            for (int i = 0; i < nodes.Count; i += stride)
            {
                var nodo = nodes[i];
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"DiagCarril_{idx}_n{nodo.Id}";
                go.transform.position = nodo.Position + Vector3.up * 0.75f;
                go.transform.localScale = new Vector3(1.9f, 1.4f, 4.4f);
                go.AddComponent<Rigidbody>().isKinematic = true;
                var npc = go.AddComponent<NpcDriver>();
                npc.Init(graph.Data, perfiles[idx % perfiles.Length]);
                muestreos.Add(new Muestreo
                {
                    Go = go,
                    Npc = npc,
                    Nombre = go.name,
                    NodoOrigenId = nodo.Id,
                    UltimaPos = go.transform.position,
                });
                idx++;
            }
            Debug.Log($"[DiagCarril] NPC sembrados: {muestreos.Count} (de {nodes.Count} nodos del grafo, stride {stride}).");

            int pasosPorMuestra = Mathf.RoundToInt(IntervaloMuestra / Time.fixedDeltaTime);
            int muestrasTotales = Mathf.RoundToInt(SegundosSim / IntervaloMuestra);

            for (int m = 0; m < muestrasTotales; m++)
            {
                // Un paso de física a la vez: la velocidad de cada muestra es
                // el desplazamiento del ÚLTIMO paso (instantánea de verdad),
                // no un promedio de los 0.5 s completos.
                for (int paso = 0; paso < pasosPorMuestra; paso++)
                {
                    yield return new WaitForFixedUpdate();
                    foreach (var ms in muestreos)
                    {
                        if (ms.Go == null) continue; // reciclado por NpcUnstuckRoutine
                        Vector3 pos = ms.Go.transform.position;
                        ms.UltimaVel = (pos - ms.UltimaPos) / Time.fixedDeltaTime;
                        ms.UltimaPos = pos;
                    }
                }

                foreach (var ms in muestreos)
                {
                    if (ms.Go == null) continue;
                    if (ms.Npc != null && ms.Npc.CurrentState == NpcState.Overtaking) continue; // adelantamiento: no se mide

                    var veredicto = LaneJudge.Judge(graph.Data, ms.UltimaPos, ms.UltimaVel);
                    ms.Muestras++;
                    if (veredicto == LaneVerdict.WrongWay)
                    {
                        ms.Contravia++;
                        ms.PeorPos = ms.UltimaPos;
                    }
                }
            }

            int infractores = 0;
            float peorPorcentaje = 0f;
            foreach (var ms in muestreos)
            {
                if (ms.Muestras == 0) continue; // reciclado antes de la primera muestra, o siempre en adelantamiento
                float pct = (float)ms.Contravia / ms.Muestras;
                if (pct > UmbralContravia)
                {
                    infractores++;
                    peorPorcentaje = Mathf.Max(peorPorcentaje, pct);
                    Debug.LogWarning($"[DiagCarril] CONTRAVÍA: {ms.Nombre} (nodo origen #{ms.NodoOrigenId}) — " +
                                      $"{ms.Contravia}/{ms.Muestras} muestras ({pct:P0}) en contravía; última en {ms.PeorPos}.");
                }
            }

            Debug.Log($"[DiagCarril] NPC medidos: {muestreos.Count}. Infractores (>10% de sus muestras en " +
                      $"contravía, excluido el adelantamiento): {infractores}. Peor porcentaje: {peorPorcentaje:P0}.");

            Assert.AreEqual(0, infractores,
                $"{infractores} NPC(s) circulan en contravía más del {UmbralContravia:P0} de sus muestras — ver log.");
        }
    }
}
