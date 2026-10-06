using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using HablaCamaron.UI;
using HablaCamaron.World;
using static HablaCamaron.EditorTools.ToonCityKit;
using static HablaCamaron.EditorTools.RoadStripKit;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// FASE 1 — Genera la ZONA SUR (Quitumbe/Guamaní): un redondel central con
    /// cuatro brazos — el garaje del papá al sur, la cuesta al norte, y calles
    /// residenciales al este/oeste. Semáforos N/S vs E/O, señales como datos y
    /// el RoadGraph autogenerado junto con las calles (validado al final).
    /// Los tramos vienen de RoadStripKit (compartido con las otras zonas).
    /// Menú: Habla Camarón > 3 · Crear Zona Sur (Fase 1)
    /// </summary>
    public static class ZonaSurBuilder
    {
        private const string SceneP = "Assets/Scenes/N1_ZonaSur.unity";

        // Casas humildes para los brazos residenciales del barrio.
        private static readonly string[] CasasBarrio =
        {
            "Brownstone_1A", "Brownstone_2A", "Brownstone_3A",
            "Building_1A", "Building_2A", "Building_13A",
        };

        // El fondo del sur de Quito: casas medianas hasta donde alcanza la vista.
        // OJO (crash "level7 corrupted", 2026-07-14): SOLO prefabs ya probados
        // en las filas de casas (CasasBarrio). Con Brownstone_4A/5A y
        // Building_3A/5A en el skyline, el level del player quedaba ilegible
        // (bisección A/B con builds reales); sin skyline cargaba. Los levels
        // son FRÁGILES: no agregar prefabs nuevos aquí sin doble smoke test.
        private static readonly string[] CasasSkyline =
        {
            "Brownstone_1A", "Brownstone_2A", "Brownstone_3A",
            "Building_1A", "Building_2A", "Building_13A",
        };

        [MenuItem("Habla Camarón/3 · Crear Zona Sur (Fase 1)")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupAmbience(sunset: false); // el tutorial es de día (GDD)

            var roadFlat = Load($"{TC}/Roads/Road_1A.prefab");
            var roadSlope = Load($"{TC}/Roads/Road_1A_+2.prefab");
            var roundaboutPrefab = Load($"{TC}/Roads/Roundabout_2A.prefab") ??
                                   Load($"{TC}/Roads/Roundabout_1A.prefab");
            if (roadFlat == null || roadSlope == null || roundaboutPrefab == null)
            {
                EditorUtility.DisplayDialog("Habla Camarón",
                    "Faltan prefabs de Toon City (Road_1A / Road_1A_+2 / Roundabout).", "OK");
                return;
            }

            float roadYaw = MeasureSlopeYaw(roadSlope, out float slopeRise);
            var world = new GameObject("ZonaSur_ToonCity").transform;

            // ---- Redondel central ----
            var ringGO = (GameObject)PrefabUtility.InstantiatePrefab(roundaboutPrefab, world);
            // MÁS AMPLIO (playtest 2026-07-14: radio original 14 m = giro incómodo).
            // Solo XZ: las alturas de la calzada no cambian (la costura con los
            // brazos queda igual de baja y la cubre la rampa de entrada).
            // 1.9x (playtest): el redondel central es el OTRO punto de giro de
            // la misión 2, y a 1.4x el giro salía incómodo.
            ringGO.transform.localScale = new Vector3(1.9f, 1f, 1.9f);
            var rb = WorldBounds(ringGO);
            ringGO.transform.position += new Vector3(-rb.center.x, -rb.min.y, -rb.center.z);
            rb = WorldBounds(ringGO);
            float ringRadius = Mathf.Max(rb.extents.x, rb.extents.z);

            // ---- Cuatro brazos ----
            var north = BuildArm("Brazo_Norte_Cuesta", world, roadYaw,
                CuestaDesafiante(roadFlat, roadSlope, slopeRise));
            // Brazo sur MAS LARGO (mapa mas grande, mas recorrido antes de
            // volver): el garaje se mide de la geometria, asi que se reubica
            // solo al final del brazo alargado.
            var south = BuildArm("Brazo_Sur_Garaje", world, roadYaw,
                ArmModules(roadFlat, roadSlope, 7, 0, 0, slopeRise));
            var east = BuildArm("Brazo_Este", world, roadYaw,
                ArmModules(roadFlat, roadSlope, 3, 0, 0, slopeRise));
            var west = BuildArm("Brazo_Oeste", world, roadYaw,
                ArmModules(roadFlat, roadSlope, 3, 0, 0, slopeRise));

            // Decorar ANTES de rotar/ubicar (PlaceAligned es world-space).
            DecorateSouthArm(south);
            FillUnder(north, new Color(0.38f, 0.45f, 0.26f)); // colina con pasto seco
            DecorateHillTop(north);
            DecorateSideArm(east);
            DecorateSideArm(west);

            // ---- Pase de DISEÑO: el barrio se ve barrio (CityDecorKit) ----
            // Veredas en los brazos que no las tenían, casas en los brazos
            // residenciales y vida de acera (árboles, postes con cables,
            // bancas, basureros) en todos.
            float paveE = CityDecorKit.Sidewalks(east);
            float paveO = CityDecorKit.Sidewalks(west);
            CityDecorKit.Sidewalks(north); // solo el llano de la base
            CityDecorKit.BuildingRows(east, CasasBarrio, east.RoadWidth * 0.5f + paveE, seed: 31);
            CityDecorKit.BuildingRows(west, CasasBarrio, west.RoadWidth * 0.5f + paveO, seed: 32);
            CityDecorKit.SidewalkLife(south, south.RoadWidth * 0.5f + 2.2f, seed: 41);
            CityDecorKit.SidewalkLife(east, east.RoadWidth * 0.5f + 2.2f, seed: 42);
            CityDecorKit.SidewalkLife(west, west.RoadWidth * 0.5f + 2.2f, seed: 43);

            // Calles E/O CERRADAS (playtest 2026-07-14): la misión es ida y
            // vuelta por el redondel — casa atravesada + barriles + muro
            // invisible, y esos brazos NO entran al grafo (los NPC y la guía
            // de ruta jamás los pisan). Siguen decorados: barrio con fondo.
            BlockArmEntrance(east);
            BlockArmEntrance(west);

            PlaceArm(north, 0f, ringRadius * 0.92f);
            PlaceArm(south, 180f, ringRadius * 0.92f);
            PlaceArm(east, 90f, ringRadius * 0.92f);
            PlaceArm(west, -90f, ringRadius * 0.92f);

            // ---- Suavizado de superficie (fix playtest: labios y acantilados) ----
            AddSlopeSmoothColliders(north);
            AddEntrySeamRamp(north);
            AddEntrySeamRamp(south);

            // ---- Grafo: SOLO los brazos abiertos (norte y sur) ----
            var graphGO = new GameObject("[RoadGraph]");
            var graph = graphGO.AddComponent<RoadGraph>();
            var ring = BuildRingNodes(graph.Data, ringRadius * 0.62f);
            AddArmToGraph(graph.Data, north, ring);
            AddArmToGraph(graph.Data, south, ring);
            // La plaza de retorno de la cima ES CALLE: entra al grafo para que
            // dar la vuelta ahí no cuente como "salirse de la vía", la guía de
            // ruta la use y los muros del mundo la dejen dentro.
            AddCimaRetornoToGraph(graph.Data, north);
            VerificarCimaPisable(graph.Data);

            // ---- Semáforos N/S (grupo 0; los E/O se fueron con el cierre) ----
            var lightsGO = new GameObject("[TrafficLights]");
            var lights = lightsGO.AddComponent<TrafficLightController>();
            AddTrafficLight(lights, north, 0);
            AddTrafficLight(lights, south, 0);
            // El de MEDIA CUESTA (grupo 1, alterna con la base): parada
            // obligada en plena pendiente — freno de mano o rodar atrás.
            AddMidHillLight(lights, north);

            // ---- Señales: límite 30 en el sur ----
            AddSign(south, SignType.SpeedLimit30, "Streetsign_1A");

            // ---- Señalética ecuatoriana del barrio (Fase 4; ver EcuadorSignCatalog) ----
            PlaceEcuadorSigns(north, south);

            // ---- Piso, garaje del papá y Aveo ----
            // PISO GRANDE, no 60 (playtest: "al finalizar el redondel se ve en
            // azul"). El plano se centra en el origen, pero la cima de la
            // cuesta está en z≈144, así que desde ARRIBA —que es donde termina
            // el nivel 3— el borde del piso quedaba a 156 m de frente (medido
            // con SnapshotTool.DiagCimaNivel3) y por los huecos entre las casas
            // se veía el cielo a ras de suelo: el fin del mundo.
            // Un Plane primitivo mide 10×10 por unidad de escala, así que 200
            // son 2000×2000 (±1000 m): desde la cima el borde se va a ~856 m,
            // detrás del skyline. Es un solo objeto plano — no cuesta nada.
            BuildGround(world, new Vector3(0f, -0.03f, 0f), 200f,
                new Color(0.42f, 0.55f, 0.30f)); // césped andino (armoniza con Toon City)

            // El auto nace DENTRO del garaje (misión T1: sacarlo a la calle).
            float garageX = -south.RoadWidth * 0.22f;
            float garageZ = south.Length - 5f;
            // La ALTURA REAL de la vía se mide con raycast (los Road de Toon
            // City traen MeshCollider): asumir y local 0 enterraba el garaje
            // y dejaba al Aveo parado sobre el techo.
            Physics.SyncTransforms(); // los colliders recién creados aún no están en el motor de física
            Vector3 probe = south.Container.TransformPoint(new Vector3(garageX, 30f, garageZ));
            float floorLocalY = Physics.Raycast(probe, Vector3.down, out var floorHit, 100f)
                ? south.Container.InverseTransformPoint(floorHit.point).y : 0f;
            Debug.Log($"[Habla Camarón] Piso del garaje medido: y local {floorLocalY:0.00} " +
                      $"({(floorLocalY == 0f ? "raycast SIN impacto (fallback)" : floorHit.collider.name)})");
            BuildGarage(south, garageX, garageZ, floorLocalY);
            Vector3 spawnWorld = south.Container.TransformPoint(
                new Vector3(garageX, floorLocalY + 0.6f, garageZ));
            // El vano del garaje mira a −z LOCAL del brazo (hacia el redondel):
            // el yaw del Aveo se calcula de esa dirección real, no de un número
            // fijo (con yaw 0 el auto nacía de cara a la pared del fondo).
            Vector3 doorDir = south.Container.rotation * Vector3.back;
            float spawnYaw = Mathf.Atan2(doorDir.x, doorDir.z) * Mathf.Rad2Deg;
            BuildPlayerCar(GetOrCreateAveoSpec(), spawnWorld, spawnYaw);

            var ui = new GameObject("[GameplayUI]");
            ui.AddComponent<HUDController>();
            ui.AddComponent<PauseController>();
            ui.AddComponent<DonPanchoDialogue>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            // ---- Validación del grafo (criterio de salida de la fase) ----
            var problems = graph.Data.Validate();
            var spawnNode = graph.Data.NearestNode(spawnWorld);
            var hillTop = north.Outbound[north.Outbound.Count - 1];
            bool reachable = graph.Data.IsReachable(spawnNode.Id, hillTop.Id);

            BuildWorldLimits(graph.Data); // muros invisibles: nadie se va del barrio
            // El juego pasa EN LA CALLE: costados de los 4 brazos cerrados.
            AddSideWalls(south); AddSideWalls(north); AddSideWalls(east); AddSideWalls(west);

            // ---- Escenografía (más allá de los muros: solo se VE) ----
            CityDecorKit.RoundaboutMonument(world, ringGO.transform, ringRadius, seed: 7);
            Vector3 mn = graph.Data.Nodes[0].Position, mx = mn;
            foreach (var n in graph.Data.Nodes)
            { mn = Vector3.Min(mn, n.Position); mx = Vector3.Max(mx, n.Position); }
            CityDecorKit.Skyline(world, mn, mx, CasasSkyline, seed: 8);
            CityDecorKit.Clouds(world, seed: 9); // día andino con nubes toon

            // El PARQUE del barrio (assets del paquete: fuente, árboles,
            // bancas) en el cuadrante noreste, entre los brazos.
            CityDecorKit.Park(world, new Vector3(ringRadius + 9f, 0f, ringRadius + 9f),
                seed: 21, radius: 7f);

            // Nada flotando (playtest: edificios voladores) — pase final.
            int asentados = CityDecorKit.ReGround(world);
            if (asentados > 0)
                Debug.Log($"[Habla Camarón] Zona Sur: {asentados} objetos re-asentados al piso.");
            TrafficInjectionTool.InjectIntoOpenScene(SceneP); // NPCs con modelo, no cubos
            EditorSceneManager.SaveScene(scene, SceneP);
            AddToBuildSettings(SceneP);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Habla Camarón — Fase 1",
                $"Zona Sur creada.\n\n" +
                $"Grafo: {graph.Data.Nodes.Count} nodos, {graph.Data.Edges.Count} aristas.\n" +
                $"Problemas de validación: {(problems.Count == 0 ? "ninguno ✓" : string.Join("\n", problems))}\n" +
                $"Ruta garaje → cima de la cuesta: {(reachable ? "alcanzable ✓" : "¡NO ALCANZABLE!")}\n\n" +
                "Los nodos y flechas del grafo se ven en la ventana Scene (gizmos).", "¡A rodar!");

            if (problems.Count > 0 || !reachable)
                Debug.LogError("[Habla Camarón] El grafo de Zona Sur tiene problemas: revisar reporte.");
        }

        // ---- Señales ecuatorianas del barrio (kit de la Fase 4). Convención de
        //      tramos: carril de ida = +x avanzando hacia +z (señal con yaw 0 a
        //      su derecha); carril de vuelta = −x hacia −z (señal con yaw 180). ----
        private static void PlaceEcuadorSigns(Arm north, Arm south)
        {
            float sEdge = south.RoadWidth * 0.5f + 0.9f;
            // El jugador sale del garaje hacia el redondel (carril −x, yendo a −z).
            EcuadorSignKit.Place("zona_escolar", south.Container, new Vector3(-sEdge, 0f, south.Length - 12f), 180f);
            EcuadorSignKit.Place("peatones", south.Container, new Vector3(-sEdge, 0f, south.Length - 26f), 180f);
            EcuadorSignKit.Place("redondel", south.Container, new Vector3(-sEdge, 0f, 8f), 180f);
            // Carril de regreso al barrio (+x, hacia +z).
            EcuadorSignKit.Place("no_pitar", south.Container, new Vector3(sEdge, 0f, 14f), 0f);
            EcuadorSignKit.Place("parada_trole", south.Container, new Vector3(sEdge, 0f, south.Length - 20f), 0f);
            // Antes vivían en los brazos E/O; con las calles cerradas se mudan
            // al brazo sur (la cuenta de señales de la zona no baja).
            EcuadorSignKit.Place("parada_bus", south.Container, new Vector3(sEdge, 0f, south.Length - 38f), 0f);
            EcuadorSignKit.Place("ninos", south.Container, new Vector3(-sEdge, 0f, south.Length - 18f), 180f);

            // La cuesta: prevenir antes de subir. Los módulos medidos dicen
            // hasta dónde llega el llano (nada de z a ojo sobre la pendiente).
            float nEdge = north.RoadWidth * 0.5f + 0.9f;
            float llano = north.Modules[0].Z1; // fin del primer módulo plano
            EcuadorSignKit.Place("resalto", north.Container, new Vector3(nEdge, 0f, llano * 0.35f), 0f);
            EcuadorSignKit.Place("curva", north.Container, new Vector3(nEdge, 0f, llano * 0.85f), 0f);
        }

        // ---- La cuesta del NIVEL 3, más brava (playtest 2026-07-14): cuatro
        //      rampas cada vez más empinadas — la última ("la pared", ~16°,
        //      rise·1.12) mata a la 2ª y OBLIGA la 1ª. El tope NO es capricho:
        //      el Aveo es tracción DELANTERA y cuesta arriba el peso se va
        //      atrás — medido con telemetría (CuestaEmpinadaTests): a 18° las
        //      ruedas patinan a fondo y el auto resbala; a 16° la 1ª sube con
        //      acelerador dosificado y la 2ª (máx ~2.97 kN) ya no puede con
        //      los ~2.93 kN que pide la pendiente + rodadura. Ese ángulo va
        //      DE LA MANO con el test PlayMode. ----
        private static System.Collections.Generic.IEnumerable<(GameObject, float)>
            CuestaDesafiante(GameObject flat, GameObject slope, float rise)
        {
            yield return (flat, 0f);
            yield return (flat, 0f);
            yield return (slope, rise);          // arranque amable (la de siempre)
            yield return (slope, rise * 1.04f);  // aprieta
            yield return (slope, rise * 1.08f);  // la 2ª empieza a sufrir
            yield return (slope, rise * 1.12f);  // la pared: freno de mano y 1ª
            yield return (flat, 0f);
            yield return (flat, 0f);
        }

        // ---- Semáforo de entrada (construido por código, a la derecha del que
        //      llega; los focos van EN ALTO — ver TrafficLightKit) ----
        private static void AddTrafficLight(TrafficLightController lights, Arm arm, int group)
        {
            arm.Inbound[0].TrafficLightGroup = group;
            TrafficLightKit.Place(arm.Container,
                new Vector3(-(arm.RoadWidth * 0.5f + 0.6f), 0f, 2.5f), 0f, lights, group);
            // El PASO CEBRA del cruce (funcional: bloquearlo en rojo descuenta).
            CrosswalkKit.Place(arm.Container,
                arm.Container.TransformPoint(new Vector3(0f, 0f, 4.8f)),
                arm.Container.eulerAngles.y, arm.RoadWidth, group);
        }

        // ---- Semáforo a MEDIA CUESTA (grupo 1): parada obligada en plena
        //      pendiente, la pedagogía del freno de mano hecha señal. Va en la
        //      PRIMERA rampa (~14.5°, el arranque en pendiente que los
        //      playtests humanos ya probaron posible): parar ahí es exigente
        //      pero justo. La pared final (~16°) NO tiene semáforo — esa se
        //      pasa entrando con impulso en 1ª (parado ahí no arranca nadie:
        //      la tracción delantera no da, medido con telemetría). ----
        private static void AddMidHillLight(TrafficLightController lights, Arm arm)
        {
            int idx = -1;
            for (int i = 0; i < arm.Modules.Count; i++)
                if (Mathf.Abs(arm.Modules[i].Rise) > 0.05f) { idx = i; break; }
            if (idx < 0) return; // brazo sin cuesta: nada que hacer

            var m = arm.Modules[idx];
            float z = (m.Z0 + m.Z1) * 0.5f;
            float y = m.Y0 + m.Rise * 0.5f;
            arm.Outbound[idx].TrafficLightGroup = 1;
            TrafficLightKit.Place(arm.Container,
                new Vector3(arm.RoadWidth * 0.5f + 0.6f, y, z), 0f, lights, 1);
        }

        // ---- Cierre TOTAL de un brazo residencial (playtest 2026-07-14):
        //      casa atravesada + barriles + muro invisible. El brazo no entra
        //      al grafo: para jugador e IA la calle simplemente no continúa. ----
        private static void BlockArmEntrance(Arm arm)
        {
            var house = Load($"{TC}/Buildings/Building_2A.prefab");
            if (house != null)
                CityDecorKit.PlaceAt(house, arm.Container, new Vector3(0f, 0f, 7f), 90f);

            var barrel = Load($"{TC}/Roads/Impact_Barrel_1A.prefab");
            if (barrel != null)
                for (int i = 0; i < 4; i++)
                {
                    float x = Mathf.Lerp(-arm.RoadWidth * 0.42f, arm.RoadWidth * 0.42f, i / 3f);
                    PlaceAligned(barrel, arm.Container, 0f, 2.2f, 0f, x);
                }

            // El muro que garantiza el cierre (la casa podría dejar rendijas).
            var wall = new GameObject("Cierre_Calle");
            wall.transform.SetParent(arm.Container, false);
            wall.transform.localPosition = new Vector3(0f, 4f, 5f);
            wall.AddComponent<BoxCollider>().size = new Vector3(arm.RoadWidth + 6f, 8f, 0.8f);
            wall.AddComponent<WorldBoundary>();
        }

        private static void AddSign(Arm arm, SignType type, string prefabName)
        {
            var node = arm.Inbound[0];
            node.Sign = type;
            var prefab = Load($"{TC}/Roads/{prefabName}.prefab");
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, arm.Container);
            go.transform.localPosition = new Vector3(-(arm.RoadWidth * 0.5f + 0.6f), 0f, 6f);
            var sign = go.AddComponent<RoadSign>();
            sign.Type = type;
            sign.AffectedNodeIds = new[] { node.Id };
        }

        // ---- Barrio del garaje: veredas, casas y el parqueadero del papá ----
        private static void DecorateSouthArm(Arm arm)
        {
            float half = arm.RoadWidth * 0.5f;
            var pave = Load($"{TC}/Pavement/Pavement_1A_2x2.prefab");
            float paveW = 3f;
            if (pave != null)
            {
                var probe = PlaceAligned(pave, arm.Container, 0f, -100f, 0f, 0f);
                float tile = probe.size.z;
                paveW = probe.size.x;
                Object.DestroyImmediate(arm.Container.GetChild(arm.Container.childCount - 1).gameObject);
                foreach (float s in new[] { -1f, 1f })
                    for (float z = 2f; z < arm.Length - tile * 0.5f; z += tile)
                        PlaceAligned(pave, arm.Container, 0f, z, 0f, s * (half + paveW * 0.5f));
            }

            string[] names = { "Building_1A", "Building_2A", "Brownstone_1A", "Building_13A" };
            var rng = new System.Random(7);
            foreach (float s in new[] { -1f, 1f })
            {
                float z = 2f;
                while (z < arm.Length - 4f)
                {
                    var prefab = Load($"{TC}/Buildings/{names[rng.Next(names.Length)]}.prefab");
                    if (prefab == null) break;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, arm.Container);
                    go.transform.localRotation = Quaternion.Euler(0f, s > 0 ? -90f : 90f, 0f);
                    var b = WorldBounds(go);
                    float x = s * (half + paveW + b.size.x * 0.5f + 0.4f);
                    go.transform.position += new Vector3(x - b.center.x, -b.min.y, 0f);
                    var lp = go.transform.localPosition;
                    go.transform.localPosition = new Vector3(lp.x, lp.y, z + b.size.z * 0.5f);
                    z += b.size.z + 1.2f;
                }
            }
        }

        // ---- Barriles cerrando la cima de la cuesta ----
        /// <summary>
        /// LA CIMA CON REDONDEL DE RETORNO (playtest 2026-07-24: "me pide dar
        /// la vuelta en un lugar cerrado y ahí no se puede"). El brazo norte
        /// terminaba en un MURO DE BARRILES: para armar la baliza del nivel 2
        /// hay que alejarse 60 m, lo que obliga a subir hasta aquí… y no había
        /// dónde girar. Ahora la cima es una PLAZA CIRCULAR de retorno (un
        /// redondel de verdad): calzada anular con isla al centro, apoyada a la
        /// altura real de la cima, con su plataforma debajo para que no flote.
        /// Los barriles pasan a cerrar SOLO el borde exterior.
        /// </summary>
        /// <summary>Geometría de la plaza de retorno (la usa el grafo).</summary>
        private static float _cimaZ, _cimaRadio, _cimaY;

        /// <summary>
        /// Altura REAL del asfalto de un brazo en una z local, por raycast.
        /// Los cuatro brazos se decoran APILADOS EN EL ORIGEN (PlaceArm viene
        /// después), así que el rayo se filtra a los colliders de ESTE brazo:
        /// si no, mediría la calle de otro brazo que pasa por el mismo sitio.
        /// Devuelve `fallback` si no encuentra nada.
        /// </summary>
        private static float SuperficieDelBrazo(Arm arm, float zLocal, float fallback)
        {
            Physics.SyncTransforms(); // los colliders recién creados aún no están en física
            Vector3 origen = arm.Container.TransformPoint(new Vector3(0f, fallback + 60f, zLocal));
            float mejor = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(origen, Vector3.down, 200f))
            {
                if (!h.collider.transform.IsChildOf(arm.Container)) continue;
                // SOLO el asfalto. Quedarse con "la superficie más alta del
                // brazo" hacía que un ÁRBOL o una valla de la decoración
                // ganaran el rayo: la plaza de la cima acabó **10.9 m en el
                // aire** (medido: vía a 8.58, calzada a 19.36) y el nivel 3 se
                // volvió imposible de terminar — el jugador llegaba al pie de
                // un escalón de 14 m. Es el mismo gotcha del árbol que ya
                // estaba documentado, en otro sitio.
                if (!EsPiezaDeVia(h.collider.transform, arm.Container)) continue;
                if (h.point.y > mejor) mejor = h.point.y;
            }
            if (float.IsNegativeInfinity(mejor)) return fallback;
            return arm.Container.InverseTransformPoint(new Vector3(0f, mejor, 0f)).y;
        }

        /// <summary>¿Este collider es asfalto del brazo (y no decoración)? Se
        /// mira la cadena de padres hasta el contenedor buscando una pieza de
        /// vía de Toon City.</summary>
        private static bool EsPiezaDeVia(Transform t, Transform container)
        {
            for (var p = t; p != null && p != container; p = p.parent)
                if (p.name.StartsWith("Road_") || p.name.StartsWith("Highway_") ||
                    p.name.StartsWith("Roundabout_"))
                    return true;
            return false;
        }

        /// <summary>
        /// Mete la plaza de retorno de la cima al grafo como un REDONDEL: un
        /// anillo de nodos antihorario (mano derecha) sobre la calzada, con
        /// entrada desde el carril de subida y salida al de bajada. Así el
        /// jugador da la vuelta rodando siempre "en calle" (el juez de fuera
        /// de vía no lo castiga) y la guía de ruta lo lleva por el giro.
        /// Llamar DESPUÉS de PlaceArm y de AddArmToGraph del brazo.
        /// </summary>
        private static void AddCimaRetornoToGraph(RoadGraphData g, Arm arm)
        {
            if (_cimaRadio <= 0f) return;
            const int N = 8;
            float rNodos = _cimaRadio * 0.74f; // línea media de la calzada anular

            var aro = new System.Collections.Generic.List<RoadNode>();
            for (int i = 0; i < N; i++)
            {
                // (cos, sin) — EL MISMO patrón que RoadStripKit.BuildRingNodes.
                // Antes decía (sin, cos), que recorre la misma circunferencia
                // AL REVÉS: el anillo quedaba en sentido HORARIO y, como la
                // guía dorada del asfalto y el minimapa se pintan sobre la ruta
                // A*, mandaban al jugador a rodear el redondel por el lado
                // contrario al del tránsito real (playtest 2026-07-30, con
                // foto). El comentario de esta línea ya decía "antihorario":
                // por eso no se cazó leyendo. Ahora hay un invariante que lo
                // MIDE, abajo.
                float a = i / (float)N * Mathf.PI * 2f;
                Vector3 local = new Vector3(Mathf.Cos(a) * rNodos, _cimaY,
                                            _cimaZ + Mathf.Sin(a) * rNodos);
                aro.Add(g.AddNode(arm.Container.TransformPoint(local)));
            }
            // Ciclo antihorario visto desde arriba (tránsito por la derecha).
            for (int i = 0; i < N; i++)
                g.Connect(aro[i].Id, aro[(i + 1) % N].Id);

            // INVARIANTE: medir el sentido en COORDENADAS DE MUNDO, que es
            // donde circula el jugador. El brazo norte está rotado, así que
            // comprobarlo en local no probaría lo que importa.
            var enMundo = new System.Collections.Generic.List<Vector3>(N);
            foreach (var n in aro) enMundo.Add(n.Position);
            if (!RingOrientation.EsAntihorario(enMundo))
                Debug.LogError("[Habla Camarón] Zona Sur: el redondel de la cima gira al REVÉS " +
                               "(horario). En tránsito por la derecha se circula antihorario, y la " +
                               "guía de ruta manda por donde diga el grafo: el nivel 3 enseñaría a " +
                               "rodearlo mal.");

            // Entrada: del final del carril de subida al nodo más cercano del
            // aro. Salida: del aro al final del carril de bajada.
            var subida = arm.Outbound[arm.Outbound.Count - 1];
            var bajada = arm.Inbound[arm.Inbound.Count - 1];
            g.Connect(subida.Id, Nearest(aro, subida.Position).Id);
            g.Connect(Nearest(aro, bajada.Position).Id, bajada.Id);
        }

        /// <summary>
        /// INVARIANTE de la cima: cada nodo del grafo tiene que estar SOBRE el
        /// asfalto, no debajo. La plaza de retorno llegó a quedar 10.9 m en el
        /// aire y el nivel 3 se volvió imposible — el jugador se plantaba al pie
        /// de un escalón de 14 m. Esto se descubrió jugando, no en los tests
        /// (el piloto automático mueve el coche por encima de la geometría y no
        /// lo nota), así que el chequeo va aquí, en el builder.
        /// </summary>
        private static void VerificarCimaPisable(RoadGraphData g)
        {
            Physics.SyncTransforms();
            float peor = 0f;
            Vector3 dondePeor = Vector3.zero;
            string quien = "";
            foreach (var n in g.Nodes)
            {
                // El asfalto BAJO el nodo, ignorando la decoración que pase por
                // encima (un árbol sobre la isla miente por 12 m — gotcha ya
                // conocido). Se busca el hit más alto que NO sea decoración.
                float suelo = float.NegativeInfinity;
                string col = "";
                foreach (var h in Physics.RaycastAll(n.Position + Vector3.up * 40f, Vector3.down,
                                                     140f, Physics.DefaultRaycastLayers,
                                                     QueryTriggerInteraction.Ignore))
                {
                    string nom = h.collider.name;
                    if (nom.StartsWith("Tree") || nom.StartsWith("Bush") ||
                        nom.StartsWith("Barril") || nom.Contains("Valla") ||
                        nom.Contains("Poste") || nom.Contains("Bordillo")) continue;
                    if (h.point.y > suelo) { suelo = h.point.y; col = nom; }
                }
                if (float.IsNegativeInfinity(suelo)) continue;

                float escalon = Mathf.Abs(suelo - n.Position.y);
                if (escalon > peor) { peor = escalon; dondePeor = n.Position; quien = col; }
            }
            if (peor > 1.5f)
                Debug.LogError($"[Habla Camarón] Zona Sur: hay un nodo del grafo a {peor:0.0} m " +
                                $"del suelo real (en {dondePeor}, collider '{quien}'). La calzada " +
                                "y el grafo no coinciden: el nivel puede quedar intransitable.");
            else
                Debug.Log($"[Habla Camarón] Grafo sobre el asfalto ✓ (peor desfase {peor:0.00} m).");
        }

        /// <summary>
        /// Cambia el collider de un cilindro APLASTADO por uno de malla.
        ///
        /// EL BUG QUE ESTO ARREGLA (playtest 2026-07-25, con foto): un
        /// `CreatePrimitive(Cylinder)` trae un **CapsuleCollider**, y una
        /// cápsula NO se puede aplastar — Unity la deja como una ESFERA de
        /// radio = medio ancho. La calzada de la plaza de la cima, escalada a
        /// (32.4, 0.12, 32.4), se veía como un disco plano pero por dentro era
        /// una **bola invisible de 16.2 m de radio**: el jugador subía por su
        /// ladera y quedaba clavado a 41 m de la meta, con el nivel 3
        /// imposible de terminar. La cuenta cuadra al centímetro — a 12 m del
        /// centro la esfera levanta 8.46+√(16.2²−12²) = 19.34 m, y ahí medía
        /// el suelo 19.36.
        /// Regla: TODO cilindro chato que sirva de suelo lleva MeshCollider.
        /// </summary>
        private static void ColliderPlano(GameObject go)
        {
            var viejo = go.GetComponent<Collider>();
            if (viejo != null) Object.DestroyImmediate(viejo);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }

        /// <summary>
        /// Una marca de pintura sobre la calzada: caja chata, sin collider (no
        /// se choca contra la pintura) y a 2 cm del asfalto para no hacer
        /// z-fighting con él.
        /// </summary>
        private static void Pintura(Transform padre, Vector3 pos, float yaw, Vector3 escala, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Marca";
            go.transform.SetParent(padre, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = escala;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        /// <summary>
        /// Pinta el redondel de la cima para que SE LEA como un redondel:
        /// el borde exterior de la calzada anular en línea discontinua, y
        /// CHEVRONES que enseñan hacia qué lado se rodea.
        ///
        /// Los chevrones apuntan en el sentido de circulación real (antihorario,
        /// tránsito por la derecha) y se calculan del MISMO ángulo que los nodos
        /// del grafo, así que si alguien vuelve a espejar el anillo, la pintura
        /// se espeja con él y el error se ve a simple vista en vez de esconderse
        /// entre la geometría.
        /// </summary>
        private static void PintarAnilloDeGiro(Transform plaza, float radio)
        {
            var blanco = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.90f, 0.89f, 0.83f) };

            // Línea discontinua del borde exterior de la calzada.
            const int Trazos = 28;
            float rBorde = radio * 0.93f;
            for (int i = 0; i < Trazos; i++)
            {
                float a = i / (float)Trazos * Mathf.PI * 2f;
                // Tangente al círculo en ese punto: la marca va "peinada" con
                // la curva, no radial.
                float yaw = -a * Mathf.Rad2Deg;
                Pintura(plaza,
                        new Vector3(Mathf.Cos(a) * rBorde, 0.02f, Mathf.Sin(a) * rBorde),
                        yaw, new Vector3(0.22f, 0.03f, radio * 0.14f), blanco);
            }

            // Chevrones de sentido, sobre la línea media de la calzada (la
            // misma que siguen los nodos del grafo: rNodos = radio · 0.74).
            const int Flechas = 6;
            float rMedio = radio * 0.74f;
            for (int i = 0; i < Flechas; i++)
            {
                float a = i / (float)Flechas * Mathf.PI * 2f;
                Vector3 centro = new Vector3(Mathf.Cos(a) * rMedio, 0.02f, Mathf.Sin(a) * rMedio);

                // Sentido de avance ANTIHORARIO = derivada de (cos a, sin a),
                // que es (-sin a, cos a). Se saca del mismo ángulo que el nodo,
                // no de una constante, para que no puedan desincronizarse.
                Vector3 avance = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                float yaw = Mathf.Atan2(avance.x, avance.z) * Mathf.Rad2Deg;

                // Punta del chevrón y dos brazos hacia atrás formando ">".
                Vector3 punta = centro + avance * 1.1f;
                const float largoBrazo = 2.1f;
                foreach (float lado in new[] { 145f, -145f })
                {
                    float yawBrazo = yaw + lado;
                    Vector3 dir = Quaternion.Euler(0f, yawBrazo, 0f) * Vector3.forward;
                    Pintura(plaza, punta + dir * (largoBrazo * 0.5f), yawBrazo,
                            new Vector3(0.34f, 0.03f, largoBrazo), blanco);
                }
            }
        }

        private static void DecorateHillTop(Arm arm)
        {
            // Radio cómodo para que el Aveo (5.6 m) dé la vuelta sin maniobrar.
            float radio = Mathf.Max(arm.RoadWidth * 1.35f, 15f);
            // La plaza arranca justo donde termina el último tramo del brazo.
            float zCentro = arm.Length + radio;
            _cimaZ = zCentro;
            _cimaRadio = radio;

            // ALTURA REAL de la cima, medida por raycast sobre el asfalto de
            // ESTE brazo (mismo patrón que el piso del garaje). `arm.EndY` es
            // la altura TEÓRICA acumulada y NO coincide con la superficie: al
            // usarla, la plaza quedaba 11.67 m en el aire (medido). Ojo: los
            // cuatro brazos se decoran apilados en el origen, así que el rayo
            // se filtra a los colliders de este brazo.
            float yCima = SuperficieDelBrazo(arm, arm.Length - 2f, arm.EndY);
            _cimaY = yCima;

            var plaza = new GameObject("Cima_RedondelRetorno").transform;
            plaza.SetParent(arm.Container, false);
            plaza.localPosition = new Vector3(0f, yCima, zCentro);

            var asfalto = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.30f, 0.31f, 0.33f) };
            var isla = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.42f, 0.52f, 0.30f) }; // pasto de la isla
            var bordillo = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.78f, 0.76f, 0.70f) };

            // Calzada: disco de asfalto (cilindro chato) CON collider — es la
            // superficie por la que se rueda al dar la vuelta.
            // OJO: el cilindro primitivo mide 2 unidades de alto (±1), así que
            // con scale.y = 0.12 su cara superior queda 0.12 sobre el centro:
            // se baja otro tanto para que la calzada quede EXACTA al nivel de
            // la vía y no haya escalón al entrar.
            var calzada = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            calzada.name = "Calzada";
            calzada.transform.SetParent(plaza, false);
            calzada.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            calzada.transform.localScale = new Vector3(radio * 2f, 0.12f, radio * 2f);
            calzada.GetComponent<Renderer>().sharedMaterial = asfalto;
            ColliderPlano(calzada);

            // Isla central (con bordillo): da la lectura de redondel y obliga
            // a rodearla, que es justo el gesto de "dar la vuelta".
            var anillo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            anillo.name = "Bordillo_Isla";
            anillo.transform.SetParent(plaza, false);
            anillo.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            anillo.transform.localScale = new Vector3(radio * 0.62f, 0.16f, radio * 0.62f);
            anillo.GetComponent<Renderer>().sharedMaterial = bordillo;
            ColliderPlano(anillo);

            var centro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            centro.name = "Isla";
            centro.transform.SetParent(plaza, false);
            centro.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            centro.transform.localScale = new Vector3(radio * 0.52f, 0.22f, radio * 0.52f);
            centro.GetComponent<Renderer>().sharedMaterial = isla;
            ColliderPlano(centro);

            // LA LOMA bajo la plaza. Antes era UN cilindro recto y la cima se
            // veía como una tarta gris flotando sobre el pasto (playtest: "está
            // feo el checkpoint del nivel 3"). Ahora son varios anillos que se
            // ensanchan hacia abajo: leen como la ladera de un cerro, que es lo
            // que se ha subido para llegar hasta aquí.
            var pasto = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.38f, 0.47f, 0.26f) };
            // OJO con las alturas: un cilindro primitivo mide 2 de alto, así que
            // su cara superior queda en localPosition.y + escala.y. La primera
            // versión de esto puso las faldas POR ENCIMA del asfalto y el pasto
            // tapaba la calzada entera. Cada falda arranca justo bajo el disco
            // y baja hasta el pie de la loma.
            float pie = -Mathf.Max(yCima, 0.6f) - 0.5f;    // fondo de la loma
            const int Faldas = 4;
            for (int i = 0; i < Faldas; i++)
            {
                float t = i / (float)(Faldas - 1);         // 0 = pegada al disco
                float rAnillo = radio * Mathf.Lerp(2.04f, 3.1f, t);
                float caraSup = -0.16f - t * Mathf.Max(yCima, 0.6f) * 0.55f;
                float mitad = Mathf.Max((caraSup - pie) * 0.5f, 0.15f);

                var falda = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                falda.name = i == 0 ? "Base_Cima" : $"Falda_Cima_{i}";
                falda.transform.SetParent(plaza, false);
                falda.transform.localPosition = new Vector3(0f, caraSup - mitad, 0f);
                falda.transform.localScale = new Vector3(rAnillo, mitad, rAnillo);
                falda.GetComponent<Renderer>().sharedMaterial = pasto;
                Object.DestroyImmediate(falda.GetComponent<Collider>()); // relleno visual
            }

            // Vereda perimetral: remata el disco y le da lectura de plaza
            // urbana en vez de plataforma suelta.
            var vereda = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vereda.name = "Vereda_Cima";
            vereda.transform.SetParent(plaza, false);
            // Cara superior 3 cm POR DEBAJO del asfalto: así solo asoma el
            // anillo de fuera. Puesta más arriba, este disco (que es más ancho
            // que la calzada) tapaba el asfalto entero y la plaza parecía de
            // cemento.
            vereda.transform.localPosition = new Vector3(0f, -0.13f, 0f);
            vereda.transform.localScale = new Vector3(radio * 2.16f, 0.10f, radio * 2.16f);
            vereda.GetComponent<Renderer>().sharedMaterial = bordillo;
            ColliderPlano(vereda);

            // SEÑALIZACIÓN HORIZONTAL. Sin ella la plaza se lee como una
            // explanada de asfalto y no como un redondel (playtest: "no sigue
            // la estructura"): no hay nada que diga dónde está la calzada
            // anular ni hacia qué lado se rodea. Va DESPUÉS de la vereda y las
            // faldas para que nada la tape.
            PintarAnilloDeGiro(plaza, radio);

            // Un árbol FRONDOSO en la isla (el Tree_1A del paquete está pelado
            // y en la cima quedaba como un palo seco), con arbustos al pie.
            var arbol = Load($"{TC}/Vegetation/Tree_2D.prefab") ??
                        Load($"{TC}/Vegetation/Tree_1A.prefab");
            if (arbol != null)
                PlaceAligned(arbol, arm.Container, 0f, zCentro, yCima + 0.44f, 0f);
            var arbusto = Load($"{TC}/Vegetation/Bush_1A.prefab");
            if (arbusto != null)
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.PI * 2f;
                    PlaceAligned(arbusto, arm.Container, 0f,
                                 zCentro + Mathf.Cos(a) * radio * 0.2f,
                                 yCima + 0.44f, Mathf.Sin(a) * radio * 0.2f);
                }

            // Farolas en el anillo: la plaza deja de parecer una maqueta.
            var farola = Load($"{TC}/Roads/Streetlight_1A.prefab");
            if (farola != null)
                for (int i = 0; i < 5; i++)
                {
                    // Sobre la VEREDA, y SOLO en el arco lejano — el mismo
                    // criterio que los barriles. Repartidas en los 360° una
                    // caía en la boca de entrada, justo sobre la ruta.
                    float a = Mathf.Lerp(-64f, 64f, i / 4f) * Mathf.Deg2Rad;
                    float x = Mathf.Sin(a) * radio * 1.05f;
                    float z = zCentro + Mathf.Cos(a) * radio * 1.05f;
                    PlaceAligned(farola, arm.Container, a * Mathf.Rad2Deg, z, yCima, x);
                }

            // Los barriles ya NO cierran el paso: rodean el BORDE EXTERIOR de
            // la plaza (se ve el fin del mundo, pero se puede girar).
            var barrel = Load($"{TC}/Roads/Impact_Barrel_1A.prefab");
            if (barrel == null) return;
            for (int i = 0; i < 9; i++)
            {
                // Solo el arco lejano (el cercano es por donde se entra).
                float a = Mathf.Lerp(-70f, 70f, i / 8f) * Mathf.Deg2Rad;
                float x = Mathf.Sin(a) * (radio * 0.94f);
                float z = zCentro + Mathf.Cos(a) * (radio * 0.94f);
                PlaceAligned(barrel, arm.Container, 0f, z, yCima, x);
            }
        }

        private static void DecorateSideArm(Arm arm)
        {
            var light1 = Load($"{TC}/Roads/Streetlight_1A.prefab");
            var bush = Load($"{TC}/Vegetation/Bush_1A.prefab");
            float edge = arm.RoadWidth * 0.5f + 1.2f;
            if (light1 != null)
                for (float z = 5f; z < arm.Length; z += 16f)
                    PlaceAligned(light1, arm.Container, 90f, z, 0f, edge);
            if (bush != null)
                for (float z = 10f; z < arm.Length; z += 14f)
                    PlaceAligned(bush, arm.Container, 0f, z, 0f, -edge);
        }

        // ---- El garaje del papá: caja con 3 paredes y techo, abierta hacia el
        //      redondel (−z local). El Aveo nace adentro y hay que SACARLO (T1). ----
        private static void BuildGarage(Arm arm, float localX, float localZ, float localY = 0f)
        {
            // A la medida del Car_2D REAL (5.6 de largo × 2.7 de ancho × 3.9 de
            // alto): con la caseta original de 4.6×7×3 el auto nacía incrustado
            // en el techo y la física lo hacía girar sin poder salir.
            const float w = 6f, depth = 9f, h = 5f, t = 0.2f;

            var wall = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.86f, 0.80f, 0.66f) };   // pared crema quiteña
            var roof = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.55f, 0.22f, 0.15f) };   // teja terracota
            var floor = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.34f, 0.34f, 0.36f) };   // cemento

            var g = new GameObject("Garaje_Papa").transform;
            g.SetParent(arm.Container, false);
            g.localPosition = new Vector3(localX, localY, localZ);

            // Piso de cemento (sin collider: debajo ya está la vía).
            Cube(g, "Piso", floor, new Vector3(0f, 0.03f, 0f), new Vector3(w, 0.06f, depth), false);
            // Pared del fondo (+z, lado del vecindario), laterales y techo: CON collider.
            Cube(g, "Pared_Fondo", wall, new Vector3(0f, h * 0.5f, depth * 0.5f - t * 0.5f),
                new Vector3(w, h, t), true);
            Cube(g, "Pared_Izq", wall, new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, 0f),
                new Vector3(t, h, depth), true);
            Cube(g, "Pared_Der", wall, new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, 0f),
                new Vector3(t, h, depth), true);
            Cube(g, "Techo", roof, new Vector3(0f, h + t * 0.5f, 0f),
                new Vector3(w + 0.3f, t, depth + 0.3f), true);
            // Dintel sobre la puerta abierta (frente, −z) para que se lea el vano.
            Cube(g, "Dintel", wall, new Vector3(0f, h - 0.35f, -depth * 0.5f + t * 0.5f),
                new Vector3(w, 0.7f, t), true);
        }

        private static void Cube(Transform parent, string name, Material mat,
            Vector3 localPos, Vector3 size, bool collide)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
