using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HablaCamaron.AI;
using static HablaCamaron.EditorTools.ToonCityKit;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// FASE 2 — Inyecta el [TrafficManager] con los autos de Toon City en las
    /// tres escenas de zona. Sin esto el tráfico igual funciona (el bootstrap
    /// crea uno con autos-caja de colores), pero con esto los NPCs son taxis,
    /// busetas y particulares de verdad.
    /// Menú: Habla Camarón > 6 · Inyectar tráfico NPC en las zonas (Fase 2)
    /// </summary>
    public static class TrafficInjectionTool
    {
        private static readonly string[] ZoneScenes =
        {
            "Assets/Scenes/N1_ZonaSur.unity",
            "Assets/Scenes/N1_SimonBolivar.unity",
            "Assets/Scenes/N1_CorredorExamen.unity",
            "Assets/Scenes/N1_CiudadToon.unity",   // la ciudad del examen (menú 8)
            "Assets/Scenes/N2_QuitoCiudad.unity",  // la ciudad completa (mejoras)
        };

        private static readonly string[] CarNames =
        {
            "Car_3B", "Car_4D", "Car_6D", "Car_7D", "Car_8D",
            "Car_10C", "Car_14C", "Car_15B", "Car_16A", "Car_17A",
        };

        /// <summary>El eje largo del vehículo, medido de la instancia real.</summary>
        private static float PrefabLength(GameObject prefab)
        {
            var temp = (GameObject)Object.Instantiate(prefab);
            var rends = temp.GetComponentsInChildren<Renderer>();
            var b = rends.Length > 0 ? rends[0].bounds
                : new Bounds(temp.transform.position, Vector3.one * 2f);
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Object.DestroyImmediate(temp);
            return Mathf.Max(b.size.x, b.size.z);
        }

        /// <summary>
        /// Inyecta la flota en la escena ABIERTA (sin abrir ni guardar nada).
        /// La llaman los builders al final de generar su zona: regenerar una
        /// escena borraba el [TrafficManager] inyectado y el tráfico volvía a
        /// ser cubos de colores hasta que alguien se acordara de correr el
        /// menú 6 — un paso manual olvidable que ya se cobró un playtest.
        /// </summary>
        public static void InjectIntoOpenScene(string scenePath)
        {
            var tm = Object.FindFirstObjectByType<TrafficManager>();
            if (tm == null)
                tm = new GameObject("[TrafficManager]").AddComponent<TrafficManager>();

            // Flota por zona (playtest 2026-07-14): los vehículos LARGOS se
            // bugueaban en los redondeles (cortaban el anillo y quedaban
            // trabados). Se mide cada prefab y NpcFleetRules decide.
            bool redondel = NpcFleetRules.ZoneHasRoundabout(scenePath);
            tm.CarPrefabs.Clear();
            int fuera = 0;
            foreach (var name in CarNames)
            {
                var prefab = Load($"{TC}/Vehicles/{name}.prefab");
                if (prefab == null) continue;
                if (!NpcFleetRules.Fits(redondel, PrefabLength(prefab))) { fuera++; continue; }
                tm.CarPrefabs.Add(prefab);
            }
            EditorUtility.SetDirty(tm);
            Debug.Log($"[Habla Camarón] Tráfico inyectado: {tm.CarPrefabs.Count} modelos " +
                      $"({fuera} descartados por largo).");
        }

        [MenuItem("Habla Camarón/6 · Inyectar tráfico NPC en las zonas (Fase 2)")]
        public static void Inject()
        {
            int done = 0;
            foreach (var scenePath in ZoneScenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    Debug.LogWarning($"[Habla Camarón] Escena no encontrada: {scenePath} " +
                                     "(genera la zona con su menú primero).");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                var tm = Object.FindFirstObjectByType<TrafficManager>();
                if (tm == null)
                    tm = new GameObject("[TrafficManager]").AddComponent<TrafficManager>();

                // Flota por zona (playtest 2026-07-14): los vehículos LARGOS se
                // bugueaban en los redondeles (cortaban el anillo y quedaban
                // trabados). Se mide cada prefab y NpcFleetRules decide.
                bool redondel = NpcFleetRules.ZoneHasRoundabout(scenePath);
                tm.CarPrefabs.Clear();
                foreach (var name in CarNames)
                {
                    var prefab = Load($"{TC}/Vehicles/{name}.prefab");
                    if (prefab == null) continue;
                    float largo = PrefabLength(prefab);
                    if (!NpcFleetRules.Fits(redondel, largo))
                    {
                        Debug.Log($"[Habla Camarón] {name} ({largo:0.0} m) queda FUERA de " +
                                  $"{System.IO.Path.GetFileNameWithoutExtension(scenePath)}: " +
                                  "muy largo para el redondel.");
                        continue;
                    }
                    tm.CarPrefabs.Add(prefab);
                }
                EditorUtility.SetDirty(tm);

                EditorSceneManager.SaveScene(scene);
                done++;
            }

            EditorUtility.DisplayDialog("Habla Camarón — Fase 2",
                $"Tráfico NPC inyectado en {done} zona(s) con " +
                $"{CarNames.Length} modelos de Toon City.\n\n" +
                "En Play: los NPCs circulan por el grafo (A*), respetan (o no,\n" +
                "según el perfil) los semáforos, y F9 muestra su estado FSM.\n" +
                "Selecciona un NPC en la Hierarchy para ver su ruta A* en magenta.", "¡A rodar!");
        }
    }
}
