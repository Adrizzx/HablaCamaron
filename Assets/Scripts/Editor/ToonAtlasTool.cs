using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using static HablaCamaron.EditorTools.ToonCityKit;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// ATLAS del paquete Toon City (reconocimiento ANTES de adaptar la ciudad,
    /// patrón del repo: medir, nunca a ojo): renderiza cada pieza de vía en
    /// cenital (Logs/snaps/roads/*.png + Logs/roads-atlas.txt con bounds) y el
    /// mapa completo de Demo_Scene_1 en mosaico (Logs/snaps/demo1/) con el
    /// inventario de instancias (Logs/demo1-inventario.txt). Corre en batch.
    /// </summary>
    public static class ToonAtlasTool
    {
        [MenuItem("Habla Camarón/Atlas · Piezas de vía (PNG)")]
        public static void SnapRoadCatalog()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Logs/snaps/roads");
            BuildSun();

            var log = new StringBuilder("pieza | tamaño (x × y × z)\n");
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { $"{TC}/Roads" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var b = WorldBounds(go);
                log.AppendLine($"{prefab.name} | {b.size.x:0.0} × {b.size.y:0.0} × {b.size.z:0.0}");
                ShootTopDown(b, $"Logs/snaps/roads/{prefab.name}.png", 512);
                Object.DestroyImmediate(go);
            }
            File.WriteAllText("Logs/roads-atlas.txt", log.ToString());
            Debug.Log("[Habla Camarón] Atlas de piezas listo en Logs/snaps/roads/");
        }

        [MenuItem("Habla Camarón/Atlas · Demo_Scene_1 (mapa + inventario)")]
        public static void SnapDemoScene()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Toon Series/Toon City/Scenes/Demo_Scene_1.unity", OpenSceneMode.Single);
            Directory.CreateDirectory("Logs/snaps/demo1");

            // Inventario: cada instancia raíz de prefab, con su origen y pose.
            var sb = new StringBuilder("origen | pos | yaw\n");
            var total = new Bounds(Vector3.zero, Vector3.zero);
            bool boundsInit = false;
            int roads = 0, otros = 0;

            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (src == null) continue;
                    string srcPath = AssetDatabase.GetAssetPath(src);

                    if (srcPath.Contains("/Roads/"))
                    {
                        roads++;
                        sb.AppendLine($"{src.name} | {t.position.x:0.0},{t.position.y:0.0},{t.position.z:0.0} | {t.eulerAngles.y:0}");
                    }
                    else otros++;
                }

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!boundsInit) { total = r.bounds; boundsInit = true; }
                else total.Encapsulate(r.bounds);
            }

            sb.Insert(0, $"RESUMEN: {roads} piezas de vía, {otros} otras instancias.\n" +
                         $"BOUNDS: centro {total.center}, tamaño {total.size}\n\n");
            File.WriteAllText("Logs/demo1-inventario.txt", sb.ToString());

            // Mosaico cenital 3×3 del mapa completo.
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var tile = new Bounds(
                        total.center + new Vector3(
                            (i - 1) * total.size.x / 3f, 0f, (j - 1) * total.size.z / 3f),
                        new Vector3(total.size.x / 3f, total.size.y, total.size.z / 3f));
                    ShootTopDown(tile, $"Logs/snaps/demo1/tile_{i}_{j}.png", 1024);
                }
            ShootTopDown(total, "Logs/snaps/demo1/mapa_completo.png", 2048);
            Debug.Log($"[Habla Camarón] Demo_Scene_1: {roads} piezas de vía. Mapa en Logs/snaps/demo1/");
        }

        [MenuItem("Habla Camarón/Atlas · Grafo de la Ciudad Toon (diagnóstico)")]
        public static void SnapCiudadGraph()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/N1_CiudadToon.unity",
                OpenSceneMode.Single);
            Physics.SyncTransforms();
            var data = RoadScanKit.Scan(scene, out var ports, out var pairs, out string report);
            Debug.Log($"[Habla Camarón] Diagnóstico: {report}");

            // Marcadores: verde = puerto cosido, ROJO = puerto suelto (ahí se
            // corta la red), cian chico = nodo de carril.
            var matched = new HashSet<int>();
            foreach (var (a, b) in pairs) { matched.Add(a); matched.Add(b); }

            // Los CASI-emparejamientos: para cada puerto suelto, su candidato
            // opuesto más cercano y el desglose de la distancia (la evidencia
            // de por qué la red se fragmenta).
            var casi = new List<(float d, string txt)>();
            for (int i = 0; i < ports.Count; i++)
            {
                if (matched.Contains(i)) continue;
                float best = float.MaxValue;
                int quien = -1;
                for (int j = 0; j < ports.Count; j++)
                {
                    if (i == j || ports[i].PieceId == ports[j].PieceId) continue;
                    if (Vector3.Dot(ports[i].Outward, ports[j].Outward) > -0.7f) continue;
                    float d = Vector3.Distance(ports[i].Position, ports[j].Position);
                    if (d < best) { best = d; quien = j; }
                }
                if (quien < 0 || best > 12f) continue;
                Vector3 delta = ports[quien].Position - ports[i].Position;
                casi.Add((best, $"suelto en {ports[i].Position} → candidato a {best:0.00} m " +
                                $"(Δ {delta.x:0.00},{delta.y:0.00},{delta.z:0.00})"));
            }
            casi.Sort((x, y) => x.d.CompareTo(y.d));
            for (int i = 0; i < Mathf.Min(casi.Count, 30); i++)
                Debug.Log($"[CasiCostura] {casi[i].txt}");
            Debug.Log($"[CasiCostura] total de sueltos con candidato <12 m: {casi.Count}");
            var root = new GameObject("MarcadoresDiag").transform;
            var verde = Mat(Color.green);
            var rojo = Mat(Color.red);
            for (int i = 0; i < ports.Count; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(s.GetComponent<Collider>());
                s.transform.SetParent(root, false);
                s.transform.position = ports[i].Position + Vector3.up * 3f;
                bool ok = matched.Contains(i);
                s.transform.localScale = Vector3.one * (ok ? 2.5f : 6f);
                s.GetComponent<Renderer>().sharedMaterial = ok ? verde : rojo;
            }

            var total = new Bounds(Vector3.zero, Vector3.zero);
            bool init = false;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!init) { total = r.bounds; init = true; } else total.Encapsulate(r.bounds);
            }
            System.IO.Directory.CreateDirectory("Logs/snaps/demo1");
            ShootTopDown(total, "Logs/snaps/demo1/grafo_diag.png", 2048);
            Object.DestroyImmediate(root.gameObject); // solo diagnóstico: NO se guarda
            Debug.Log("[Habla Camarón] Diagnóstico del grafo en Logs/snaps/demo1/grafo_diag.png");
        }

        [MenuItem("Habla Camarón/Atlas · Ciudad Toon de cerca (verificación)")]
        public static void SnapCiudadCloseups()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/N1_CiudadToon.unity",
                OpenSceneMode.Single);
            Directory.CreateDirectory("Logs/snaps/demo1");

            // Un retrato a nivel de calle de cada elemento agregado.
            Shoot("Aveo_Camaron", "ciudad_spawn.png", 9f, 2.2f);
            Shoot("[MetaExamen]", "ciudad_meta.png", 14f, 3f);
            Shoot("PasoCebra", "ciudad_cebra.png", 8f, 2.4f);
            Shoot("Semaforo", "ciudad_semaforo.png", 8f, 2f);
            Shoot("Parque", "ciudad_parque.png", 16f, 4f);
            Debug.Log("[Habla Camarón] Retratos de la ciudad en Logs/snaps/demo1/");
        }

        /// <summary>Foto del primer objeto con ese nombre, desde un ojo cercano.</summary>
        private static void Shoot(string objName, string file, float dist, float alto)
        {
            GameObject target = null;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == objName || go.name.StartsWith(objName)) { target = go; break; }
            if (target == null)
            {
                Debug.LogWarning($"[Habla Camarón] No encontré '{objName}' para retratar.");
                return;
            }

            var camGO = new GameObject("CamRetrato");
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            Vector3 c = target.transform.position + Vector3.up * 1.2f;
            cam.transform.position = c + new Vector3(dist * 0.7f, alto, dist * 0.7f);
            cam.transform.LookAt(c);

            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes($"Logs/snaps/demo1/{file}", tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
        }

        private static Material Mat(Color c) =>
            new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = c };

        [MenuItem("Habla Camarón/Atlas · Zona Sur y su cima (verificación)")]
        public static void SnapZonaSurTop()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/N1_ZonaSur.unity", OpenSceneMode.Single);
            Directory.CreateDirectory("Logs/snaps");
            Physics.SyncTransforms();

            // Encuadre de la vía (sin el skyline lejano).
            var total = new Bounds(Vector3.zero, Vector3.zero);
            bool init = false;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.transform.position.magnitude > 150f) continue;
                if (!init) { total = r.bounds; init = true; } else total.Encapsulate(r.bounds);
            }
            ShootTopDown(total, "Logs/snaps/zonasur_cenital_nuevo.png", 1600);

            // La CIMA: el redondel de retorno del nivel 2, de cerca y medido.
            var plaza = GameObject.Find("Cima_RedondelRetorno");
            if (plaza == null) { Debug.LogWarning("[Habla Camarón] No hallé la plaza de la cima."); return; }
            var b = new Bounds(plaza.transform.position, new Vector3(46f, 6f, 46f));
            ShootTopDown(b, "Logs/snaps/zonasur_cima_retorno.png", 1024);
            // Radio real de la plaza, medido de su propia calzada.
            var calz = plaza.transform.Find("Calzada");
            _ultimoRadio = calz != null ? calz.GetComponent<Renderer>().bounds.extents.x : 15f;

            // ¿Hay escalón o vacío? Se mide la CALZADA (el anillo por donde se
            // rueda, no el centro: ahí está el árbol de la isla) en 8 puntos,
            // y se compara con la vía del brazo justo antes de entrar.
            // La calzada se mide de SU PROPIA malla (un raycast desde arriba
            // choca con la copa del árbol de la isla y miente por 12 m).
            var calzTr = plaza.transform.Find("Calzada");
            float calzadaY = calzTr.GetComponent<Renderer>().bounds.max.y;

            // La vía del brazo, justo antes de entrar, informando QUÉ se midió.
            Vector3 c = plaza.transform.position;
            Vector3 pVia = c - plaza.transform.forward * (_ultimoRadio + 4f);
            string queVia = "nada";
            float via = float.NaN;
            if (Physics.Raycast(pVia + Vector3.up * 40f, Vector3.down, out var hv, 90f))
            { via = hv.point.y; queVia = hv.collider.name; }

            Debug.Log($"[CimaDiag] calzada y={calzadaY:0.00} | vía del brazo y={via:0.00} " +
                      $"({queVia}) | escalón={Mathf.Abs(calzadaY - via):0.00} m " +
                      $"(debería ser ~0)");
        }

        private static float _ultimoRadio = 15f;

        // ---------------- Helpers de render ----------------

        private static void BuildSun()
        {
            var sun = new GameObject("Sol_Atlas").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
        }

        /// <summary>Render cenital ortográfico de unos bounds a PNG.</summary>
        private static void ShootTopDown(Bounds b, string file, int px)
        {
            var go = new GameObject("CamAtlas");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.z, 1f) * 1.05f;
            cam.transform.position = b.center + Vector3.up * (b.size.y + 60f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = b.size.y + 160f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.13f, 0.15f);

            var rt = new RenderTexture(px, px, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(px, px, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, px, px), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
    }
}
