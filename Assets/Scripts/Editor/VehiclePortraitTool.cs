using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Renderiza un retrato 3/4 de cada vehículo de Toon City para ELEGIR
    /// el taxi amarillo mirando imágenes (medir, nunca a ojo). Deja un
    /// inventario con bounds y conteo de ruedas (el jugador exige 4).
    /// </summary>
    public static class VehiclePortraitTool
    {
        private const string Dir = "Logs/snaps/vehicles";

        [MenuItem("Habla Camarón/Atlas · Retratos de vehículos (PNG)")]
        public static void RenderAll()
        {
            Directory.CreateDirectory(Dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var luz = new GameObject("Sol").AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            luz.intensity = 1.2f;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);

            var camGO = new GameObject("CamRetrato");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.16f, 0.2f);

            var guids = AssetDatabase.FindAssets("t:Prefab",
                new[] { "Assets/Toon Series/Toon City/Prefabs/Vehicles" });
            var lineas = new System.Collections.Generic.List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var b = ToonCityKit.WorldBounds(go);

                // 3/4 frontal: se ve color, frente y costado a la vez.
                var dir = new Vector3(1f, 0.55f, 1.2f).normalized;
                cam.transform.position = b.center + dir * b.size.magnitude * 1.15f;
                cam.transform.LookAt(b.center);

                int ruedas = go.GetComponentsInChildren<Transform>()
                    .Count(t => t.name.Contains("Wheel_") &&
                                (t.name.EndsWith(".L") || t.name.EndsWith(".R")));
                lineas.Add($"{prefab.name} | {b.size.x:0.0}x{b.size.y:0.0}x{b.size.z:0.0} | ruedas={ruedas}");

                Shot(cam, $"{Dir}/{prefab.name}.png");
                Object.DestroyImmediate(go);
            }
            File.WriteAllLines($"{Dir}/inventario.txt", lineas);
            Debug.Log($"[Habla Camarón] {guids.Length} retratos en {Dir}");
        }

        private static void Shot(Camera cam, string file)
        {
            var rt = new RenderTexture(512, 384, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
