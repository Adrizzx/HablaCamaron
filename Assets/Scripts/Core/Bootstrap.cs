using UnityEngine;

namespace HablaCamaron.Core
{
    /// <summary>
    /// Garantiza que GameManager y SceneLoader existan, sin importar en qué escena
    /// arranques (útil al probar escenas sueltas en el editor).
    /// Se ejecuta automáticamente antes de cargar la primera escena.
    /// No hay que ponerlo en ningún GameObject: el atributo lo dispara solo.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (Object.FindFirstObjectByType<GameManager>() == null)
            {
                var go = new GameObject("[GameManager]");
                go.AddComponent<GameManager>();
            }

            if (Object.FindFirstObjectByType<SceneLoader>() == null)
            {
                var go = new GameObject("[SceneLoader]");
                go.AddComponent<SceneLoader>();
            }

            // La música de menús (Fase 4) también vive entre escenas.
            if (Object.FindFirstObjectByType<MusicDirector>() == null)
            {
                var go = new GameObject("[MusicDirector]");
                go.AddComponent<MusicDirector>();
            }
        }
    }
}
