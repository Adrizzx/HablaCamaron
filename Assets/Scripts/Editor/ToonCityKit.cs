using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using HablaCamaron.Vehicle;
using HablaCamaron.World;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Utilidades compartidas por TODOS los armadores de escenas (Fase 0, zonas
    /// de la Fase 1...): carga y medición de prefabs de Toon City, colocación
    /// alineada por bounds, ambiente, piso, y el armado completo del Aveo jugable.
    /// Regla de la casa: nunca asumir dimensiones de un prefab — medirlas.
    /// </summary>
    public static class ToonCityKit
    {
        public const string TC = "Assets/Toon Series/Toon City/Prefabs";
        private const string SpecPath = "Assets/Data/Vehicles/Aveo.asset";

        // ================== Carga y medición ==================

        public static GameObject Load(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path);

        public static Bounds WorldBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>
        /// Mide con raycasts hacia qué eje local sube una pieza de cuesta.
        /// Devuelve el yaw para que la subida apunte a +Z y la altura por módulo.
        /// </summary>
        public static float MeasureSlopeYaw(GameObject slopePrefab, out float rise)
        {
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(slopePrefab);
            temp.transform.position = new Vector3(0f, 500f, 0f);
            Physics.SyncTransforms();

            var b = WorldBounds(temp);
            float hXp = SampleHeight(b.center + new Vector3(b.extents.x * 0.8f, 0, 0), b);
            float hXn = SampleHeight(b.center - new Vector3(b.extents.x * 0.8f, 0, 0), b);
            float hZp = SampleHeight(b.center + new Vector3(0, 0, b.extents.z * 0.8f), b);
            float hZn = SampleHeight(b.center - new Vector3(0, 0, b.extents.z * 0.8f), b);
            Object.DestroyImmediate(temp);

            float riseX = hXp - hXn, riseZ = hZp - hZn;
            float yaw;
            if (Mathf.Abs(riseX) > Mathf.Abs(riseZ))
            {
                rise = Mathf.Abs(riseX);
                yaw = riseX > 0 ? -90f : 90f;
            }
            else
            {
                rise = Mathf.Abs(riseZ);
                yaw = riseZ > 0 ? 0f : 180f;
            }
            if (rise < 0.2f) rise = 2f; // fallback: el nombre del prefab dice +2
            return yaw;
        }

        public static float SampleHeight(Vector3 over, Bounds b)
        {
            var origin = new Vector3(over.x, b.max.y + 5f, over.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 50f))
                return hit.point.y;
            return b.min.y;
        }

        /// <summary>
        /// Instancia un prefab con el yaw dado y lo desplaza para que su caja
        /// empiece en minZ, se apoye en y, y quede centrado en centerX.
        /// (Trabaja en el espacio LOCAL del padre si este está en el origen.)
        /// </summary>
        public static Bounds PlaceAligned(GameObject prefab, Transform parent,
            float yaw, float minZ, float y, float centerX)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var b = WorldBounds(go);
            go.transform.position += new Vector3(centerX - b.center.x, y - b.min.y, minZ - b.min.z);
            return WorldBounds(go);
        }

        // ================== Ambiente ==================

        /// <summary>Cielo, sol, ambiente y niebla. Atardecer (examen) o día (tutorial).</summary>
        public static void SetupAmbience(bool sunset)
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Toon Series/Toon City/Skyboxes/Skybox_Afternoon_1A.mat");
            if (sky != null) RenderSettings.skybox = sky;

            var lightGO = new GameObject(sunset ? "Sol_Atardecer" : "Sol_Dia");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            if (sunset)
            {
                light.color = new Color(1.0f, 0.85f, 0.63f);
                light.intensity = 1.25f;
                lightGO.transform.rotation = Quaternion.Euler(27f, -38f, 0f);
                RenderSettings.ambientSkyColor = new Color(0.72f, 0.55f, 0.42f);
                RenderSettings.ambientEquatorColor = new Color(0.55f, 0.43f, 0.33f);
                RenderSettings.ambientGroundColor = new Color(0.29f, 0.22f, 0.16f);
                RenderSettings.fogColor = new Color(0.79f, 0.56f, 0.38f);
                RenderSettings.fogDensity = 0.004f;
            }
            else // día andino claro (el tutorial es de día, según el GDD)
            {
                light.color = new Color(1.0f, 0.96f, 0.88f);
                light.intensity = 1.15f;
                lightGO.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.75f);
                RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.5f);
                RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.25f);
                RenderSettings.fogColor = new Color(0.75f, 0.78f, 0.8f);
                RenderSettings.fogDensity = 0.0025f;
            }
        }

        /// <summary>
        /// Noche cerrada de la Av. Simón Bolívar: sin skybox (cielo negro azulado),
        /// luna tenue y niebla oscura. La vía la alumbran los faros del auto y
        /// los pocos postes con luz — esa soledad ES el diseño (GDD).
        /// </summary>
        public static void SetupNightAmbience()
        {
            RenderSettings.skybox = null; // color sólido de cámara = cielo nocturno

            var moonGO = new GameObject("Luna");
            var moon = moonGO.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.62f, 0.80f); // azul lunar
            moon.intensity = 0.22f;
            moon.shadows = LightShadows.Soft;
            moonGO.transform.rotation = Quaternion.Euler(50f, 160f, 0f);
            RenderSettings.sun = moon;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.12f, 0.20f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.08f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.05f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.09f);
            RenderSettings.fogDensity = 0.010f;
        }

        public static void BuildGround(Transform parent, Vector3 center, float scale, Color? color = null)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Piso";
            ground.transform.SetParent(parent);
            ground.transform.position = center;
            ground.transform.localScale = new Vector3(scale, 1f, scale);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color ?? new Color(0.33f, 0.30f, 0.22f); // por defecto: tierra seca andina
            ground.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// Muros límite INVISIBLES alrededor del área jugable (rectángulo de
        /// los nodos del grafo + margen): el jugador no puede irse a zonas no
        /// jugables y Don Pancho le avisa al toparlos (WorldBoundary).
        /// </summary>
        public static void BuildWorldLimits(RoadGraphData graph,
            float margin = 22f, float height = 12f) =>
            BuildWorldLimits(graph.Nodes.Select(n => n.Position).ToList(), margin, height);

        public static void BuildWorldLimits(IReadOnlyList<Vector3> points,
            float margin = 22f, float height = 12f)
        {
            if (!WorldLimitsMath.Bounds(points, margin, out var min, out var max)) return;

            var root = new GameObject("[LimitesMundo]").transform;
            Vector3 c = (min + max) * 0.5f;
            float sx = max.x - min.x, sz = max.z - min.z, t = 2f;

            Wall(root, "Muro_N", new Vector3(c.x, height * 0.5f, max.z + t * 0.5f), new Vector3(sx + t * 2f, height, t));
            Wall(root, "Muro_S", new Vector3(c.x, height * 0.5f, min.z - t * 0.5f), new Vector3(sx + t * 2f, height, t));
            Wall(root, "Muro_E", new Vector3(max.x + t * 0.5f, height * 0.5f, c.z), new Vector3(t, height, sz + t * 2f));
            Wall(root, "Muro_O", new Vector3(min.x - t * 0.5f, height * 0.5f, c.z), new Vector3(t, height, sz + t * 2f));
        }

        public static void Wall(Transform root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            go.AddComponent<WorldBoundary>();
        }

        // ================== El Aveo jugable ==================

        public static VehicleSpec GetOrCreateAveoSpec()
        {
            var spec = AssetDatabase.LoadAssetAtPath<VehicleSpec>(SpecPath);
            if (spec != null) return spec;

            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Vehicles"))
                AssetDatabase.CreateFolder("Assets/Data", "Vehicles");

            spec = ScriptableObject.CreateInstance<VehicleSpec>();
            spec.DisplayName = "Chevrolet Aveo 2008";
            AssetDatabase.CreateAsset(spec, SpecPath);
            return spec;
        }

        /// <summary>El prefab del auto del jugador (elegido mirando los
        /// retratos de VehiclePortraitTool; las físicas siguen siendo las
        /// del VehicleSpec del Aveo — el "taxi" es solo la carrocería).</summary>
        public const string PlayerCarPrefabName = "Car_14C";

        /// <summary>
        /// Arma el Aveo jugable completo: colliders, WheelColliders medidos sobre
        /// las ruedas del modelo, física, cámara, audio y puentes de HUD.
        /// </summary>
        public static void BuildPlayerCar(VehicleSpec spec, Vector3 spawnPos, float yawDeg = 0f)
        {
            var prefab = Load($"{TC}/Vehicles/{PlayerCarPrefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogError($"[Habla Camarón] No encuentro {PlayerCarPrefabName}.prefab para el Aveo.");
                return;
            }

            var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(car, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            car.name = "Aveo_Camaron";
            car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            foreach (var col in car.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);

            var bounds = WorldBounds(car);

            var body = car.AddComponent<BoxCollider>();
            body.center = new Vector3(bounds.center.x, bounds.center.y + 0.18f, bounds.center.z);
            body.size = new Vector3(bounds.size.x * 0.92f, bounds.size.y * 0.78f, bounds.size.z * 0.96f);

            var vc = car.AddComponent<VehicleController>(); // crea el Rigidbody
            vc.Spec = spec;

            var wheelMeshes = car.GetComponentsInChildren<Transform>()
                .Where(t => t.name.Contains("Wheel_") && (t.name.EndsWith(".L") || t.name.EndsWith(".R")))
                .ToArray();
            if (wheelMeshes.Length < 4)
            {
                Debug.LogError($"[Habla Camarón] Esperaba 4 ruedas en {PlayerCarPrefabName} y encontré {wheelMeshes.Length}.");
                return;
            }

            var wcRoot = new GameObject("WheelColliders").transform;
            wcRoot.SetParent(car.transform, false);

            var pairs = new List<WheelVisualSync.Pair>();
            foreach (var mesh in wheelMeshes)
            {
                float radius = 0.38f;
                var r = mesh.GetComponentInChildren<Renderer>();
                if (r != null) radius = r.bounds.extents.y;

                var wcGO = new GameObject($"WC_{mesh.name}");
                wcGO.transform.SetParent(wcRoot, false);
                wcGO.transform.position = mesh.position + Vector3.up * (spec.SuspensionDistance * 0.5f);

                var wc = wcGO.AddComponent<WheelCollider>();
                wc.radius = radius;
                wc.mass = 22f;
                wc.suspensionDistance = spec.SuspensionDistance;
                var spring = wc.suspensionSpring;
                spring.spring = spec.SpringForce;
                spring.damper = spec.SpringDamper;
                spring.targetPosition = 0.5f;
                wc.suspensionSpring = spring;

                bool front = mesh.name.Contains("F.");
                bool left = mesh.name.EndsWith(".L");
                if (front && left) vc.WheelFL = wc;
                else if (front) vc.WheelFR = wc;
                else if (left) vc.WheelRL = wc;
                else vc.WheelRR = wc;

                pairs.Add(new WheelVisualSync.Pair { Collider = wc, Visual = mesh });
            }

            var sync = car.AddComponent<WheelVisualSync>();
            sync.Wheels = pairs.ToArray();

            car.AddComponent<VehicleInput>();
            car.AddComponent<EngineAudio>();
            car.AddComponent<VehicleHUDBridge>();
            car.AddComponent<VehicleLights>(); // faros reales (misión nocturna)

            var cam = car.AddComponent<DriverCamera>();
            cam.EyeLocalPos = Vehicle.CabinEyeMath.EyeLocalFor(bounds);

            car.transform.SetPositionAndRotation(spawnPos, Quaternion.Euler(0f, yawDeg, 0f));
        }

        // ================== Build Settings ==================

        public static void AddToBuildSettings(string scenePath)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == scenePath)) return;
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
