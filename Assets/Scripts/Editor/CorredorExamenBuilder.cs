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
    /// FASE 1 — CORREDOR DEL EXAMEN FINAL: la ruta de Guamaní a La Carolina AL
    /// ATARDECER (la hora narrativa del examen). Sur residencial → redondel con
    /// semáforos → cuesta → La Carolina moderna con parque, y la META bajo un
    /// arco (el objeto [MetaExamen] es el ancla del trigger de misión en Fase 3).
    /// Menú: Habla Camarón > 5 · Crear Corredor del Examen (Fase 1)
    /// </summary>
    public static class CorredorExamenBuilder
    {
        private const string SceneP = "Assets/Scenes/N1_CorredorExamen.unity";

        // Calles transversales: mezcla de barrio y comercio.
        private static readonly string[] CasasTransversales =
        {
            "Brownstone_2A", "Building_2A", "Building_13A",
            "Building_4A", "Brownstone_5A", "Building_1A",
        };

        // El fondo de la ciudad grande: torres modernas hacia La Carolina.
        private static readonly string[] TorresSkyline =
        {
            "Building_10A", "Building_11A", "Building_12A", "Building_14A",
            "Building_15A", "Building_16A", "Building_17A", "Building_18A",
        };

        [MenuItem("Habla Camarón/5 · Crear Corredor del Examen (Fase 1)")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupAmbience(sunset: true); // el examen se corre al atardecer (GDD)

            var flat = Load($"{TC}/Roads/Road_1A.prefab");
            var slope = Load($"{TC}/Roads/Road_1A_+2.prefab");
            var roundaboutPrefab = Load($"{TC}/Roads/Roundabout_2A.prefab") ??
                                   Load($"{TC}/Roads/Roundabout_1A.prefab");
            if (flat == null || slope == null || roundaboutPrefab == null)
            {
                EditorUtility.DisplayDialog("Habla Camarón",
                    "Faltan prefabs (Road_1A / Road_1A_+2 / Roundabout).", "OK");
                return;
            }

            float roadYaw = MeasureSlopeYaw(slope, out float rise);
            var world = new GameObject("CorredorExamen_ToonCity").transform;

            // ---- Redondel central ----
            var ringGO = (GameObject)PrefabUtility.InstantiatePrefab(roundaboutPrefab, world);
            // Amplio como el de la Zona Sur (playtest 2026-07-14): solo XZ,
            // las alturas de calzada no cambian.
            ringGO.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
            var rb = WorldBounds(ringGO);
            ringGO.transform.position += new Vector3(-rb.center.x, -rb.min.y, -rb.center.z);
            rb = WorldBounds(ringGO);
            float ringRadius = Mathf.Max(rb.extents.x, rb.extents.z);

            // ---- Brazos: sur largo (Guamaní), norte con cuesta (La Carolina) ----
            // Niveles finales MÁS LARGOS (2026-07-15): +2 módulos por brazo
            // principal — la ruta del examen de hora pico creció ~30%.
            var south = BuildArm("Brazo_Sur_Guamani", world, roadYaw,
                ArmModules(flat, slope, flats: 8, slopes: 0, flatsTop: 0, rise));
            var north = BuildArm("Brazo_Norte_LaCarolina", world, roadYaw,
                ArmModules(flat, slope, flats: 2, slopes: 2, flatsTop: 5, rise));
            var east = BuildArm("Brazo_Este", world, roadYaw,
                ArmModules(flat, slope, 2, 0, 0, rise));
            var west = BuildArm("Brazo_Oeste", world, roadYaw,
                ArmModules(flat, slope, 2, 0, 0, rise));

            // ---- Decoración (antes de PlaceArm: contenedores en el origen) ----
            DecorateBarrioSur(south);
            FillUnder(north);
            DecorateLaCarolina(north);
            DecorateStub(east);
            DecorateStub(west);

            // ---- Pase de DISEÑO: la ciudad del examen se ve ciudad ----
            float paveE = CityDecorKit.Sidewalks(east);
            float paveO = CityDecorKit.Sidewalks(west);
            CityDecorKit.Sidewalks(north); // el llano antes de la cuesta
            CityDecorKit.BuildingRows(east, CasasTransversales, east.RoadWidth * 0.5f + paveE, seed: 51);
            CityDecorKit.BuildingRows(west, CasasTransversales, west.RoadWidth * 0.5f + paveO, seed: 52);
            CityDecorKit.SidewalkLife(south, south.RoadWidth * 0.5f + 2.2f, seed: 53);
            CityDecorKit.SidewalkLife(east, east.RoadWidth * 0.5f + 2.2f, seed: 54);
            CityDecorKit.SidewalkLife(west, west.RoadWidth * 0.5f + 2.2f, seed: 55);

            PlaceArm(south, 180f, ringRadius * 0.92f);
            PlaceArm(north, 0f, ringRadius * 0.92f);
            PlaceArm(east, 90f, ringRadius * 0.92f);
            PlaceArm(west, -90f, ringRadius * 0.92f);

            // ---- Suavizado de superficie (fix playtest: labios y acantilados) ----
            AddSlopeSmoothColliders(north);
            AddEntrySeamRamp(south);
            AddEntrySeamRamp(north);
            AddEntrySeamRamp(east);
            AddEntrySeamRamp(west);

            // ---- Grafo ----
            var graphGO = new GameObject("[RoadGraph]");
            var graph = graphGO.AddComponent<RoadGraph>();
            var ring = BuildRingNodes(graph.Data, ringRadius * 0.62f);
            foreach (var arm in new[] { south, north, east, west })
                AddArmToGraph(graph.Data, arm, ring);

            // ---- Semáforos del redondel y señales ----
            var lightsGO = new GameObject("[TrafficLights]");
            var lights = lightsGO.AddComponent<TrafficLightController>();
            AddTrafficLight(lights, north, 0);
            AddTrafficLight(lights, south, 0);
            AddTrafficLight(lights, east, 1);
            AddTrafficLight(lights, west, 1);
            east.Inbound[0].Sign = SignType.Yield;
            west.Inbound[0].Sign = SignType.Yield;
            south.Inbound[2].Sign = SignType.SpeedLimit50;

            // ---- Señalética ecuatoriana del corredor (Fase 4) ----
            PlaceEcuadorSigns(south, north, east, west);

            // ---- La META: arco + ancla del trigger (Fase 3) al final del norte ----
            Vector3 metaLocal = new Vector3(0f, north.EndY, north.Length - 5f);
            Vector3 metaWorld = north.Container.TransformPoint(metaLocal);
            var arch = Load($"{TC}/Pavement/Modern_Arch_1A.prefab");
            if (arch != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(arch, north.Container);
                go.transform.localPosition = metaLocal;
            }
            var meta = new GameObject("[MetaExamen]");
            meta.transform.position = metaWorld + Vector3.up * 0.5f;

            // ---- Piso y Aveo: nace en el extremo sur mirando al norte ----
            BuildGround(world, Vector3.zero + new Vector3(0f, -0.03f, 0f), 60f);
            // Altura real de la vía por raycast y yaw hacia el redondel (−z
            // local del brazo): mismos fixes que la Zona Sur — con y=0.6 y
            // yaw 0 fijos el Aveo podía nacer enterrado/flotando y mirando
            // al borde del mapa.
            Physics.SyncTransforms(); // colliders recién creados: sincronizar antes del raycast
            Vector3 sProbe = south.Container.TransformPoint(
                new Vector3(-south.RoadWidth * 0.22f, 30f, south.Length - 6f));
            float sFloorY = Physics.Raycast(sProbe, Vector3.down, out var sHit, 100f)
                ? south.Container.InverseTransformPoint(sHit.point).y : 0f;
            Vector3 spawnWorld = south.Container.TransformPoint(
                new Vector3(-south.RoadWidth * 0.22f, sFloorY + 0.6f, south.Length - 6f));
            Vector3 northDir = south.Container.rotation * Vector3.back;
            float spawnYaw = Mathf.Atan2(northDir.x, northDir.z) * Mathf.Rad2Deg;
            BuildPlayerCar(GetOrCreateAveoSpec(), spawnWorld, spawnYaw);

            var ui = new GameObject("[GameplayUI]");
            ui.AddComponent<HUDController>();
            ui.AddComponent<PauseController>();
            ui.AddComponent<DonPanchoDialogue>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            // ---- Validación: la ruta del examen DEBE existir ----
            var problems = graph.Data.Validate();
            var spawnNode = graph.Data.NearestNode(spawnWorld);
            var metaNode = graph.Data.NearestNode(metaWorld);
            bool reachable = graph.Data.IsReachable(spawnNode.Id, metaNode.Id);

            BuildWorldLimits(graph.Data); // muros invisibles: el examen no se sale del corredor
            // El examen se corre EN LA VÍA: costados de los 4 brazos cerrados.
            AddSideWalls(south); AddSideWalls(north); AddSideWalls(east); AddSideWalls(west);

            // ---- Escenografía (más allá de los muros: solo se VE) ----
            CityDecorKit.RoundaboutMonument(world, ringGO.transform, ringRadius, seed: 17);
            Vector3 mn = graph.Data.Nodes[0].Position, mx = mn;
            foreach (var n in graph.Data.Nodes)
            { mn = Vector3.Min(mn, n.Position); mx = Vector3.Max(mx, n.Position); }
            CityDecorKit.Skyline(world, mn, mx, TorresSkyline, seed: 18);
            CityDecorKit.Clouds(world, seed: 19); // nubes del atardecer

            // Vallas publicitarias de La Carolina (la ciudad moderna anuncia):
            // a los costados del brazo norte, de cara al que sube al examen.
            float bx = north.RoadWidth * 0.5f + 6.5f;
            foreach (var (side, z, ad) in new (float, float, string)[]
                     { (1f, north.Length - 24f, "Billboard_2A"), (-1f, north.Length - 12f, "Billboard_3A") })
            {
                Vector3 at = north.Container.TransformPoint(new Vector3(side * bx, north.EndY, z));
                Vector3 haciaVia = north.Container.TransformPoint(new Vector3(0f, north.EndY, z)) - at;
                float yaw = Mathf.Atan2(haciaVia.x, haciaVia.z) * Mathf.Rad2Deg;
                CityDecorKit.Billboard(world, at, yaw, ad);
            }

            // Nada flotando (playtest: edificios voladores) — pase final.
            int asentados = CityDecorKit.ReGround(world);
            if (asentados > 0)
                Debug.Log($"[Habla Camarón] Corredor: {asentados} objetos re-asentados al piso.");
            TrafficInjectionTool.InjectIntoOpenScene(SceneP); // NPCs con modelo, no cubos
            EditorSceneManager.SaveScene(scene, SceneP);
            AddToBuildSettings(SceneP);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Habla Camarón — Fase 1",
                $"Corredor del Examen creado.\n\n" +
                $"Grafo: {graph.Data.Nodes.Count} nodos, {graph.Data.Edges.Count} aristas.\n" +
                $"Validación: {(problems.Count == 0 ? "ninguno ✓" : string.Join("\n", problems))}\n" +
                $"Ruta Guamaní → meta La Carolina: {(reachable ? "alcanzable ✓" : "¡NO ALCANZABLE!")}\n\n" +
                "La meta es el arco al final del brazo norte ([MetaExamen]).\n" +
                "Esta es la ruta que el examen final recorrerá contra el reloj.", "¡Habla, camarón!");

            if (problems.Count > 0 || !reachable)
                Debug.LogError("[Habla Camarón] Grafo del Corredor con problemas.");
        }

        // ---- Señales ecuatorianas del corredor (kit de la Fase 4). El jugador
        //      nace al final del brazo sur y va hacia el redondel (carril −x,
        //      avanzando a −z: sus señales llevan yaw 180). ----
        private static void PlaceEcuadorSigns(Arm south, Arm north, Arm east, Arm west)
        {
            float sEdge = south.RoadWidth * 0.5f + 0.9f;

            // El límite 50 ya vivía en el grafo (south.Inbound[2]) pero SIN señal
            // visible: ahora la placa "50" existe y enlaza ese mismo nodo.
            EcuadorSignKit.Place("lim50", south.Container,
                new Vector3(-sEdge, 0f, south.InboundLocal[2].z), 180f,
                new[] { south.Inbound[2].Id });
            EcuadorSignKit.Place("semaforo", south.Container, new Vector3(-sEdge, 0f, 10f), 180f);
            EcuadorSignKit.Place("no_giro_u", south.Container, new Vector3(sEdge, 0f, 24f), 0f);

            // La Carolina: la meseta alta del brazo norte (buscar dónde empieza
            // el llano elevado con los módulos medidos, como DecorateLaCarolina).
            float nEdge = north.RoadWidth * 0.5f + 0.9f;
            float plateauZ0 = north.Length * 0.6f;
            foreach (var m in north.Modules)
                if (Mathf.Abs(m.Rise) < 0.05f && m.Y0 > 0.05f) { plateauZ0 = m.Z0; break; }
            EcuadorSignKit.Place("hospital", north.Container,
                new Vector3(nEdge, north.EndY, Mathf.Max(plateauZ0 + 2f, north.Length - 16f)), 0f);
            EcuadorSignKit.Place("no_estacionar", north.Container,
                new Vector3(nEdge, north.EndY, north.Length - 9f), 0f);

            // Brazos cortos: sentido único y contravía señalizados.
            EcuadorSignKit.Place("una_via", east.Container,
                new Vector3(east.RoadWidth * 0.5f + 0.9f, 0f, 4f), 0f);
            EcuadorSignKit.Place("pare", east.Container,
                new Vector3(east.RoadWidth * 0.5f + 0.9f, 0f, east.Length - 3f), 0f);
            EcuadorSignKit.Place("no_entre", west.Container,
                new Vector3(-(west.RoadWidth * 0.5f + 0.9f), 0f, 4f), 180f);
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

        // ---- Sur: barrio popular denso (casas, veredas, árboles, parqueados) ----
        private static void DecorateBarrioSur(Arm arm)
        {
            float half = arm.RoadWidth * 0.5f;
            float paveW = TilePavement(arm, half);

            string[] names = { "Building_1A", "Building_2A", "Brownstone_1A",
                               "Building_13A", "Brownstone_2A" };
            LineBuildings(arm, names, half + paveW, seed: 11);

            var tree = Load($"{TC}/Vegetation/Tree_1A.prefab");
            if (tree != null)
                for (float z = 8f; z < arm.Length; z += 20f)
                    PlaceAligned(tree, arm.Container, 0f, z, 0f, half + paveW * 0.5f);

            var parked = Load($"{TC}/Vehicles/Car_15B.prefab");
            if (parked != null)
            {
                PlaceAligned(parked, arm.Container, 180f, 12f, 0f, half - 1.6f);
                PlaceAligned(parked, arm.Container, 180f, 40f, 0f, half - 1.6f);
            }
        }

        // ---- Norte alto: La Carolina moderna con su parque junto a la meta ----
        private static void DecorateLaCarolina(Arm arm)
        {
            float half = arm.RoadWidth * 0.5f;
            string[] modern = { "Building_10A", "Building_12A", "Building_14A",
                                "Building_16A", "Building_17A" };

            // Solo los módulos planos superiores llevan edificios (la meseta).
            float plateauZ0 = 0f;
            foreach (var m in arm.Modules)
                if (Mathf.Abs(m.Rise) < 0.05f && m.Y0 > 0.05f) { plateauZ0 = m.Z0; break; }
            float plateauY = arm.EndY;

            var rng = new System.Random(23);
            foreach (float s in new[] { -1f, 1f })
            {
                float z = plateauZ0 + 1f;
                while (z < arm.Length - 6f)
                {
                    var prefab = Load($"{TC}/Buildings/{modern[rng.Next(modern.Length)]}.prefab");
                    if (prefab == null) break;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, arm.Container);
                    go.transform.localRotation = Quaternion.Euler(0f, s > 0 ? -90f : 90f, 0f);
                    var b = WorldBounds(go);
                    float x = s * (half + 2.5f + b.size.x * 0.5f);
                    go.transform.position += new Vector3(x - b.center.x, plateauY - b.min.y, 0f);
                    var lp = go.transform.localPosition;
                    go.transform.localPosition = new Vector3(lp.x, lp.y, z + b.size.z * 0.5f);
                    z += b.size.z + 1.5f;
                }
            }

            // El "parque de La Carolina": árboles y bancas junto a la meta.
            var tree = Load($"{TC}/Vegetation/Tree_2A.prefab");
            var bench = Load($"{TC}/Props/Bench_2A.prefab");
            for (int i = 0; i < 5 && tree != null; i++)
                PlaceAligned(tree, arm.Container, 0f, arm.Length - 4f - i * 3.5f,
                    plateauY, (i % 2 == 0 ? 1f : -1f) * (half + 1.5f));
            if (bench != null)
                PlaceAligned(bench, arm.Container, 90f, arm.Length - 8f, plateauY, half + 1.2f);
        }

        private static void DecorateStub(Arm arm)
        {
            var tree = Load($"{TC}/Vegetation/Tree_1B.prefab");
            var pole = Load($"{TC}/Roads/Streetlight_1A.prefab");
            float edge = arm.RoadWidth * 0.5f + 1.2f;
            if (pole != null) PlaceAligned(pole, arm.Container, 90f, 5f, 0f, edge);
            if (tree != null)
                for (float z = 4f; z < arm.Length; z += 10f)
                    PlaceAligned(tree, arm.Container, 0f, z, 0f, -edge);
        }

        // ---- Helpers compartidos con el estilo del barrio ----

        private static float TilePavement(Arm arm, float half)
        {
            var pave = Load($"{TC}/Pavement/Pavement_1A_2x2.prefab");
            if (pave == null) return 3f;
            var probe = PlaceAligned(pave, arm.Container, 0f, -100f, 0f, 0f);
            float tile = probe.size.z, paveW = probe.size.x;
            Object.DestroyImmediate(arm.Container.GetChild(arm.Container.childCount - 1).gameObject);
            foreach (float s in new[] { -1f, 1f })
                for (float z = 2f; z < arm.Length - tile * 0.5f; z += tile)
                    PlaceAligned(pave, arm.Container, 0f, z, 0f, s * (half + paveW * 0.5f));
            return paveW;
        }

        private static void LineBuildings(Arm arm, string[] names, float innerX, int seed)
        {
            var rng = new System.Random(seed);
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
                    float x = s * (innerX + b.size.x * 0.5f + 0.4f);
                    go.transform.position += new Vector3(x - b.center.x, -b.min.y, 0f);
                    var lp = go.transform.localPosition;
                    go.transform.localPosition = new Vector3(lp.x, lp.y, z + b.size.z * 0.5f);
                    z += b.size.z + 1.2f;
                }
            }
        }
    }
}
