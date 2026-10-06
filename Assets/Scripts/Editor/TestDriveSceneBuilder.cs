using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using HablaCamaron.UI;
using static HablaCamaron.EditorTools.ToonCityKit;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// FASE 0 — Genera la escena de prueba de manejo "N0_TestDrive" usando los
    /// prefabs de Toon City: una avenida recta con una cuesta al estilo Quito,
    /// veredas, edificios, postes, autos parqueados... y el Aveo jugable armado
    /// con su física completa (WheelColliders, embrague, cámara, HUD).
    ///
    /// Las utilidades de medición/colocación viven en ToonCityKit (compartidas
    /// con los armadores de zonas de la Fase 1).
    /// Menú: Habla Camarón > 2 · Crear escena de prueba de manejo (Fase 0)
    /// </summary>
    public static class TestDriveSceneBuilder
    {
        private const string SceneP = "Assets/Scenes/N0_TestDrive.unity";

        // Composición de la avenida (módulos de vía).
        private const int FlatLow = 6;    // tramo plano inicial
        private const int SlopeCount = 3; // módulos de cuesta (+2 de altura c/u)
        private const int FlatTop = 3;    // meseta superior

        [MenuItem("Habla Camarón/2 · Crear escena de prueba de manejo (Fase 0)")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupAmbience(sunset: true);

            var roadFlat = Load($"{TC}/Roads/Road_1A.prefab");
            var roadSlope = Load($"{TC}/Roads/Road_1A_+2.prefab");
            if (roadFlat == null || roadSlope == null)
            {
                EditorUtility.DisplayDialog("Habla Camarón",
                    "No encuentro los prefabs de Toon City (Road_1A / Road_1A_+2).\n" +
                    "Verifica la carpeta Assets/Toon Series/Toon City/Prefabs/Roads.", "OK");
                return;
            }

            float roadYaw = MeasureSlopeYaw(roadSlope, out float slopeRise);

            // ---- Construcción de la avenida ----
            var world = new GameObject("Entorno_ToonCity").transform;
            float cursorZ = 0f, cursorY = 0f;
            var roadBoundsList = new List<Bounds>();

            void PlaceRoad(GameObject prefab, float rise)
            {
                var b = PlaceAligned(prefab, world, roadYaw, cursorZ, cursorY, 0f);
                roadBoundsList.Add(b);
                cursorZ = b.max.z;
                cursorY += rise;
            }

            for (int i = 0; i < FlatLow; i++) PlaceRoad(roadFlat, 0f);
            float slopeStartZ = cursorZ, slopeStartY = cursorY;
            for (int i = 0; i < SlopeCount; i++) PlaceRoad(roadSlope, slopeRise);
            float slopeEndZ = cursorZ, topY = cursorY;
            for (int i = 0; i < FlatTop; i++) PlaceRoad(roadFlat, 0f);
            float endZ = cursorZ;

            float roadWidth = roadBoundsList[0].size.x;
            float half = roadWidth * 0.5f;

            // ---- Piso, colina de relleno y cierre de la vía ----
            BuildGround(world, new Vector3(0f, -0.03f, endZ * 0.5f), 40f);
            BuildHillFill(world, roadWidth, slopeStartZ, slopeEndZ, slopeStartY, topY, endZ);
            BuildEndBarrier(world, endZ, topY, roadWidth);

            // ---- Veredas, edificios y mobiliario urbano (tramo bajo) ----
            float lowEndZ = slopeStartZ;
            float paveWidth = BuildPavements(world, half, lowEndZ);
            BuildBuildings(world, half + paveWidth, lowEndZ);
            BuildStreetProps(world, half, paveWidth, lowEndZ);
            BuildParkedCars(world, half, lowEndZ);

            // ---- El Aveo jugable ----
            BuildPlayerCar(GetOrCreateAveoSpec(), new Vector3(half * 0.45f, 0.6f, 3f));

            // ---- UI de gameplay (HUD real, sin el tester de mentira) ----
            var ui = new GameObject("[GameplayUI]");
            ui.AddComponent<HUDController>();
            ui.AddComponent<PauseController>();
            ui.AddComponent<DonPanchoDialogue>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            // ---- Guardar y registrar ----
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, SceneP);
            AddToBuildSettings(SceneP);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Habla Camarón — Fase 0",
                "Escena N0_TestDrive creada.\n\n" +
                "CONTROLES:\n" +
                "  Shift = embrague (¡mantener!)   F = encender\n" +
                "  W/S = acelerar/frenar   A/D = volante\n" +
                "  1-5 / N / R = marchas   Espacio = freno de mano\n" +
                "  Q/E = direccionales   L = luces   B = bocina   C = cámara   H = ayuda\n\n" +
                "Ritual de arranque: Shift → F → 1ª → acelera un poco →\n" +
                "suelta Shift SUAVE. Si lo sueltas de golpe... se cala 😉", "¡A manejar!");
        }

        // Cajas de relleno bajo la parte elevada para que la cuesta no "flote".
        private static void BuildHillFill(Transform parent, float roadWidth,
            float z0, float z1, float y0, float y1, float endZ)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.30f, 0.27f, 0.20f);
            float width = roadWidth + 10f;

            float topLen = endZ - z1;
            if (topLen > 0.1f && y1 > 0.1f)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Colina_Meseta";
                box.transform.SetParent(parent);
                box.transform.position = new Vector3(0f, y1 * 0.5f - 0.05f, z1 + topLen * 0.5f);
                box.transform.localScale = new Vector3(width, y1 - 0.1f, topLen);
                box.GetComponent<Renderer>().sharedMaterial = mat;
                Object.DestroyImmediate(box.GetComponent<Collider>());
            }

            float len = z1 - z0, rise = y1 - y0;
            if (len > 0.1f && rise > 0.1f)
            {
                float angle = Mathf.Atan2(rise, len) * Mathf.Rad2Deg;
                var wedge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wedge.name = "Colina_Rampa";
                wedge.transform.SetParent(parent);
                float thick = 3f;
                Vector3 mid = new Vector3(0f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
                Vector3 normal = Quaternion.Euler(-angle, 0f, 0f) * Vector3.up;
                wedge.transform.position = mid - normal * (thick * 0.5f + 0.06f);
                wedge.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
                wedge.transform.localScale = new Vector3(width, thick, Mathf.Sqrt(len * len + rise * rise));
                wedge.GetComponent<Renderer>().sharedMaterial = mat;
                Object.DestroyImmediate(wedge.GetComponent<Collider>());
            }
        }

        private static void BuildEndBarrier(Transform parent, float endZ, float topY, float roadWidth)
        {
            var barrel = Load($"{TC}/Roads/Impact_Barrel_1A.prefab");
            if (barrel == null) return;
            int count = Mathf.Max(4, Mathf.RoundToInt(roadWidth / 1.5f));
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-roadWidth * 0.45f, roadWidth * 0.45f, i / (float)(count - 1));
                PlaceAligned(barrel, parent, 0f, endZ - 1.6f, topY, x);
            }
        }

        private static float BuildPavements(Transform parent, float roadHalf, float endZ)
        {
            var pave = Load($"{TC}/Pavement/Pavement_1A_2x2.prefab");
            if (pave == null) return 3f;

            var probe = PlaceAligned(pave, parent, 0f, -100f, 0f, 0f);
            float tile = probe.size.z;
            float paveW = probe.size.x;
            Object.DestroyImmediate(parent.GetChild(parent.childCount - 1).gameObject);

            foreach (float sideSign in new[] { -1f, 1f })
            {
                float x = sideSign * (roadHalf + paveW * 0.5f);
                for (float z = 0f; z < endZ - tile * 0.5f; z += tile)
                    PlaceAligned(pave, parent, 0f, z, 0f, x);
            }
            return paveW;
        }

        private static void BuildBuildings(Transform parent, float innerX, float endZ)
        {
            string[] names = { "Building_1A", "Building_2A", "Building_10A",
                               "Building_13A", "Brownstone_1A", "Building_17A" };
            var prefabs = names.Select(n => Load($"{TC}/Buildings/{n}.prefab"))
                               .Where(p => p != null).ToArray();
            if (prefabs.Length == 0) return;

            var rng = new System.Random(42); // misma calle en cada regeneración
            foreach (float sideSign in new[] { -1f, 1f })
            {
                float yaw = sideSign > 0 ? -90f : 90f; // fachada mirando a la vía
                float z = 1f;
                while (z < endZ - 4f)
                {
                    var prefab = prefabs[rng.Next(prefabs.Length)];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    var b = WorldBounds(go);
                    float x = sideSign * (innerX + b.size.x * 0.5f + 0.4f);
                    go.transform.position += new Vector3(
                        x - b.center.x, 0f - b.min.y, z - b.min.z);
                    z = WorldBounds(go).max.z + 1.2f;
                }
            }
        }

        private static void BuildStreetProps(Transform parent, float roadHalf, float paveW, float endZ)
        {
            var light1 = Load($"{TC}/Roads/Streetlight_1A.prefab");
            var hydrant = Load($"{TC}/Roads/Hydrant_1A.prefab");
            var bench = Load($"{TC}/Props/Bench_2A.prefab");
            var bush = Load($"{TC}/Vegetation/Bush_1A.prefab");
            var cone = Load($"{TC}/Roads/Traffic_Cone_1A.prefab");

            float edgeX = roadHalf + paveW * 0.5f;

            if (light1 != null)
                for (float z = 6f; z < endZ; z += 18f)
                {
                    float side = ((int)(z / 18f) % 2 == 0) ? 1f : -1f;
                    PlaceAligned(light1, parent, side > 0 ? -90f : 90f, z, 0f, side * edgeX);
                }

            if (hydrant != null) PlaceAligned(hydrant, parent, 0f, 9f, 0f, edgeX);
            if (bench != null) PlaceAligned(bench, parent, 90f, 22f, 0f, -edgeX);
            if (bush != null)
                for (float z = 14f; z < endZ; z += 25f)
                    PlaceAligned(bush, parent, 0f, z, 0f, -edgeX);

            if (cone != null)
            {
                PlaceAligned(cone, parent, 0f, endZ - 6f, 0f, roadHalf * 0.7f);
                PlaceAligned(cone, parent, 0f, endZ - 4f, 0f, roadHalf * 0.55f);
            }
        }

        private static void BuildParkedCars(Transform parent, float roadHalf, float endZ)
        {
            string[] cars = { "Car_10C", "Car_15B", "Car_3B" };
            float z = 16f;
            foreach (var name in cars)
            {
                var prefab = Load($"{TC}/Vehicles/{name}.prefab");
                if (prefab == null) continue;
                var b = PlaceAligned(prefab, parent, 0f, z, 0f, -(roadHalf - 1.6f));
                z = b.max.z + 7f;
                if (z > endZ - 8f) break;
            }
        }
    }
}
