using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HablaCamaron.Core;

namespace HablaCamaron.UI
{
    /// <summary>
    /// PANTALLA 10 — PAUSA. Singleton. Escape alterna mostrar/ocultar.
    /// Show(): Time.timeScale = 0. Hide(): Time.timeScale = 1.
    /// Pon el script en un GameObject de la escena Gameplay.
    /// </summary>
    public class PauseController : MonoBehaviour
    {
        public static PauseController Instance { get; private set; }

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _card;
        private OptionsPanel _options;
        private bool _visible;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
            Hide();
        }

        private void Build()
        {
            var canvasGO = new GameObject("PauseCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 80;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            _group = canvasGO.AddComponent<CanvasGroup>();
            var root = _canvas.transform;

            // Oscurecedor + viñeta.
            UIFactory.Panel("Dim", root, new Color(0.04f, 0.025f, 0.015f, 0.7f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var vig = new GameObject("Vignette", typeof(RectTransform));
            vig.transform.SetParent(root, false);
            var vrt = (RectTransform)vig.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            var vimg = vig.AddComponent<Image>();
            vimg.sprite = BackgroundBuilder.GenerateRadialSprite(
                new Color(0, 0, 0, 0), new Color(0.05f, 0.03f, 0.02f, 0.7f), 128, 1.6f);
            vimg.raycastTarget = false;

            // Tarjeta central.
            _card = UIFactory.GradientCard("Card", root, UITheme.PanelWarm, UITheme.PanelDark, UITheme.RadiusLg);
            UIFactory.SetRect(_card.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560, 560));
            var edge = _card.gameObject.AddComponent<Outline>();
            edge.effectColor = UITheme.A(UITheme.Gold, 0.4f); edge.effectDistance = new Vector2(1.5f, -1.5f);

            var title = UIFactory.Label("Title", _card, "Pausa", 48, UITheme.TextCream,
                TextAnchor.UpperCenter, FontStyle.BoldAndItalic);
            UIFactory.SetRect(title.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -40), new Vector2(400, 60), new Vector2(0.5f, 1));

            float y = -150f;
            MakeButton("Reanudar", true, Hide, ref y);
            MakeButton("Reintentar misi\u00f3n", false, Retry, ref y);
            MakeButton("Opciones", false, OpenOptions, ref y);
            var exit = MakeButton("Salir al men\u00fa", false, ExitToMenu, ref y);
            exit.GetComponentInChildren<Text>().color = UITheme.TextMuted;

            var hint = UIFactory.Label("Hint", _card, "ESC para reanudar", 18, UITheme.TextMuted,
                TextAnchor.LowerCenter, FontStyle.Italic);
            UIFactory.SetRect(hint.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 24), new Vector2(400, 26), new Vector2(0.5f, 0));

            _options = OptionsPanel.Create(root);
        }

        private GameObject MakeButton(string label, bool primary, System.Action action, ref float y)
        {
            var b = UIFactory.GradientButton(label, _card, action, primary, true, 26);
            UIFactory.SetRect(b, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(420, 70), new Vector2(0.5f, 1));
            y -= 86f;
            return b;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Con un overlay de misión abierto (briefing, evaluación o final)
                // la pausa no debe entrar: "Reanudar" pondría timeScale = 1 y
                // rompería el congelado que esas pantallas necesitan.
                if (!_visible && MissionOverlayOpen()) return;
                if (_visible) Hide(); else Show();
            }
        }

        private static bool MissionOverlayOpen() =>
            FindFirstObjectByType<MissionBriefingController>() != null ||
            FindFirstObjectByType<EvaluationScreen>() != null ||
            FindFirstObjectByType<EndingScreen>() != null;

        public void Show()
        {
            _visible = true;
            Time.timeScale = 0f;
            // La pausa CALLA el juego (el motor rugiendo era horrible): la
            // mordaza apaga el audio procedural, la pausa del listener apaga
            // los clips (bocinas NPC), y entra la música cálida del menú.
            GameAudioSettings.GameplayMuted = true;
            AudioListener.pause = true;
            MusicDirector.Instance?.SetPauseMusic(true);
            _group.alpha = 1f; _group.interactable = true; _group.blocksRaycasts = true;
            _canvas.transform.SetAsLastSibling();
            StartCoroutine(ScaleIn());
        }

        public void Hide()
        {
            _visible = false;
            Time.timeScale = 1f;
            // Reanudar: vuelve el audio del juego y la música de pausa se va.
            GameAudioSettings.GameplayMuted = false;
            AudioListener.pause = false;
            MusicDirector.Instance?.SetPauseMusic(false);
            _group.alpha = 0f; _group.interactable = false; _group.blocksRaycasts = false;
        }

        private IEnumerator ScaleIn()
        {
            float t = 0f; const float dur = 0.15f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(0.92f, 1f, t / dur);
                _card.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            _card.localScale = Vector3.one;
        }

        private void Retry()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OpenOptions() => _options?.Show();

        private void ExitToMenu()
        {
            Time.timeScale = 1f;
            SceneLoader.Instance?.Load(SceneLoader.SCENE_MAIN_MENU);
        }
    }
}
