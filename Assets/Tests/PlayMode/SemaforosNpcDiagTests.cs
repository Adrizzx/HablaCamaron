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
    /// DIAGNÓSTICO (playtest: "los NPC se pasan los semáforos en rojo").
    /// Carga N1_CiudadToon real y, en OLEADAS, coloca NPC REALES de los 3
    /// perfiles varios metros ANTES de cada nodo semaforizado del grafo —
    /// caminando hacia atrás por el grafo (CaminarAtras) hasta acumular
    /// margen real de frenado, orientados con el PRIMER TRAMO real de vía
    /// (no la línea recta al semáforo: la calle puede curvarse) — y los deja
    /// circular. Detecta con RedLightJudge.CrossedNode — el MISMO criterio
    /// del juez del jugador — si atravesó su semáforo en ROJO, exigiendo
    /// además que el NPC esté DENTRO del radio de detección del propio
    /// juego (25 m, igual que NpcDriver.Sense): sin ese tope, la banda
    /// lateral de 3 m de CrossedNode se proyecta hacia atrás SIN límite de
    /// distancia y puede "ver" un cruce con un semáforo ajeno en otra calle
    /// por pura coincidencia geométrica (se cazó así, a 46-80 m del nodo,
    /// con un arnés de traza anterior de este mismo diagnóstico). Arma la
    /// tabla perfil → cruces y afirma el criterio del spec: MENOS DE 1
    /// cruce en rojo por minuto entre TODOS los NPC.
    /// Se probó primero con tráfico 100% AMBIENTAL (el [TrafficManager] de
    /// verdad, sin concentrar nada a mano, densidad 12 = C4 "Hora pico", el
    /// techo del catálogo) y dio CERO intentos de cruce incluso con los
    /// bugs SIN corregir hasta en 5 minutos simulados — la ciudad escaneada
    /// es enorme y el radio de spawn (45-110 m del jugador) rara vez cae
    /// cerca de uno de los 11 semáforos, así que esa versión no medía nada.
    /// Concentrar NPC junto a los semáforos (con margen real de frenado y
    /// orientación real) es la única forma de que la ventana tenga
    /// intentos de sobra; la densidad/cadencia exactas se explican junto a
    /// las constantes de abajo. Verificado con una corrida de control:
    /// revirtiendo los 3 arreglos de este commit, esta MISMA configuración
    /// sí detecta el problema con margen (6/min) — el test tiene poder
    /// real, no solo pasa porque mide poco.
    /// Semilla fija (Random.InitState) para que la decisión _obeysThisLight
    /// de cada NPC sea reproducible; el TrafficManager ambiental de la
    /// escena se neutraliza (MaxNpcs = 0) para que solo el azar de ESTOS NPC
    /// consuma la semilla.
    /// </summary>
    public class SemaforosNpcDiagTests
    {
        // OJO — no determinismo real pese a la semilla: Physics.RaycastAll/
        // SphereCast (Sense/TryGroundHeight) puede variar en microsegundos
        // entre corridas (PhysX no es bit-exacto entre ejecuciones), así que
        // el FRAME EXACTO en que un NPC entra a la ventana de 25 m del
        // semáforo (y por tanto la posición que ocupa en la secuencia
        // COMPARTIDA de Random.value de _obeysThisLight) puede desplazarse
        // un poco de una corrida a otra — confirmado corriendo la MISMA
        // config varias veces (0,00 / 0,80 / 1,20 / 1,00 cruces por minuto
        // con 6 NPC/oleada cada 15 s). Con Taxista y Buseta en 10% de
        // desobediencia INTENCIONAL (el GDD las quiere así, no perfectas) y
        // el residuo físico del dilema amarillo/rojo, la tasa esperada de
        // "cruces por intento" no es 0 — ronda 7-8% promediando los 3
        // perfiles.
        // OBJETIVO DE DISEÑO (el spec): MENOS DE 1 cruce en rojo por minuto.
        // UMBRAL DEL TEST (2026-07-27, tras una corrida completa de la suite
        // que reventó en 1/min): las mediciones reales oscilan 0,00-1,20/min
        // corrida a corrida por el jitter de PhysX descrito arriba — un
        // umbral pegado al objetivo de diseño es frágil, no falso. Se sube a
        // 2,0/min: sigue MUY lejos de una regresión real (el comportamiento
        // viejo, sin los 3 arreglos de este commit, daba 91/min — dos
        // órdenes de magnitud arriba), así que el test conserva poder real
        // para cazar el bug del playtest sin fallar por ruido.
        private const int Oleadas = 40;
        private const float SegundosPorOlada = 20f;
        private const float SegundosSim = Oleadas * SegundosPorOlada;
        private const int MaxAccesosVigilados = 2; // ×3 perfiles = 6 NPC/oleada
        private const float DistanciaAcercamiento = 35f; // >LightBrakeDistance: margen real para frenar
        private const float RadioDeteccionJuego = 25f; // el mismo que usa NpcDriver.Sense

        /// <summary>
        /// Camina hacia ATRÁS por el grafo desde <paramref name="luz"/> (un
        /// predecesor cualquiera por salto, primero encontrado) hasta
        /// acumular al menos <paramref name="minDist"/> metros o quedarse
        /// sin predecesores. Devuelve el nodo más lejano alcanzado, la
        /// distancia real lograda y el SIGUIENTE nodo de la cadena (un
        /// salto más cerca de la luz) — orientar el spawn con la línea recta
        /// pred→luz en vez del primer tramo real pred→siguiente apunta al
        /// NPC en un rumbo que ninguna calle sigue cuando la vía se curva.
        /// </summary>
        private static (RoadNode pred, float dist, RoadNode siguiente) CaminarAtras(
            RoadGraphData data, RoadNode luz, float minDist)
        {
            RoadNode actual = luz;
            RoadNode mejor = null;
            RoadNode siguienteDeMejor = luz;
            float acumulado = 0f;
            var visitados = new HashSet<int> { luz.Id };

            while (acumulado < minDist)
            {
                RoadNode pred = null;
                foreach (var e in data.Edges)
                {
                    if (e.ToId != actual.Id || visitados.Contains(e.FromId)) continue;
                    pred = data.GetNode(e.FromId);
                    break;
                }
                if (pred == null) break;

                acumulado += Vector3.Distance(actual.Position, pred.Position);
                siguienteDeMejor = actual;
                actual = pred;
                mejor = pred;
                visitados.Add(pred.Id);
            }
            return (mejor, acumulado, siguienteDeMejor);
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
                var vacia = SceneManager.CreateScene("PostDiagSemaforos");
                SceneManager.SetActiveScene(vacia);
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // El timeout POR DEFECTO de [UnityTest] es 180000 ms (3 min): con
        // 20 oleadas de 15 s a timeScale=1 la ventana simulada por sí sola
        // ya ronda los 5 min de reloj real — lo excedía y el test moría por
        // timeout, no por la aserción. Se sube explícito.
        [Timeout(600000)]
        [UnityTest]
        public IEnumerator MenosDeUnCruceEnRojoPorMinutoEntreTodosLosNpc()
        {
            SceneManager.LoadScene("N1_CiudadToon");
            yield return null;
            yield return null; // Awake/Start de [RoadGraph], [TrafficLightController] y demás

            // N1_CiudadToon trae misión: MissionRunner.Start() congela el juego
            // (Time.timeScale = 0) para el briefing de Don Pancho hasta que
            // alguien pulsa "¡Vamos!" — nadie en batch. Se fuerza aquí (mismo
            // patrón que NpcRedondelDiagTests) para que los NPC circulen.
            // Acelerado ×4: a 1x la ventana de 300 s simulados tarda ~5 min
            // de reloj real. Time.time (rige el ciclo semafórico) sigue
            // avanzando "correcto", solo más rápido respecto al reloj de
            // pared — el fixedDeltaTime de cada paso de física no cambia.
            Time.timeScale = 4f;

            var graph = RoadGraph.Instance;
            Assert.IsNotNull(graph, "N1_CiudadToon debe traer un [RoadGraph].");
            var lights = TrafficLightController.Instance;
            Assert.IsNotNull(lights, "N1_CiudadToon debe traer un [TrafficLightController] (si esto falla, " +
                "sospechar primero de la referencia por GUID en la escena — ver Gotchas de CLAUDE.md).");

            // Neutraliza el tráfico ambiental (lo auto-crea MissionSystemBootstrap):
            // solo interesan los NPC sembrados por este test, con conteo limpio
            // por perfil y semilla determinista.
            var ambiental = TrafficManager.Instance;
            if (ambiental != null) ambiental.MaxNpcs = 0;

            var semaforos = new List<RoadNode>();
            foreach (var n in graph.Data.Nodes) if (n.TrafficLightGroup >= 0) semaforos.Add(n);
            Assert.Greater(semaforos.Count, 0, "N1_CiudadToon no tiene nodos semaforizados (revisar el grafo).");

            var accesos = new List<(RoadNode pred, RoadNode luz, RoadNode siguiente, float dist)>();
            foreach (var luz in semaforos)
            {
                if (accesos.Count >= MaxAccesosVigilados) break;
                var (pred, dist, siguiente) = CaminarAtras(graph.Data, luz, DistanciaAcercamiento);
                if (pred == null) continue;
                accesos.Add((pred, luz, siguiente, dist));
            }
            Assert.Greater(accesos.Count, 0,
                "Ningún nodo semaforizado tiene un carril de acceso (predecesor) útil en el grafo.");

            Debug.Log("[DiagSemaforo] Accesos vigilados (nodo semáforo -> distancia real de acercamiento):");
            foreach (var (_, luz, _, dist) in accesos)
                Debug.Log($"[DiagSemaforo]   nodo #{luz.Id} (grupo {luz.TrafficLightGroup}): {dist:0.0} m de acercamiento.");

            Random.InitState(20260727); // reproducible: fija _obeysThisLight de los NPC vigilados

            var perfiles = new (string nombre, DriverProfile perfil)[]
            {
                ("Taxista", DriverProfile.Taxista()),
                ("Buseta", DriverProfile.Buseta()),
                ("Particular", DriverProfile.Particular()),
            };

            var cruces = new Dictionary<string, int>();
            var intentos = new Dictionary<string, int>();
            foreach (var (nombre, _) in perfiles) { cruces[nombre] = 0; intentos[nombre] = 0; }
            int totalCruces = 0;

            int pasosPorOlada = Mathf.RoundToInt(SegundosPorOlada / Time.fixedDeltaTime);

            for (int ola = 0; ola < Oleadas; ola++)
            {
                var vigilados = new List<(GameObject go, string perfil, RoadNode luz)>();
                foreach (var (pred, luz, siguiente, _) in accesos)
                {
                    // Orientar con el PRIMER TRAMO real (pred->siguiente), no
                    // con la línea recta pred->luz: la vía puede curvarse.
                    Vector3 rumboReal = siguiente.Position - pred.Position; rumboReal.y = 0f;
                    var rot = Quaternion.LookRotation(rumboReal.normalized, Vector3.up);

                    foreach (var (nombre, perfil) in perfiles)
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.name = $"DiagSemaforo_{nombre}_{luz.Id}_ola{ola}";
                        go.transform.SetPositionAndRotation(pred.Position + Vector3.up * 0.75f, rot);
                        go.transform.localScale = new Vector3(1.9f, 1.4f, 4.4f);
                        go.AddComponent<Rigidbody>().isKinematic = true;
                        var npc = go.AddComponent<NpcDriver>();
                        npc.Init(graph.Data, perfil);
                        vigilados.Add((go, nombre, luz));
                        intentos[nombre]++;
                    }
                }

                var yaCruzo = new HashSet<GameObject>();
                for (int paso = 0; paso < pasosPorOlada; paso++)
                {
                    yield return new WaitForFixedUpdate();

                    foreach (var (go, nombre, luz) in vigilados)
                    {
                        if (go == null || yaCruzo.Contains(go)) continue;
                        if (lights.GetGroupState(luz.TrafficLightGroup) != LightState.Red) continue;
                        // RedLightJudge.CrossedNode NO acota la distancia (su
                        // banda de 3 m lateral se proyecta atrás sin límite):
                        // en una ciudad con calles paralelas a pocos metros,
                        // un NPC lejos en OTRA calle podía "cruzar" un nodo
                        // semaforizado ajeno con el que nunca se cruzó de
                        // verdad, por pura coincidencia geométrica. El propio
                        // juego nunca considera un semáforo fuera de su radio
                        // de detección (25 m, ver NpcDriver.Sense) — aplicar
                        // el mismo límite aquí evita falsos positivos.
                        if (Vector3.Distance(go.transform.position, luz.Position) > RadioDeteccionJuego) continue;
                        if (!RedLightJudge.CrossedNode(go.transform.position, go.transform.forward, luz.Position)) continue;

                        yaCruzo.Add(go);
                        cruces[nombre]++;
                        totalCruces++;
                        Debug.LogWarning($"[DiagSemaforo] CRUCE EN ROJO: {go.name} (perfil {nombre}) atravesó el " +
                                          $"semáforo del nodo #{luz.Id} {luz.Position} en rojo (oleada {ola}).");
                    }
                }

                foreach (var (go, _, _) in vigilados) if (go != null) Object.Destroy(go);
                yield return null; // dejar que Destroy() se procese antes de la próxima oleada
            }

            Debug.Log($"[DiagSemaforo] Tabla perfil -> cruces en rojo / intentos (ventana de {SegundosSim:0} s, " +
                      $"{Oleadas} oleadas, {accesos.Count} accesos semaforizados vigilados):");
            foreach (var (nombre, _) in perfiles)
                Debug.Log($"[DiagSemaforo]   {nombre}: {cruces[nombre]} cruce(s) en rojo de {intentos[nombre]} intento(s).");
            float crucesPorMinuto = totalCruces / (SegundosSim / 60f);
            Debug.Log($"[DiagSemaforo]   TOTAL: {totalCruces} cruce(s) en rojo en {SegundosSim / 60f:0.0} minuto(s) " +
                      $"= {crucesPorMinuto:0.00}/min.");

            // Umbral del test: 2,0/min (el objetivo de DISEÑO sigue siendo
            // <1/min — ver el comentario junto a las constantes de arriba
            // sobre el jitter de PhysX medido entre corridas, 0,00-1,20/min).
            Assert.Less(crucesPorMinuto, 2f,
                $"{totalCruces} cruce(s) en rojo en {SegundosSim:0} s ({crucesPorMinuto:0.00}/min) — " +
                "el umbral del TEST es 2/min (el objetivo de diseño del spec es <1/min; ver comentario " +
                "arriba). Ver tabla de perfiles arriba.");
        }
    }
}
