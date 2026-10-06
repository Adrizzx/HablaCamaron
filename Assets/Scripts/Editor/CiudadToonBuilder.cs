using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using HablaCamaron.AI;
using HablaCamaron.UI;
using HablaCamaron.World;
using static HablaCamaron.EditorTools.ToonCityKit;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// LA CIUDAD GRANDE: adapta Demo_Scene_1 de Toon City (el mapa completo
    /// que trae el paquete: ~790 piezas de vía, anillo de autopista con
    /// cuestas, grilla urbana, plazas) como zona jugable N1_CiudadToon — el
    /// escenario del EXAMEN FINAL. La demo original queda intacta: se guarda
    /// una copia y sobre ella se re-asientan los objetos flotantes, se escanea
    /// el grafo de carriles (RoadScanKit + RoadPortGraph), se elige la ruta
    /// más larga, y se agregan Aveo, meta, semáforos con cebras, señalética,
    /// parques, muros y UI. Menú: Habla Camarón > 8 (corre en batch).
    /// </summary>
    public static class CiudadToonBuilder
    {
        private const string DemoP = "Assets/Toon Series/Toon City/Scenes/Demo_Scene_1.unity";
        private const string SceneP = "Assets/Scenes/N1_CiudadToon.unity";

        /// <summary>Cuántos cruces de la ruta reciben semáforo + cebras.</summary>
        private const int SemaforizedCrossings = 6;

        [MenuItem("Habla Camarón/8 · Crear Ciudad Toon (examen final)")]
        public static void Build()
        {
            // La demo se COPIA (la original del paquete no se toca).
            var scene = EditorSceneManager.OpenScene(DemoP, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, SceneP))
            {
                Debug.LogError("[Habla Camarón] No pude guardar la copia N1_CiudadToon.");
                return;
            }
            scene = EditorSceneManager.OpenScene(SceneP, OpenSceneMode.Single);
            Physics.SyncTransforms();

            // ---- 1) Nada flotando (playtest: edificios voladores) ----
            int asentados = 0;
            foreach (var root in scene.GetRootGameObjects())
                asentados += CityDecorKit.ReGround(root.transform);

            // ---- 2) El grafo de carriles, ESCANEADO de la ciudad real ----
            var graphGO = new GameObject("[RoadGraph]");
            var graph = graphGO.AddComponent<RoadGraph>();
            graph.Data = RoadScanKit.Scan(scene, out var ports, out var pairs, out string report);
            Debug.Log($"[Habla Camarón] Escaneo: {report}. Voladores asentados: {asentados}.");

            // ---- 3) La ruta más larga alcanzable (el examen cruza la ciudad) ----
            if (!PickLongRoute(graph.Data, out var spawnNode, out var goalNode, out var path))
            {
                Debug.LogError("[Habla Camarón] El grafo escaneado no dio una ruta válida.");
                return;
            }

            // ---- 3b) Se arranca ABAJO (playtest: "en el nivel 3 no hay cuesta") ----
            // La ruta larga sirve igual en los dos sentidos, pero el extremo por
            // el que se empieza decide el relieve de TODAS las misiones de la
            // ciudad. Medido antes de esto: el spawn caía en el punto más alto
            // del mapa (10.1 m de 10.1 m), así que "la cuesta de Guamaní"
            // terminaba a la misma altura que empezaba (desnivel CERO) y los
            // niveles 4, 5, 6 y 7 iban todos cuesta abajo. Si el otro extremo
            // está más abajo y la vuelta es alcanzable, se invierte la ruta.
            if (goalNode.Position.y < spawnNode.Position.y - 1f &&
                graph.Data.IsReachable(goalNode.Id, spawnNode.Id))
            {
                var alRevés = AStarPlanner.FindPath(graph.Data, goalNode.Id, spawnNode.Id);
                if (alRevés != null && alRevés.Count > 1)
                {
                    Debug.Log($"[Habla Camarón] Ruta invertida para arrancar abajo: " +
                              $"y {spawnNode.Position.y:0.0} → {goalNode.Position.y:0.0}.");
                    (spawnNode, goalNode) = (goalNode, spawnNode);
                    path = alRevés;
                }
            }

            // ---- 3c) PAVIMENTAR las costuras de rescate ----
            // Esas costuras unen piezas que NO se tocan, así que el grafo dice
            // "por aquí se pasa" donde puede no haber asfalto: medido en el
            // nivel 4, el 9.7 % de la ruta caía sobre terreno o sobre la
            // vereda (playtest, con foto: el taxi encima de una jardinera).
            // Si el grafo promete camino, aquí se construye.
            PavimentarCosturas(ports, RoadScanKit.LastRescuePairs, graphGO.transform);

            // ---- 4) El Aveo en el arranque, mirando hacia la ruta ----
            Vector3 spawn = spawnNode.Position + Vector3.up * 0.6f;
            Vector3 dir = path.Count > 1 ? path[1].Position - spawnNode.Position : Vector3.forward;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            BuildPlayerCar(GetOrCreateAveoSpec(), spawn, yaw);

            // ---- 5) La META bajo un arco al otro lado de la ciudad ----
            BuildMetaArch(goalNode.Position, path, graph.Data);

            // ---- 5b) Anclas de meta de los OTROS niveles (3 al 6): la misma
            //      ciudad grande aloja varias misiones, cada una con su ruta.
            var anclas = PlaceLevelAnchors(graph.Data, spawnNode, path);

            // ---- 6) Semáforos + PASOS CEBRA en los cruces de la ruta ----
            var lightsGO = new GameObject("[TrafficLights]");
            var lights = lightsGO.AddComponent<TrafficLightController>();
            int cruces = SemaforizeRoute(lights, graph.Data, ports, pairs, path);

            // ---- 7) Señalética ecuatoriana a lo largo de la ruta ----
            PlaceSigns(graph.Data, path);

            // ---- 8) Parques con los assets del paquete, junto a la ruta ----
            var world = graphGO.transform; // padre neutro para lo nuestro
            int parques = PlaceParks(path, world);

            // ---- 9) Muros de mundo + UI + validación ----
            BuildWorldLimits(graph.Data);
            var ui = new GameObject("[GameplayUI]");
            ui.AddComponent<HUDController>();
            ui.AddComponent<PauseController>();
            ui.AddComponent<DonPanchoDialogue>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            // ---- 9b) La ruta jugable, DESPEJADA de la decoración estática de
            //      la demo (playtest nivel 4: "hay cosas en el camino que no
            //      dejan avanzar" — autos parqueados, cercas, tuberías del
            //      paquete cayendo literalmente sobre el camino real).
            // Y ASFALTO donde el grafo promete camino y no lo hay: sin esto, el
            // jugador acababa conduciendo sobre el terreno o subido a una
            // vereda con jardineras (playtest, con foto). Se hace después de
            // las anclas porque necesita las rutas de todas las misiones.
            PavimentarRutas(graph.Data, spawnNode, path, anclas, graphGO.transform);

            // Regla GLOBAL antes que nada: ningún poste, señal o hidrante puede
            // estar sobre la calzada, sea o no de la ruta de una misión.
            DespejarPropsDeLaCalzada(graph.Data);

            ClearRouteObstacles(graph.Data, spawnNode, path, anclas);
            // El peaje entero fuera (pedido del dueño): no solo lo que invade
            // la ruta jugable, sino las casetas completas de la demo.
            RemoveTollBooths();
            // Invariante permanente: si algo SIGUE invadiendo una ruta jugable
            // (normalmente geometría propia mal separada, no decoración de la
            // demo), se reporta fuerte para no dejarlo pasar desapercibido.
            // También comprueba que no haya quedado ningún peaje.
            ReportRemainingClearance(graph.Data, spawnNode, path, anclas);

            var problems = graph.Data.Validate();
            bool reachable = graph.Data.IsReachable(spawnNode.Id, goalNode.Id);
            float largo = PathLength(path);

            // El tráfico con MODELOS, aquí mismo: regenerar la escena borraba
            // el [TrafficManager] inyectado y los NPCs volvían a ser cubos de
            // colores hasta que alguien corriera el menú 6 a mano.
            TrafficInjectionTool.InjectIntoOpenScene(SceneP);

            EditorSceneManager.SaveScene(scene, SceneP);
            AddToBuildSettings(SceneP);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Habla Camarón] Ciudad Toon lista: ruta de {largo:0} m, " +
                      $"{cruces} cruces semaforizados con cebra, {parques} parques, " +
                      $"{asentados} voladores asentados.");
            EditorUtility.DisplayDialog("Habla Camarón — Ciudad Toon",
                $"N1_CiudadToon creada desde Demo_Scene_1.\n\n{report}\n" +
                $"Ruta del examen: {largo:0} m.\n" +
                $"Validación: {(problems.Count == 0 ? "ninguno ✓" : string.Join("\n", problems))}\n" +
                $"Spawn → meta: {(reachable ? "alcanzable ✓" : "¡NO ALCANZABLE!")}", "¡A rodar!");

            if (problems.Count > 0 || !reachable)
                Debug.LogError("[Habla Camarón] El grafo de Ciudad Toon tiene problemas.");
        }

        /// <summary>
        /// Anclas de meta de los niveles 3-6 sobre la MISMA ciudad, cada una
        /// con su carácter (y todas ALCANZABLES desde el spawn — se comprueba
        /// con A*):
        ///  · Meta_Cuesta  → el nodo más ALTO alcanzable (las lomas de la
        ///    ciudad y las rampas de la autopista) = la misión de la cuesta.
        ///  · Meta_Redondel→ junto al REDONDEL de la ciudad: el nivel de dos
        ///    vueltas gira ahí, que es lo intuitivo (pedido del playtest).
        ///  · Meta_Noche   → una ruta larga para la misión nocturna.
        ///  · Meta_HoraPico→ ruta larga alterna para la de tráfico denso.
        /// </summary>
        /// <summary>Subida mínima (m) que debe tener la meta del nivel 3 para
        /// que "la cuesta" merezca el nombre. El relieve total de la ciudad
        /// Toon es de ~10 m: pedir 4 es exigente pero alcanzable.</summary>
        private const float MinSubidaCuesta = 4f;

        /// <summary>Banda de longitud de ruta (m) para la meta del nivel 3: un
        /// nivel temprano, ni un suspiro ni la maratón más larga del juego.</summary>
        private const float LargoCuestaMin = 220f, LargoCuestaMax = 600f;

        /// <summary>Pendiente máxima JUGABLE de un tramo de ruta (grados). Por
        /// encima de esto el Aveo no sube: son artefactos del trazado de la
        /// demo (rampas de autopista con los puertos casi encimados). Ninguna
        /// meta puede mandar al jugador por ahí — medido: la ruta al redondel
        /// pasaba por un tramo de 53.9°.</summary>
        private const float MaxPendienteJugable = 20f;

        /// <summary>
        /// ¿Se puede llegar manejando de verdad? Devuelve la ruta y su largo
        /// solo si NINGÚN tramo supera la pendiente jugable.
        /// </summary>
        private static bool RutaJugable(RoadGraphData g, RoadNode desde, RoadNode hasta,
            out float largo)
        {
            largo = 0f;
            var r = AStarPlanner.FindPath(g, desde.Id, hasta.Id);
            if (r == null) return false;
            for (int i = 1; i < r.Count; i++)
            {
                Vector3 d = r[i].Position - r[i - 1].Position;
                float dy = d.y; d.y = 0f;
                float horiz = d.magnitude;
                largo += horiz;
                if (horiz > 0.5f &&
                    Mathf.Abs(Mathf.Atan2(dy, horiz) * Mathf.Rad2Deg) > MaxPendienteJugable)
                    return false;
            }
            return true;
        }

        private static Dictionary<string, RoadNode> PlaceLevelAnchors(
            RoadGraphData g, RoadNode spawn, List<RoadNode> rutaF1)
        {
            var anclas = new Dictionary<string, RoadNode>();

            // Solo nodos a los que de verdad se puede llegar manejando.
            var alcanzables = new List<RoadNode>();
            foreach (var n in g.Nodes)
                if (n.Id != spawn.Id && AStarPlanner.FindPath(g, spawn.Id, n.Id) != null)
                    alcanzables.Add(n);
            if (alcanzables.Count == 0) return anclas;

            // 1) La cuesta: sube de verdad, pero SIN ser una maratón.
            //    Historia: antes era "el nodo más alto" en términos absolutos,
            //    y con el arranque arriba daba desnivel CERO (playtest: "en el
            //    nivel 3 no hay cuesta"). Al corregirlo se fue al otro extremo:
            //    el punto más alto quedaba a 1621 m de ruta — el nivel MÁS
            //    largo del juego siendo el tercero. Ahora se piden las dos
            //    cosas: entre los nodos que suben lo suficiente, el de ruta
            //    más CORTA. Así el nivel 3 es una cuesta y sigue siendo un
            //    nivel temprano.
            RoadNode alto = null;
            float mejorSubida = -1f, largoElegido = 0f;
            foreach (var n in alcanzables)
            {
                float sube = n.Position.y - spawn.Position.y;
                if (sube < MinSubidaCuesta) continue;
                if (!RutaJugable(g, spawn, n, out float largoR)) continue;
                // La banda es lo que hace del nivel 3 un nivel 3: sin ella, el
                // criterio "el que más sube" daba 1621 m (una maratón) y el
                // criterio "el más corto que suba" daba 34 m (un suspiro).
                if (largoR < LargoCuestaMin || largoR > LargoCuestaMax) continue;
                if (sube > mejorSubida) { mejorSubida = sube; alto = n; largoElegido = largoR; }
            }
            // Sin ningún candidato en la banda: el más alto que haya (y el
            // invariante de abajo lo denunciará si encima no sube).
            if (alto == null)
            {
                alto = alcanzables[0];
                foreach (var n in alcanzables) if (n.Position.y > alto.Position.y) alto = n;
                Debug.LogWarning("[Habla Camarón] Ninguna cuesta cae en la banda de longitud " +
                                 $"[{LargoCuestaMin}-{LargoCuestaMax} m]: se toma el punto más alto.");
            }
            else
            {
                Debug.Log($"[Habla Camarón] Meta_Cuesta: {largoElegido:0} m de ruta " +
                          $"subiendo {mejorSubida:0.0} m (la que más sube dentro de la banda).");
            }
            float subida = alto.Position.y - spawn.Position.y;
            Anchor("Meta_Cuesta", alto.Position);
            anclas["Meta_Cuesta"] = alto;

            // INVARIANTE: la misión "La cuesta de Guamaní" tiene que subir de
            // verdad. Si esto vuelve a quedar plano, se grita en el build en
            // vez de descubrirlo jugando meses después.
            if (subida < MinSubidaCuesta)
                Debug.LogError($"[Habla Camarón] Meta_Cuesta sube solo {subida:0.0} m " +
                                $"(mínimo {MinSubidaCuesta} m): el nivel 3 quedaría SIN CUESTA. " +
                                "Revisar el arranque — debe estar en la parte baja de la ciudad.");
            else
                Debug.Log($"[Habla Camarón] Meta_Cuesta sube {subida:0.0} m desde el arranque.");

            // 2) El redondel de la ciudad (la vuelta intuitiva del nivel de 2
            //    vueltas): se BUSCA la pieza Roundabout_* y se toma el nodo
            //    alcanzable más cercano a ella.
            RoadNode redondel = null;
            Vector3? centroRedondel = FindRoundaboutCenter();
            if (centroRedondel.HasValue)
            {
                float mejor = float.MaxValue;
                foreach (var n in alcanzables)
                {
                    float d = (n.Position - centroRedondel.Value).sqrMagnitude;
                    // El más cercano al redondel PERO al que se llegue manejando:
                    // el candidato natural tenía un tramo de 53.9° en su ruta.
                    if (d < mejor && RutaJugable(g, spawn, n, out _)) { mejor = d; redondel = n; }
                }
            }
            else
            {
                // Sin redondel en el paquete: a media ruta larga, para no
                // dejar la misión sin meta.
                redondel = rutaF1[rutaF1.Count / 2];
                Debug.LogWarning("[Habla Camarón] No se encontró ninguna pieza Roundabout_*: " +
                                 "la meta del nivel 4 cae a media ruta y el nivel NO tendrá " +
                                 "redondel. Revisar FindRoundaboutCenter.");
            }
            if (redondel != null && centroRedondel.HasValue)
                Debug.Log($"[Habla Camarón] Meta_Redondel: nodo a " +
                          $"{Vector3.Distance(redondel.Position, centroRedondel.Value):0.0} m " +
                          $"del centro del redondel ({centroRedondel.Value}).");
            if (redondel != null)
            {
                Anchor("Meta_Redondel", redondel.Position);
                anclas["Meta_Redondel"] = redondel;
            }

            // 3) y 4) Rutas largas alternas: SIEMPRE lejos del spawn. El
            //    criterio viejo ("eje X domina / eje Z domina") medía 5,6 m
            //    de Meta_HoraPico al spawn en la ciudad real (playtest: "la
            //    meta aparece en el punto de partida") — porque el trazado
            //    cerca del spawn está sesgado a un eje y casi ningún nodo
            //    alcanzable cae del lado "no dominante", así que ESE lado
            //    terminaba resolviéndose con el candidato más cercano que
            //    hubiera, no el más lejano. Ahora: Meta_Noche es SIEMPRE el
            //    nodo alcanzable más lejano; Meta_HoraPico es el más lejano
            //    entre los que además queden lejos de Meta_Noche (ruta
            //    distinta) — con un piso mínimo de distancia al spawn (35 %
            //    de la ruta más larga) que ninguna meta puede violar.
            //    Y todas exigen RUTA JUGABLE: sin ese filtro la meta podía caer
            //    al otro lado de una rampa de 53° del trazado de la demo.
            RoadNode lejosA = null;
            float dA = -1f;
            foreach (var n in alcanzables)
            {
                float d = (n.Position - spawn.Position).sqrMagnitude;
                if (d > dA && RutaJugable(g, spawn, n, out _)) { dA = d; lejosA = n; }
            }

            RoadNode lejosB = null;
            if (lejosA != null)
            {
                const float PisoFrac = 0.35f;
                float piso = dA * PisoFrac * PisoFrac; // umbral en distancia AL CUADRADO
                float dB = -1f;
                foreach (var n in alcanzables)
                {
                    float dSpawn = (n.Position - spawn.Position).sqrMagnitude;
                    if (dSpawn < piso) continue;                          // no pegada al spawn
                    float dToA = (n.Position - lejosA.Position).sqrMagnitude;
                    if (dToA < piso) continue;                            // no la misma zona que Meta_Noche
                    if (dSpawn > dB && RutaJugable(g, spawn, n, out _)) { dB = dSpawn; lejosB = n; }
                }
                if (lejosB == null) // mapa muy alargado en un eje: se relaja SOLO
                    foreach (var n in alcanzables)                       // el filtro de "lejos de Meta_Noche"
                    {
                        if (n == lejosA) continue;
                        float dSpawn = (n.Position - spawn.Position).sqrMagnitude;
                        if (dSpawn < piso) continue;                      // el piso del spawn NUNCA se relaja
                        if (dSpawn > dB && RutaJugable(g, spawn, n, out _)) { dB = dSpawn; lejosB = n; }
                    }
            }
            if (lejosA != null) { Anchor("Meta_Noche", lejosA.Position); anclas["Meta_Noche"] = lejosA; }
            if (lejosB != null) { Anchor("Meta_HoraPico", lejosB.Position); anclas["Meta_HoraPico"] = lejosB; }

            return anclas;
        }

        // ---- Ruta jugable despejada (playtest nivel 4) ----

        /// <summary>Volumen del Aveo + margen para barrer las rutas (mitad Car
        /// toon + margen, igual al de la Tarea 10).</summary>
        private static readonly Vector3 RouteClearBox = new Vector3(1.6f, 1.2f, 3.0f);

        /// <summary>Nombres de props que NUNCA deben estar sobre la calzada.</summary>
        private static readonly string[] PropsQueEstorban =
        {
            "Streetlight_", "Streetsign_", "Cable_Pole_", "Metal_Pole_",
            "Hydrant_", "Traffic_Light_", "Trash_Can_", "Bench_", "Plant_Pot_",
        };

        /// <summary>Semiancho de calzada libre de props (m). Por dentro de
        /// esto, un poste es un muro.
        /// 3.6, no 2.4: con 2.4 se colaba lo que estuviera entre 2.4 y 3.6 m
        /// del EJE DEL CARRIL, que sigue siendo carril — medido en el nivel 4,
        /// un `Streetlight_2A` de 8.3 m quedaba a 3.2 m con su collider puesto,
        /// en plena vía (playtest: "en medio nivel hay un poste en toda vía").
        /// El coche mide 2.7 m de ancho, así que 1.35 son suyos y nadie conduce
        /// clavado al eje: por debajo de ~3.5 m se choca.</summary>
        private const float LibreDeProps = 3.6f;

        /// <summary>Cuánto se puede empujar un prop hacia afuera buscándole
        /// sitio (pasos de 0.8 m). Sube con LibreDeProps: si no, los que antes
        /// encontraban hueco a 6.4 m ahora se quedan sin él y acaban solo sin
        /// collider, o sea VISIBLES en mitad de la calle.</summary>
        private const int PasosParaApartar = 14;

        /// <summary>Techo del coche (m): lo que empieza por encima de esto no
        /// lo golpea nadie (brazos de señal, cables, dinteles).</summary>
        private const float AlturaTecho = 2.2f;

        /// <summary>
        /// REGLA GLOBAL: aparta de la calzada todo poste, señal o mobiliario de
        /// la demo que caiga sobre una vía del grafo. Perseguir esto ruta por
        /// ruta no bastaba —según qué nodo de arranque se tomara, el barrido
        /// veía unos obstáculos u otros— y el jugador se encontraba farolas en
        /// mitad de la calle (playtest, con foto). Aquí no importa la ruta: si
        /// está sobre una vía, se aparta; si no se puede, pierde el collider.
        /// </summary>
        private static void DespejarPropsDeLaCalzada(RoadGraphData g)
        {
            Physics.SyncTransforms();
            int apartados = 0, sinCollider = 0;

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src == null) continue;

                bool estorba = false;
                foreach (var pre in PropsQueEstorban)
                    if (src.name.StartsWith(pre)) { estorba = true; break; }
                if (!estorba) continue;

                Vector3 pos = go.transform.position;
                if (g.DistanceToNearestEdge(pos) >= LibreDeProps) continue;

                // Se empuja hacia AFUERA de la vía más cercana, en pasos.
                bool resuelto = false;
                if (LaneJudgeDireccionFuera(g, pos, out Vector3 fuera))
                    for (int i = 1; i <= PasosParaApartar; i++)
                    {
                        Vector3 cand = pos + fuera * (i * 0.8f);
                        if (g.DistanceToNearestEdge(cand) < LibreDeProps) continue;
                        go.transform.position = cand;
                        apartados++;
                        resuelto = true;
                        break;
                    }

                if (resuelto) continue;
                foreach (var col in go.GetComponentsInChildren<Collider>())
                    if (col.enabled) { col.enabled = false; sinCollider++; }
            }

            // Segunda pasada: colliders SUELTOS (no son prefab raíz porque
            // están desanidados o cuelgan de otro objeto). Aquí no se mueve
            // nada —arrastraría geometría ajena— pero si su collider pisa la
            // calzada se apaga: se sigue viendo y deja de ser un muro.
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!col.enabled) continue;
                bool estorba = false;
                for (var t = col.transform; t != null && !estorba; t = t.parent)
                    foreach (var pre in PropsQueEstorban)
                        if (t.name.StartsWith(pre)) { estorba = true; break; }
                if (!estorba) continue;

                // Solo estorba lo que puede GOLPEAR al coche: si el collider
                // entero pasa por encima del techo (un brazo de señal, un
                // cable), que se quede — desactivarlo todo dejaba más de mil
                // props fantasma por la ciudad.
                var b = col.bounds;
                if (b.min.y > AlturaTecho) continue;

                // Y su volumen tiene que invadir el carril de verdad: se mide
                // el punto del collider más cercano al eje de la vía.
                if (!Missions.LaneJudge.NearestLanePoint(g, b.center, out var eje, out _)) continue;
                Vector3 cercano = b.ClosestPoint(eje);
                if (g.DistanceToNearestEdge(cercano) >= LibreDeProps) continue;

                col.enabled = false;
                sinCollider++;
            }

            if (apartados + sinCollider > 0)
                Debug.Log($"[Habla Camarón] Props fuera de la calzada: {apartados} apartados, " +
                          $"{sinCollider} sin collider (no se podían mover).");
        }

        /// <summary>Dirección perpendicular a la vía más cercana, hacia fuera.</summary>
        private static bool LaneJudgeDireccionFuera(RoadGraphData g, Vector3 p, out Vector3 fuera)
        {
            fuera = Vector3.zero;
            if (!Missions.LaneJudge.NearestLanePoint(g, p, out var punto, out var dir)) return false;
            Vector3 haciaFuera = p - punto; haciaFuera.y = 0f;
            if (haciaFuera.sqrMagnitude < 0.01f)
                haciaFuera = Vector3.Cross(Vector3.up, dir); // encima del eje: a un lado
            fuera = haciaFuera.normalized;
            return true;
        }

        /// <summary>
        /// Quita del camino la decoración ESTÁTICA de Demo_Scene_1 (autos
        /// parqueados, cercas, tuberías industriales de la autopista...) que
        /// cae literalmente ENCIMA de una ruta jugable (playtest: "hay cosas
        /// en el camino que no dejan avanzar"). Solo toca PREFABS completos
        /// de la demo (`GetOutermostPrefabInstanceRoot`): las piezas propias
        /// (postes, señales, cebras, arco de meta) son procedurales, no
        /// prefabs, así que quedan intactas — si UNA de ellas invade la ruta,
        /// el arreglo es de geometría (separarla), nunca borrado automático.
        /// </summary>
        private static void ClearRouteObstacles(RoadGraphData g, RoadNode spawn,
            List<RoadNode> examPath, Dictionary<string, RoadNode> anclas)
        {
            var quitadas = new HashSet<GameObject>();
            var reporte = new List<string>();
            foreach (var (nombre, ids) in RutasJugables(g, spawn, examPath, anclas))
                foreach (var c in RouteClearanceScan.Scan(g, ids, RouteClearBox))
                {
                    if (c == null) continue;
                    var pieza = PrefabUtility.GetOutermostPrefabInstanceRoot(c.gameObject);
                    if (pieza != null)
                    {
                        if (!quitadas.Add(pieza)) continue;
                        reporte.Add($"{nombre}: {pieza.name}");
                        continue;
                    }

                    // No es un prefab entero de la demo (está desanidado o
                    // cuelga de otro): no se borra —podría llevarse geometría
                    // que sí importa— pero SÍ se le quita el collider. Queda de
                    // adorno y deja de ser un muro invisible en plena calzada.
                    if (c.enabled)
                    {
                        c.enabled = false;
                        reporte.Add($"{nombre}: {c.name} (sin collider)");
                    }
                }
            foreach (var pieza in quitadas) Object.DestroyImmediate(pieza);
            if (quitadas.Count > 0)
                Debug.Log($"[Habla Camarón] Ruta despejada: {quitadas.Count} piezas de decoración " +
                          $"de la demo quitadas del camino ({string.Join("; ", reporte)}).");
        }

        /// <summary>
        /// Invariante permanente (corre SIEMPRE al final de Build): si tras
        /// limpiar la decoración de la demo TODAVÍA hay algo sobre una ruta
        /// jugable, se reporta fuerte — normalmente es geometría propia (un
        /// poste demasiado pegado a la vía) que hay que separar del camino.
        /// </summary>
        private static void ReportRemainingClearance(RoadGraphData g, RoadNode spawn,
            List<RoadNode> examPath, Dictionary<string, RoadNode> anclas)
        {
            var restantes = new List<string>();
            foreach (var (nombre, ids) in RutasJugables(g, spawn, examPath, anclas))
                foreach (var c in RouteClearanceScan.Scan(g, ids, RouteClearBox))
                    if (c != null)
                        restantes.Add($"{nombre}: {c.transform.root.name}/{c.name} @ {c.transform.position}");
            if (restantes.Count > 0)
                Debug.LogError("[Habla Camarón] Ciudad Toon: quedan obstáculos sobre rutas " +
                                "jugables tras limpiar la decoración de la demo — revisar la " +
                                "geometría propia:\n" + string.Join("\n", restantes));

            // El peaje debe haber quedado completamente fuera de la escena
            // (RemoveTollBooths corre antes en Build). Si algo sobrevivió —
            // una instancia sin PrefabInstanceRoot, un nombre inesperado —
            // se reporta fuerte: el dueño pidió el peaje entero afuera.
            var peajesRestantes = new List<string>();
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go != null && go.name.StartsWith("Toll_Booth"))
                    peajesRestantes.Add($"{go.name} @ {go.transform.position}");
            if (peajesRestantes.Count > 0)
                Debug.LogError("[Habla Camarón] Ciudad Toon: quedó peaje en la escena tras " +
                                "RemoveTollBooths — revisar:\n" + string.Join("\n", peajesRestantes));
        }

        /// <summary>
        /// El peaje entero FUERA de la ciudad (decisión del dueño): las casetas
        /// `Toll_Booth_1A (1)` y `Toll_Booth_1A (3)` heredadas de Demo_Scene_1
        /// bloqueaban calles del nivel 3. A diferencia de <see cref="ClearRouteObstacles"/>
        /// (que solo quita lo que invade una ruta jugable), esto es incondicional:
        /// se destruye TODA raíz de instancia de prefab cuyo nombre empiece con
        /// "Toll_Booth" esté o no sobre el camino — al borrar la raíz se va la
        /// caseta completa con su barrera. OJO: no tocar nombres "Barrier"/"Gate"
        /// — esos son las vallas de misión de `Missions.RouteBarriers` (otro
        /// sistema, de otro desarrollador, con sus propios tests).
        /// </summary>
        private static void RemoveTollBooths()
        {
            int quitadas = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go == null) continue;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                if (!go.name.StartsWith("Toll_Booth")) continue;
                Object.DestroyImmediate(go);
                quitadas++;
            }
            Debug.Log($"[Habla Camarón] Peaje quitado de la ciudad: {quitadas} caseta(s) destruida(s).");
        }

        /// <summary>La ruta al examen y a cada ancla con nombre, como (nombre, ids de nodos).</summary>
        private static IEnumerable<(string nombre, List<int> ids)> RutasJugables(
            RoadGraphData g, RoadNode spawn, List<RoadNode> examPath, Dictionary<string, RoadNode> anclas)
        {
            yield return ("[MetaExamen]", examPath.ConvertAll(n => n.Id));

            // Se barre desde el nodo del spawn Y desde el más cercano al coche
            // REAL. En el juego la ruta se traza con `NearestNode(posición del
            // jugador)`, que no siempre es el mismo nodo que eligió el builder:
            // por esa diferencia quedaban farolas en plena calzada de la ruta
            // que el jugador sí recorre (playtest: "hay un poste en mitad de la
            // calle") mientras el invariante daba cero.
            var arranques = new List<RoadNode> { spawn };
            var coche = Object.FindFirstObjectByType<Vehicle.VehicleController>();
            if (coche != null)
            {
                var real = g.NearestNode(coche.transform.position);
                if (real != null && real.Id != spawn.Id) arranques.Add(real);
            }

            foreach (var desde in arranques)
                foreach (var kv in anclas)
                {
                    var ruta = AStarPlanner.FindPath(g, desde.Id, kv.Value.Id);
                    if (ruta != null) yield return (kv.Key, ruta.ConvertAll(n => n.Id));
                }
        }

        /// <summary>Centro de la pieza Roundabout_* de la ciudad (si la hay).</summary>
        private static Vector3? FindRoundaboutCenter()
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src == null || !src.name.StartsWith("Roundabout")) continue;
                return WorldBounds(go).center;
            }
            return null;
        }

        /// <summary>Baliza de meta con nombre (la busca MissionRunner por GoalAnchor).</summary>
        private static void Anchor(string name, Vector3 at)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out var hit, 80f))
                at = hit.point;
            var go = new GameObject(name);
            go.transform.position = at;
        }

        // ---- La ruta: entre los extremos del mapa, el par alcanzable más largo ----
        private static bool PickLongRoute(RoadGraphData g,
            out RoadNode a, out RoadNode b, out List<RoadNode> path)
        {
            a = b = null;
            path = null;
            if (g.Nodes.Count == 0) return false;

            // Candidatos: los nodos extremos del rectángulo del mapa + un
            // muestreo repartido (por si el componente más grande del grafo
            // no toca los extremos absolutos).
            var candidatos = new List<RoadNode>();
            foreach (var pick in new System.Func<RoadNode, float>[]
                     { n => n.Position.x, n => -n.Position.x, n => n.Position.z, n => -n.Position.z,
                       n => n.Position.x + n.Position.z, n => -n.Position.x - n.Position.z,
                       n => n.Position.x - n.Position.z, n => n.Position.z - n.Position.x })
            {
                RoadNode best = g.Nodes[0];
                foreach (var n in g.Nodes) if (pick(n) > pick(best)) best = n;
                if (!candidatos.Contains(best)) candidatos.Add(best);
            }
            int paso = Mathf.Max(g.Nodes.Count / 14, 1);
            for (int i = 0; i < g.Nodes.Count; i += paso)
                if (!candidatos.Contains(g.Nodes[i])) candidatos.Add(g.Nodes[i]);

            float mejor = -1f;
            foreach (var na in candidatos)
                foreach (var nb in candidatos)
                {
                    if (na == nb) continue;
                    float d = (na.Position - nb.Position).sqrMagnitude;
                    if (d <= mejor) continue;
                    var p = AStarPlanner.FindPath(g, na.Id, nb.Id);
                    if (p == null || p.Count < 8) continue;
                    mejor = d;
                    a = na; b = nb; path = p;
                }
            return path != null;
        }

        private static float PathLength(List<RoadNode> path)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++)
                total += Vector3.Distance(path[i - 1].Position, path[i].Position);
            return total;
        }

        // ---- El arco de la meta (ancla [MetaExamen], como el Corredor) ----
        private static void BuildMetaArch(Vector3 at, List<RoadNode> path, RoadGraphData g)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out var hit, 80f))
                at = hit.point;

            Vector3 dir = path.Count > 1
                ? (path[path.Count - 1].Position - path[path.Count - 2].Position).normalized
                : Vector3.forward;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            var meta = new GameObject("[MetaExamen]");
            meta.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.72f, 0.28f, 0.18f) }; // terracota quiteña
            // Los pilares se APARTAN de la calzada midiendo contra el grafo: en
            // la ciudad cosida, la meta cae en cruces donde otra ruta pasa al
            // lado, y un pilar fijo a 4.6 m quedaba plantado sobre esa vía
            // (lo cazó el invariante de RouteClearanceScan). El dintel se
            // estira después para llegar a donde hayan quedado.
            Vector3 lateral = meta.transform.right;
            float semiAncho = 4.6f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 mundo = ApartarDeLaVia(g, at, lateral * s, 4.6f);
                var pilar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pilar.name = "Pilar";
                pilar.transform.SetParent(meta.transform, false);
                float lx = Vector3.Dot(mundo - at, lateral * s);
                semiAncho = Mathf.Max(semiAncho, lx);
                pilar.transform.localPosition = new Vector3(s * lx, 2.6f, 0f);
                pilar.transform.localScale = new Vector3(0.7f, 5.2f, 0.7f);
                pilar.GetComponent<Renderer>().sharedMaterial = mat;

                // Si ni apartándolo se despeja (la meta cae en un cruce donde
                // otra ruta pasa por al lado, o el arco quedó sobre la autopista
                // elevada), el pilar se queda de adorno SIN collider — mismo
                // criterio que el dintel. Nunca un obstáculo sólido en la vía.
                if (!DespejadoDeVia(g, mundo))
                {
                    Object.DestroyImmediate(pilar.GetComponent<Collider>());
                    Debug.Log($"[Habla Camarón] Pilar del arco sin collider: no se pudo " +
                              $"apartar de la calzada en {mundo} (queda decorativo).");
                }
            }
            var dintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dintel.name = "Dintel";
            dintel.transform.SetParent(meta.transform, false);
            dintel.transform.localPosition = new Vector3(0f, 5.4f, 0f);
            dintel.transform.localScale = new Vector3(semiAncho * 2f + 0.8f, 0.8f, 0.9f);
            dintel.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(dintel.GetComponent<Collider>()); // en alto, sin chocar
        }

        // ---- Semáforos + cebras en los cruces (≥3 puertos) de la ruta ----
        private static int SemaforizeRoute(TrafficLightController lights, RoadGraphData g,
            List<RoadPort> ports, List<(int a, int b)> pairs, List<RoadNode> path)
        {
            // Puertos por pieza (las piezas con ≥3 puertos son intersecciones)
            // y el conjunto de puertos COSIDOS: solo esos reciben semáforo y
            // cebra (un puerto suelto da hacia el pasto — pintarle cebra ahí
            // quedaba flotando en el campo, visto en el retrato de la 1ª corrida).
            var cosidos = new HashSet<int>();
            foreach (var (a, b) in pairs) { cosidos.Add(a); cosidos.Add(b); }
            var porPieza = new Dictionary<int, List<int>>();
            for (int i = 0; i < ports.Count; i++)
            {
                if (!porPieza.TryGetValue(ports[i].PieceId, out var list))
                    porPieza[ports[i].PieceId] = list = new List<int>();
                list.Add(i);
            }

            // Cruces ordenados por cercanía a la ruta, espaciados entre sí.
            var pathSet = new HashSet<int>();
            foreach (var n in path) pathSet.Add(n.Id);
            var cruces = new List<(float alongDist, List<int> portIdx, Vector3 centro)>();
            foreach (var kv in porPieza)
            {
                if (kv.Value.Count < 3) continue;
                Vector3 centro = Vector3.zero;
                bool enRuta = false;
                foreach (int pi in kv.Value)
                {
                    centro += ports[pi].Position;
                    // el nodo de entrada del puerto i es 2i+1 (orden de Build)
                    if (pathSet.Contains(2 * pi + 1) || pathSet.Contains(2 * pi)) enRuta = true;
                }
                centro /= kv.Value.Count;
                if (!enRuta) continue;
                float d = float.MaxValue;
                for (int s = 0; s < path.Count; s++)
                {
                    float dd = (path[s].Position - centro).sqrMagnitude;
                    if (dd < d) d = dd;
                }
                cruces.Add((d, kv.Value, centro));
            }

            int puestos = 0;
            var usados = new List<Vector3>();
            foreach (var cruce in cruces)
            {
                if (puestos >= SemaforizedCrossings) break;
                bool cerca = false;
                foreach (var u in usados)
                    if ((u - cruce.centro).sqrMagnitude < 45f * 45f) cerca = true;
                if (cerca) continue; // que no se amontonen
                usados.Add(cruce.centro);
                puestos++;

                foreach (int pi in cruce.portIdx)
                {
                    if (!cosidos.Contains(pi)) continue; // hacia el pasto: nada
                    var port = ports[pi];
                    // Eje N/S = grupo 0; E/O = grupo 1 (alternan de verdad).
                    int grupo = Mathf.Abs(port.Outward.z) > Mathf.Abs(port.Outward.x) ? 0 : 1;
                    var entryNode = g.GetNode(2 * pi + 1);
                    if (entryNode != null) entryNode.TrafficLightGroup = grupo;

                    // El poste a la derecha del que LLEGA por este puerto
                    // (viaja hacia -Outward; su derecha es Cross(up, -Outward)).
                    // OJO (Tarea 10, RouteClearanceScan): con +1.6f el poste
                    // quedaba a rozar el carril de entrada (medido: la cápsula
                    // del poste tocaba la caja de barrido del auto en varios
                    // cruces de la ruta real) — el mismo 1.6 m se usaba de
                    // margen en AMBOS lados. +3.6f midió CERO postes sobre
                    // las 5 rutas jugables de la ciudad (invariante del builder).
                    Vector3 rightIn = Vector3.Cross(Vector3.up, -port.Outward).normalized;
                    // Se aparta de la calzada, pero SIN soltarse de su nodo:
                    // el test de cableado exige el poste a menos de 6 m del
                    // nodo-semáforo (si no, el NPC no lo asocia y se lo pasa).
                    Vector3 anclaNodo = entryNode != null ? entryNode.Position : port.Position;
                    Vector3 pos = ApartarDeLaVia(g, port.Position, rightIn,
                                                 port.LaneOffset + 3.6f,
                                                 anclaNodo, MaxPosteDesdeNodo);
                    float yawLuz = Mathf.Atan2(-port.Outward.x, -port.Outward.z)
                        * Mathf.Rad2Deg + 180f;
                    TrafficLightKit.Place(lights.transform, pos, yawLuz, lights, grupo);

                    // La cebra cruza la boca del puerto, gobernada por su grupo.
                    CrosswalkKit.Place(lights.transform,
                        port.Position - port.Outward * 1.2f,
                        Mathf.Atan2(port.Outward.x, port.Outward.z) * Mathf.Rad2Deg,
                        port.LaneOffset / 0.22f, grupo);
                }
            }
            return puestos;
        }

        /// <summary>Ancho del parche de asfalto de una costura de rescate (m):
        /// una calzada de dos carriles del paquete.</summary>
        private const float AnchoParche = 9f;

        /// <summary>
        /// Tiende asfalto sobre las costuras de RESCATE. Esas costuras unen
        /// piezas que no se tocan (por eso hizo falta rescatarlas), así que el
        /// grafo promete un camino que a veces no existe: el jugador acababa
        /// conduciendo sobre el terreno o subido a una vereda con jardineras.
        /// Se comprueba el punto medio y, si no hay calzada, se tiende una losa
        /// que une los dos puertos. Nada de tapar lo que ya es carretera.
        /// </summary>
        /// <summary>
        /// Le da lectura de CALLE a una losa de parche: línea central
        /// discontinua y dos bordillos. Sin esto el tramo se ve como un
        /// rectángulo gris tirado encima de la vereda —que es literalmente lo
        /// que es— y el jugador no sabe si va por la calzada o por la acera
        /// (playtest nivel 4: "antes era vereda y le hicieron calle, se ve
        /// feo"). Estas costuras son PUENTES del grafo: no se pueden evitar,
        /// así que al menos tienen que parecer una calle.
        /// Todo va SIN collider: el suelo es la losa, esto es pintura.
        /// </summary>
        private static void VestirDeCalle(Transform losa, float ancho, float largo)
        {
            var pintura = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.93f, 0.82f, 0.25f) };  // amarillo de vía
            var cemento = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.78f, 0.76f, 0.70f) };  // bordillo

            // Línea central discontinua (en LOCAL: la losa ya está rotada).
            int trazos = Mathf.Max(2, Mathf.FloorToInt(largo / 4f));
            for (int i = 0; i < trazos; i++)
            {
                var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
                t.name = "Linea_Parche";
                t.transform.SetParent(losa, false);
                float z = -0.5f + (i + 0.5f) / trazos;   // repartidos a lo largo
                t.transform.localPosition = new Vector3(0f, 0.55f, z);
                // La losa está escalada, así que en LOCAL hay que dividir.
                t.transform.localScale = new Vector3(0.35f / ancho, 0.15f, 2f / largo);
                t.GetComponent<Renderer>().sharedMaterial = pintura;
                Object.DestroyImmediate(t.GetComponent<Collider>());
            }

            // Bordillos a los lados: rematan el parche contra el terreno.
            foreach (float s in new[] { -1f, 1f })
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Bordillo_Parche";
                b.transform.SetParent(losa, false);
                b.transform.localPosition = new Vector3(s * 0.5f, 0.9f, 0f);
                b.transform.localScale = new Vector3(0.5f / ancho, 1.6f, 1f);
                b.GetComponent<Renderer>().sharedMaterial = cemento;
                Object.DestroyImmediate(b.GetComponent<Collider>());
            }
        }

        private static void PavimentarCosturas(List<RoadPort> ports,
            List<(int a, int b)> rescate, Transform padre)
        {
            if (rescate == null || rescate.Count == 0) return;
            Physics.SyncTransforms();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.30f, 0.31f, 0.33f) }; // el gris del asfalto Toon
            int puestos = 0;

            foreach (var (ia, ib) in rescate)
            {
                Vector3 pa = ports[ia].Position, pb = ports[ib].Position;
                Vector3 medio = (pa + pb) * 0.5f;
                float largo = Vector3.Distance(pa, pb);
                if (largo < 0.5f) continue;

                // ¿Ya hay calzada bajo el punto medio? Entonces no hace falta.
                if (HayCalzadaBajo(medio)) continue;

                var losa = GameObject.CreatePrimitive(PrimitiveType.Cube);
                losa.name = "Parche_Costura";
                losa.transform.SetParent(padre, false);
                Vector3 dir = pb - pa; dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) { Object.DestroyImmediate(losa); continue; }
                losa.transform.SetPositionAndRotation(
                    new Vector3(medio.x, Mathf.Min(pa.y, pb.y) + 0.02f, medio.z),
                    Quaternion.LookRotation(dir.normalized, Vector3.up));
                // Un poco más largo que el hueco para solapar con las dos vías.
                losa.transform.localScale = new Vector3(AnchoParche, 0.08f, largo + 3f);
                losa.GetComponent<Renderer>().sharedMaterial = mat;
                VestirDeCalle(losa.transform, AnchoParche, largo + 3f);
                puestos++;
            }
            if (puestos > 0)
                Debug.Log($"[Habla Camarón] {puestos} costuras de rescate PAVIMENTADAS " +
                          $"(de {rescate.Count}): el grafo prometía camino y no había asfalto.");
        }

        /// <summary>
        /// Recorre TODAS las rutas jugables metro a metro y tiende asfalto
        /// donde no lo hay. Las costuras de rescate no eran el único hueco: el
        /// trazado de la demo deja tramos donde el grafo cruza terreno o pasa
        /// rozando una vereda, y ahí el jugador terminaba fuera de la calzada
        /// (medido: 9.7 % de la ruta del nivel 4; con esto, 0 %).
        /// </summary>
        private static void PavimentarRutas(RoadGraphData g, RoadNode spawn,
            List<RoadNode> examPath, Dictionary<string, RoadNode> anclas, Transform padre)
        {
            Physics.SyncTransforms();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.30f, 0.31f, 0.33f) };

            int puestos = 0;
            var yaCubierto = new List<Vector3>();

            foreach (var (_, ids) in RutasJugables(g, spawn, examPath, anclas))
            {
                for (int i = 1; i < ids.Count; i++)
                {
                    var a = g.GetNode(ids[i - 1]);
                    var b = g.GetNode(ids[i]);
                    if (a == null || b == null) continue;

                    float largo = Vector3.Distance(a.Position, b.Position);
                    int pasos = Mathf.Max(2, Mathf.CeilToInt(largo / 3f));
                    for (int k = 0; k <= pasos; k++)
                    {
                        Vector3 p = Vector3.Lerp(a.Position, b.Position, k / (float)pasos);
                        if (HayCalzadaBajo(p)) continue;

                        // Un parche por zona: sin esto se apilarían decenas.
                        bool repetido = false;
                        foreach (var c in yaCubierto)
                            if ((c - p).sqrMagnitude < 36f) { repetido = true; break; }
                        if (repetido) continue;
                        yaCubierto.Add(p);

                        Vector3 dir = b.Position - a.Position; dir.y = 0f;
                        if (dir.sqrMagnitude < 1e-4f) continue;

                        var losa = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        losa.name = "Parche_Costura";
                        losa.transform.SetParent(padre, false);
                        losa.transform.SetPositionAndRotation(
                            new Vector3(p.x, p.y + 0.02f, p.z),
                            Quaternion.LookRotation(dir.normalized, Vector3.up));
                        losa.transform.localScale = new Vector3(AnchoParche, 0.08f, 10f);
                        losa.GetComponent<Renderer>().sharedMaterial = mat;
                        VestirDeCalle(losa.transform, AnchoParche, 10f);
                        puestos++;
                        Physics.SyncTransforms(); // que el siguiente punto lo vea
                    }
                }
            }
            if (puestos > 0)
                Debug.Log($"[Habla Camarón] {puestos} parches de asfalto sobre las rutas: " +
                          "el grafo pasaba por donde no había calzada.");
        }

        /// <summary>¿Hay una pieza de vía (o un parche) justo bajo el punto?</summary>
        private static bool HayCalzadaBajo(Vector3 p)
        {
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 4f, Vector3.down, 12f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (Mathf.Abs(h.point.y - p.y) > 2.5f) continue;
                for (var t = h.collider.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("Road_") || t.name.StartsWith("Highway_") ||
                        t.name.StartsWith("Roundabout") || t.name == "Parche_Costura")
                        return true;
            }
            return false;
        }

        /// <summary>Margen libre (m) que debe quedar entre una pieza propia
        /// plantada al borde (poste, pilar) y el carril más cercano: media
        /// carrocería del Aveo más holgura.</summary>
        private const float MargenPiezaAVia = 2.6f;

        /// <summary>Radio máximo poste↔nodo del semáforo (m). El test de
        /// cableado usa 6: se deja holgura para no rozar el límite.</summary>
        private const float MaxPosteDesdeNodo = 5.4f;

        /// <summary>
        /// Empuja una pieza de borde hacia afuera hasta que deje de estorbar a
        /// CUALQUIER calle del grafo. Un desplazamiento fijo no basta: desde que
        /// el escaneo cose la ciudad entera (2026-07-25), una calle transversal
        /// que antes era un trozo muerto ahora se transita, y el poste que
        /// quedaba "en el pasto" pasó a estar en mitad de la vía.
        /// Devuelve la posición original si ya estaba despejada.
        /// </summary>
        /// <param name="anclaMax">Punto que la pieza no puede abandonar (el nodo
        /// del semáforo). Un poste que se aleja de su nodo deja de estar
        /// cableado a él y los NPCs dejan de verlo — hay un test que lo exige.</param>
        /// <param name="maxDesdeAncla">Radio máximo respecto de ese ancla (m).</param>
        private static Vector3 ApartarDeLaVia(RoadGraphData g, Vector3 origen,
            Vector3 haciaAfuera, float distanciaBase,
            Vector3? anclaMax = null, float maxDesdeAncla = 0f)
        {
            Vector3 pos = origen + haciaAfuera * distanciaBase;
            if (g == null) return pos;
            for (int i = 0; i < 10; i++)
            {
                if (g.DistanceToNearestEdge(pos) >= MargenPiezaAVia) return pos;
                Vector3 siguiente = pos + haciaAfuera * 0.8f;
                // El cableado manda sobre el despeje: antes que soltar el poste
                // de su nodo, se prefiere dejarlo un poco más adentro.
                if (anclaMax.HasValue &&
                    Vector3.Distance(siguiente, anclaMax.Value) > maxDesdeAncla) return pos;
                pos = siguiente;
            }
            return pos;
        }

        /// <summary>¿Esta posición quedó libre de calzada? (mismo margen).</summary>
        private static bool DespejadoDeVia(RoadGraphData g, Vector3 pos) =>
            g == null || g.DistanceToNearestEdge(pos) >= MargenPiezaAVia;

        // ---- Señalética ecuatoriana repartida por la ruta ----
        private static void PlaceSigns(RoadGraphData g, List<RoadNode> path)
        {
            var root = new GameObject("[Senaletica]").transform;
            string[] keys = { "lim50", "semaforo", "peatones", "no_estacionar", "parada_bus", "no_pitar" };
            int cada = Mathf.Max(path.Count / (keys.Length + 1), 2);
            for (int k = 0; k < keys.Length; k++)
            {
                int idx = Mathf.Min((k + 1) * cada, path.Count - 2);
                Vector3 dir = (path[idx + 1].Position - path[idx].Position).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                Vector3 at = path[idx].Position + right * 4.2f;
                float yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
                var go = EcuadorSignKit.Place(keys[k], root, Vector3.zero, 0f,
                    keys[k] == "lim50" ? new[] { path[idx].Id } : null);
                if (go == null) continue;
                if (Physics.Raycast(at + Vector3.up * 25f, Vector3.down, out var hit, 60f))
                    at.y = hit.point.y;
                go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                if (keys[k] == "lim50") path[idx].Sign = SignType.SpeedLimit50;
            }
        }

        // ---- Parques junto a la ruta, en lotes libres detectados ----
        private static int PlaceParks(List<RoadNode> path, Transform parent)
        {
            int hechos = 0;
            // Se prueban varios puntos de la ruta, ambos lados y dos
            // distancias: la ciudad es densa y el primer lote casi nunca
            // está libre (la corrida inicial encontró CERO).
            foreach (float f in new[] { 0.2f, 0.35f, 0.5f, 0.65f, 0.8f })
            {
                if (hechos >= 2) break;
                int i = Mathf.Clamp(Mathf.FloorToInt(path.Count * f), 1, path.Count - 1);
                var node = path[i];
                Vector3 dir = (node.Position - path[i - 1].Position).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

                bool puesto = false;
                foreach (float lado in new[] { -1f, 1f })
                {
                    if (puesto) break;
                    foreach (float dist in new[] { 18f, 26f, 34f })
                    {
                        Vector3 spot = node.Position + right * lado * dist;
                        if (Physics.CheckBox(spot + Vector3.up * 2.5f,
                            new Vector3(6.5f, 2f, 6.5f), Quaternion.identity)) continue;

                        CityDecorKit.Park(parent, spot, seed: 100 + hechos * 7);
                        hechos++;
                        puesto = true;
                        break;
                    }
                }
            }
            return hechos;
        }
    }
}
