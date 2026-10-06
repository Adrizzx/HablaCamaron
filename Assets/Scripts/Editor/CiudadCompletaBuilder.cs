using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using HablaCamaron.UI;
using HablaCamaron.World;
using static HablaCamaron.EditorTools.ToonCityKit;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// LA CIUDAD COMPLETA (pedido del playtest: "usar todo el mapa") — genera
    /// N2_QuitoCiudad: una cuadrícula de 5×4 intersecciones (20 esquinas, 31
    /// cuadras de calle) con el grafo por carril de CityGridGraph, asfalto y
    /// veredas de RoadMeshKit EXACTAMENTE sobre las líneas del grafo, manzanas
    /// edificadas con los prefabs exonerados de Toon City (blindaje del crash
    /// "level corrupted": NO agregar prefabs nuevos sin doble smoke test),
    /// semáforos reales en las 6 intersecciones del centro (N/S vs E/O) y
    /// señalética ecuatoriana. Aloja la misión bonus "Quito entero" (id 7).
    /// Menú: Habla Camarón > 8 · Crear Ciudad Completa
    /// </summary>
    public static class CiudadCompletaBuilder
    {
        private const string SceneP = "Assets/Scenes/N2_QuitoCiudad.unity";

        // La cuadrícula (mismos números que CityGridGraphTests).
        private const int Cols = 5, Rows = 4;
        private const float Spacing = 46f;   // metros entre intersecciones
        private const float Lane = 2.6f;     // centro del carril desde el eje
        private const float Inset = 8f;      // nodos de entrada/salida
        private const float HalfStreet = 5.5f;
        private const float SidewalkW = 3f;
        private const float CrossHalf = 7.5f; // media intersección (sin veredas)

        // SOLO prefabs exonerados (los mismos de las filas de casas de las
        // otras zonas — ver el gotcha del crash en ZonaSurBuilder).
        private static readonly string[] Casas =
        {
            "Brownstone_1A", "Brownstone_2A", "Brownstone_3A",
            "Building_1A", "Building_2A", "Building_13A",
        };

        [MenuItem("Habla Camarón/8 · Crear Ciudad Completa (N2_QuitoCiudad)")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupAmbience(sunset: true); // el paseo de graduado es al atardecer

            var world = new GameObject("QuitoCiudad_ToonCity").transform;
            BuildGround(world, new Vector3(0f, -0.03f, 0f), 80f,
                new Color(0.46f, 0.50f, 0.36f)); // verde urbano apagado

            // ---- El grafo manda: las calles se dibujan SOBRE sus líneas ----
            var graphGO = new GameObject("[RoadGraph]");
            var graph = graphGO.AddComponent<RoadGraph>();
            graph.Data = CityGridGraph.Build(Cols, Rows, Spacing, Lane, Inset,
                interiorLights: true);

            BuildStreets(world);
            BuildBlocks(world);
            BuildLights(world, graph.Data);
            PlaceSigns(world, graph.Data);

            // ---- El Aveo nace en la esquina suroeste, rumbo este ----
            Vector3 c00 = CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, 0, 0);
            Vector3 spawn = c00 + new Vector3(Inset + 2f, 0.6f, -Lane);
            BuildPlayerCar(GetOrCreateAveoSpec(), spawn, 90f);

            var ui = new GameObject("[GameplayUI]");
            ui.AddComponent<HUDController>();
            ui.AddComponent<PauseController>();
            ui.AddComponent<DonPanchoDialogue>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            // ---- Validación (criterio de salida, como en las otras zonas) ----
            var problems = graph.Data.Validate();
            var spawnNode = graph.Data.NearestNode(spawn);
            var far = Missions.MissionGoals.Farthest(graph.Data, spawn);
            bool reachable = far != null &&
                graph.Data.IsReachable(spawnNode.Id, far.Id) &&
                graph.Data.IsReachable(far.Id, spawnNode.Id);

            BuildWorldLimits(graph.Data); // muros invisibles del perímetro

            // ---- Escenografía del horizonte ----
            Vector3 mn = graph.Data.Nodes[0].Position, mx = mn;
            foreach (var n in graph.Data.Nodes)
            { mn = Vector3.Min(mn, n.Position); mx = Vector3.Max(mx, n.Position); }
            CityDecorKit.Skyline(world, mn, mx, Casas, seed: 18);
            CityDecorKit.Clouds(world, seed: 19, count: 9, spread: 220f);

            TrafficInjectionTool.InjectIntoOpenScene(SceneP); // NPCs con modelo, no cubos
            EditorSceneManager.SaveScene(scene, SceneP);
            AddToBuildSettings(SceneP);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Habla Camarón — Ciudad Completa",
                $"N2_QuitoCiudad creada.\n\n" +
                $"Grafo: {graph.Data.Nodes.Count} nodos, {graph.Data.Edges.Count} aristas.\n" +
                $"Problemas: {(problems.Count == 0 ? "ninguno ✓" : string.Join("\n", problems))}\n" +
                $"Ruta spawn → meta (ida y vuelta): {(reachable ? "alcanzable ✓" : "¡NO ALCANZABLE!")}",
                "¡A rodar!");

            if (problems.Count > 0 || !reachable)
                Debug.LogError("[Habla Camarón] El grafo de la Ciudad tiene problemas: revisar reporte.");
        }

        // ================== Calles (RoadMeshKit sobre el grafo) ==================

        private static void BuildStreets(Transform world)
        {
            var asphalt = RoadMeshKit.SolidMat(new Color(0.23f, 0.23f, 0.25f));
            var paint = RoadMeshKit.SolidMat(new Color(0.92f, 0.88f, 0.72f));
            var curbMat = RoadMeshKit.SolidMat(new Color(0.62f, 0.60f, 0.56f));
            var walkMat = RoadMeshKit.SolidMat(new Color(0.72f, 0.68f, 0.60f));

            Vector3 c00 = CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, 0, 0);
            Vector3 cNN = CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, Cols - 1, Rows - 1);

            var root = new GameObject("Calles").transform;
            root.SetParent(world, false);

            // Cintas de asfalto CONTINUAS por línea de cuadrícula (la de las
            // columnas va 1.5 cm más arriba: nada de z-fighting en los cruces).
            for (int j = 0; j < Rows; j++)
            {
                var pts = Line(new Vector3(c00.x - 14f, 0f, c00.z + j * Spacing),
                               new Vector3(cNN.x + 14f, 0f, c00.z + j * Spacing));
                var r = RoadMeshKit.ComputeRights(pts);
                RoadMeshKit.BuildRibbon($"Asfalto_Fila_{j}", root, pts, r,
                    -HalfStreet, HalfStreet, 0.02f, asphalt, collider: true);
            }
            for (int i = 0; i < Cols; i++)
            {
                var pts = Line(new Vector3(c00.x + i * Spacing, 0f, c00.z - 14f),
                               new Vector3(c00.x + i * Spacing, 0f, cNN.z + 14f));
                var r = RoadMeshKit.ComputeRights(pts);
                RoadMeshKit.BuildRibbon($"Asfalto_Col_{i}", root, pts, r,
                    -HalfStreet, HalfStreet, 0.035f, asphalt, collider: true);
            }

            // Por CUADRA (entre intersecciones): línea central, bordillos y
            // veredas — cortados en los cruces para no tapar la otra calle.
            ForEachSegment((a, b, t) =>
            {
                var pts = Line(a + t * CrossHalf, b - t * CrossHalf);
                var r = RoadMeshKit.ComputeRights(pts);
                float lift = Mathf.Abs(t.x) > 0.5f ? 0.03f : 0.045f;

                RoadMeshKit.BuildDashes("LineaCentral", root, pts, r,
                    0f, 0.14f, lift, dash: 3f, gap: 3f, paint);
                foreach (float s in new[] { -1f, 1f })
                {
                    RoadMeshKit.BuildCurb("Bordillo", root, pts, r,
                        s * (HalfStreet + 0.18f), 0.36f, 0.14f, curbMat, collider: true);
                    // BuildRibbon exige left < right (si no, la normal queda
                    // hacia abajo y la vereda se vuelve invisible).
                    float o1 = s * (HalfStreet + 0.36f);
                    float o2 = s * (HalfStreet + 0.36f + SidewalkW);
                    RoadMeshKit.BuildRibbon("Vereda", root, pts, r,
                        Mathf.Min(o1, o2), Mathf.Max(o1, o2),
                        0.14f, walkMat, collider: true);
                }
            });
        }

        /// <summary>Recorre cada cuadra: (centro A, centro B, dirección A→B).</summary>
        private static void ForEachSegment(System.Action<Vector3, Vector3, Vector3> visit)
        {
            for (int i = 0; i < Cols; i++)
                for (int j = 0; j < Rows; j++)
                {
                    Vector3 a = CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, i, j);
                    if (i + 1 < Cols)
                        visit(a, CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, i + 1, j),
                              Vector3.right);
                    if (j + 1 < Rows)
                        visit(a, CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, i, j + 1),
                              Vector3.forward);
                }
        }

        private static System.Collections.Generic.List<Vector3> Line(Vector3 a, Vector3 b)
        {
            var pts = new System.Collections.Generic.List<Vector3>();
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 12f));
            for (int k = 0; k <= steps; k++) pts.Add(Vector3.Lerp(a, b, k / (float)steps));
            return pts;
        }

        // ================== Manzanas edificadas ==================

        private static void BuildBlocks(Transform world)
        {
            var rng = new System.Random(23);
            var tree = Load($"{TC}/Vegetation/Tree_1A.prefab");
            float innerHalf = Spacing * 0.5f - (HalfStreet + 0.36f + SidewalkW);

            var root = new GameObject("Manzanas").transform;
            root.SetParent(world, false);

            for (int bi = 0; bi < Cols - 1; bi++)
                for (int bj = 0; bj < Rows - 1; bj++)
                {
                    Vector3 center =
                        (CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, bi, bj) +
                         CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, bi + 1, bj + 1)) * 0.5f;

                    foreach (var side in CityGridGraph.Dirs)
                        BuildBlockSide(root, center, side, innerHalf, rng);

                    if (tree != null)
                        CityDecorKit.PlaceAt(tree, root, center, rng.Next(360));
                }
        }

        /// <summary>Una hilera de fachadas mirando a la calle de un costado.</summary>
        private static void BuildBlockSide(Transform parent, Vector3 blockCenter,
            Vector3 side, float innerHalf, System.Random rng)
        {
            Vector3 along = Vector3.Cross(Vector3.up, side); // recorre el frente
            float yaw = Mathf.Atan2(side.x, side.z) * Mathf.Rad2Deg;
            float cursor = -(innerHalf - 6f);

            while (cursor < innerHalf - 6f)
            {
                var prefab = Load($"{TC}/Buildings/{Casas[rng.Next(Casas.Length)]}.prefab");
                if (prefab == null) return;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                var b = WorldBounds(go);
                float depth = Mathf.Abs(side.x) > 0.5f ? b.size.x : b.size.z;
                float width = Mathf.Abs(side.x) > 0.5f ? b.size.z : b.size.x;

                if (cursor + width > innerHalf - 6f)
                {
                    Object.DestroyImmediate(go);
                    return;
                }

                Vector3 target = blockCenter
                    + side * (innerHalf - depth * 0.5f - 0.4f)
                    + along * (cursor + width * 0.5f);
                go.transform.position += new Vector3(
                    target.x - b.center.x, -b.min.y, target.z - b.center.z);

                cursor += width + 1.4f;
            }
        }

        // ================== Semáforos del centro ==================

        private static void BuildLights(Transform world, RoadGraphData data)
        {
            var lightsGO = new GameObject("[TrafficLights]");
            var lights = lightsGO.AddComponent<TrafficLightController>();

            for (int i = 1; i < Cols - 1; i++)
                for (int j = 1; j < Rows - 1; j++)
                {
                    Vector3 c = CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, i, j);
                    for (int d = 0; d < 4; d++)
                    {
                        Vector3 t = CityGridGraph.Dirs[d];
                        Vector3 right = Vector3.Cross(Vector3.up, t);
                        int group = (d == 1 || d == 3) ? 0 : 1; // el de CityGridGraph
                        Vector3 pos = c - t * (Inset - 1f) + right * (HalfStreet + 0.9f);
                        float yaw = Mathf.Atan2(-t.x, -t.z) * Mathf.Rad2Deg; // de cara al que llega
                        TrafficLightKit.Place(world, pos, yaw, lights, group);
                    }
                }
        }

        // ================== Señalética ==================

        private static void PlaceSigns(Transform world, RoadGraphData data)
        {
            // Límite 50 funcional en dos avenidas (dato en el nodo + placa real).
            AddLimit50(world, data, i0: 0, j0: 0, d: 0); // borde sur, rumbo este
            AddLimit50(world, data, i0: Cols - 1, j0: Rows - 1, d: 2); // borde norte, rumbo oeste

            // Identidad quiteña decorativa junto a las veredas del centro.
            Vector3 c11 = CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, 1, 1);
            Vector3 c21 = CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, 2, 1);
            Vector3 c12 = CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, 1, 2);
            EcuadorSignKit.Place("parada_trole", world,
                c11 + new Vector3(Spacing * 0.5f, 0f, -(HalfStreet + 1.1f)), 0f);
            EcuadorSignKit.Place("no_pitar", world,
                c21 + new Vector3(-Spacing * 0.5f, 0f, HalfStreet + 1.1f), 180f);
            EcuadorSignKit.Place("peatones", world,
                c12 + new Vector3(HalfStreet + 1.1f, 0f, Spacing * 0.5f), -90f);
            EcuadorSignKit.Place("semaforo", world,
                c11 + new Vector3(-(HalfStreet + 1.1f), 0f, -Spacing * 0.35f), 90f);
        }

        private static void AddLimit50(Transform world, RoadGraphData data,
            int i0, int j0, int d)
        {
            Vector3 t = CityGridGraph.Dirs[d];
            Vector3 right = Vector3.Cross(Vector3.up, t);
            Vector3 a = CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, i0, j0);
            Vector3 mid = a + t * (Spacing * 0.5f) + right * Lane;

            var node = data.NearestNode(mid);
            node.Sign = SignType.SpeedLimit50;

            Vector3 signPos = a + t * (Spacing * 0.5f) + right * (HalfStreet + 1.0f);
            float yaw = Mathf.Atan2(-t.x, -t.z) * Mathf.Rad2Deg;
            var go = EcuadorSignKit.Place("lim50", world, signPos, yaw, new[] { node.Id });
            if (go == null) Debug.LogWarning("[Habla Camarón] Falta la placa lim50 del catálogo.");
        }
    }
}
