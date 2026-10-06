using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>
    /// El "clima" de la ciudad grande EN RUNTIME: la misma escena enorme sirve
    /// de día para unas misiones y de NOCHE para otras (la misión nocturna del
    /// GDD). Así no hace falta duplicar una ciudad de 29 MB por cada momento
    /// del día — MissionRunner la pone en modo noche si la misión lo pide.
    /// </summary>
    public static class CityMood
    {
        /// <summary>Deja la ciudad de noche: sol lunar tenue, ambiente frío y
        /// niebla oscura (los faros del Aveo pasan a ser imprescindibles).</summary>
        public static void ApplyNight()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.20f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.11f);
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.skybox = null;

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                l.color = new Color(0.55f, 0.62f, 0.85f); // luz de luna
                l.intensity = 0.22f;
                l.transform.rotation = Quaternion.Euler(38f, 205f, 0f);
            }
        }
    }
}
