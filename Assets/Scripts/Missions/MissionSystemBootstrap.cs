using UnityEngine;
using UnityEngine.SceneManagement;
using HablaCamaron.AI;
using HablaCamaron.World;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// Enchufa el sistema de misiones y el tráfico SIN tocar las escenas:
    /// al cargar cualquier escena...
    ///  - restaura Time.timeScale = 1 (por si una pantalla lo dejó en 0),
    ///  - si la escena aloja una misión del catálogo → crea el MissionRunner,
    ///  - si la escena tiene RoadGraph y no hay TrafficManager → crea uno
    ///    (con autos-caja de fallback; el menú 6 inyecta los de Toon City).
    /// Mismo patrón que Core.Bootstrap: nada que armar en el editor.
    /// </summary>
    public static class MissionSystemBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Setup(SceneManager.GetActiveScene()); // la escena inicial ya cargó
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Setup(scene);

        private static void Setup(Scene scene)
        {
            // Guardia global: ninguna pantalla debe dejar el juego congelado
            // ni el audio amordazado (veredicto y pausa silencian el gameplay).
            Time.timeScale = 1f;
            Core.GameAudioSettings.GameplayMuted = false;
            AudioListener.pause = false;

            // Varias misiones comparten zona: hc_current_mission decide cuál
            // se juega (lo escriben el mapa de campaña y los reintentos).
            var def = MissionCatalog.ForScene(scene.name,
                PlayerPrefs.GetInt(MissionCatalog.KEY_CURRENT, -1));
            if (def != null && Object.FindFirstObjectByType<MissionRunner>() == null)
            {
                var go = new GameObject("[MissionRunner]");
                go.AddComponent<MissionRunner>().Def = def;
            }

            // Tráfico en toda escena con grafo de calles (misión o manejo libre).
            if (Object.FindFirstObjectByType<RoadGraph>() != null &&
                Object.FindFirstObjectByType<TrafficManager>() == null)
            {
                new GameObject("[TrafficManager]").AddComponent<TrafficManager>();
            }

            // Ambiente sonoro de la zona (Fase 4): brisa, pájaros, bocinas...
            var profile = ZoneAmbience.ProfileForScene(scene.name);
            if (profile != AmbienceProfile.None &&
                Object.FindFirstObjectByType<ZoneAmbience>() == null)
            {
                var amb = new GameObject("[ZoneAmbience]").AddComponent<ZoneAmbience>();
                amb.Profile = profile;
            }

            // Vehículo elegido en el garaje (Fase 4): si es el BT-50 y está
            // desbloqueado, se re-equipa el auto de la escena con su ficha.
            Vehicle.VehicleRoster.ApplySelectionTo(
                Object.FindFirstObjectByType<Vehicle.VehicleController>());

            FixSignTextMaterials();
        }

        /// <summary>
        /// EN RUNTIME (nada serializado): cambia el material de los TextMesh de
        /// las señales por uno Unlit transparente CON profundidad — el material
        /// builtin de fuente dibuja siempre encima y los textos se veían
        /// flotando a través de paredes y edificios.
        /// </summary>
        private static void FixSignTextMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;

            foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                var r = tm.GetComponent<MeshRenderer>();
                if (r == null || tm.font == null) continue;

                var mat = new Material(shader)
                {
                    mainTexture = tm.font.material.mainTexture,
                    color = tm.color,
                };
                mat.SetFloat("_Surface", 1f); // transparente (los glifos usan alfa)
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                r.sharedMaterial = mat;
            }
        }
    }
}
