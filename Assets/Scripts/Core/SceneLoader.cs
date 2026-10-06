using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HablaCamaron.Core
{
    /// <summary>
    /// Maneja toda la navegación entre escenas con un fundido a negro.
    /// Es la pieza que implementa el "flujo de navegación" de la rúbrica (criterio 3).
    /// Crea su propio Canvas de fade por código, así que no hay que armar nada en el editor.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Fundido")]
        public float FadeDuration = 0.4f;

        private Image _fadeImage;
        private Canvas _canvas;

        // Nombres de las escenas. Deben coincidir con los archivos .unity
        // y estar agregadas en Build Settings (File > Build Settings).
        public const string SCENE_SPLASH = "Splash";
        public const string SCENE_MAIN_MENU = "MainMenu";
        public const string SCENE_CAMPAIGN = "CampaignMap";
        public const string SCENE_GAMEPLAY = "Gameplay";
        public const string SCENE_GARAGE = "Garage";
        public const string SCENE_CREDITS = "Credits";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildFadeCanvas();
        }

        // Construye un Canvas overlay con una imagen negra a pantalla completa.
        private void BuildFadeCanvas()
        {
            var canvasGO = new GameObject("FadeCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999; // siempre encima de todo
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var imgGO = new GameObject("FadeImage");
            imgGO.transform.SetParent(canvasGO.transform, false);
            _fadeImage = imgGO.AddComponent<Image>();
            _fadeImage.color = new Color(0.13f, 0.10f, 0.07f, 0f); // café muy oscuro, transparente
            _fadeImage.raycastTarget = false;

            var rt = _fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Carga una escena con fundido. Llama esto desde cualquier botón.</summary>
        public void Load(string sceneName)
        {
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            yield return Fade(1f);  // a negro
            yield return SceneManager.LoadSceneAsync(sceneName);
            yield return Fade(0f);  // de vuelta
        }

        private IEnumerator Fade(float targetAlpha)
        {
            if (_fadeImage == null) yield break;
            _fadeImage.raycastTarget = true;
            float start = _fadeImage.color.a;
            float t = 0f;
            while (t < FadeDuration)
            {
                t += Time.unscaledDeltaTime; // unscaled: funciona aunque el juego esté en pausa
                float a = Mathf.Lerp(start, targetAlpha, t / FadeDuration);
                var c = _fadeImage.color; c.a = a; _fadeImage.color = c;
                yield return null;
            }
            var cc = _fadeImage.color; cc.a = targetAlpha; _fadeImage.color = cc;
            _fadeImage.raycastTarget = targetAlpha > 0.5f;
        }

        public void QuitGame()
        {
            Debug.Log("Saliendo del juego...");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
