using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.Core;

namespace HablaCamaron.UI
{
    /// <summary>
    /// PANTALLA 1 — SPLASH. Pantalla de carga inicial con identidad del juego.
    /// Estilo videojuego: fondo atardecer + sol + viñeta, barra de carga animada.
    /// Al completar la barra O pulsar cualquier tecla → MainMenu.
    /// Pon este script en un GameObject vacío de la escena "Splash" (índice 0 en Build Settings).
    /// </summary>
    public class SplashController : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _barFill;
        private bool _done;

        private void Start()
        {
            BuildCanvas();
            BuildScreen();
            StartCoroutine(LoadRoutine());
        }

        private void BuildCanvas()
        {
            var go = new GameObject("SplashCanvas");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
        }

        private void BuildScreen()
        {
            var root = _canvas.transform;
            BackgroundBuilder.BuildSunsetBackground(root);

            // Ícono de auto (placeholder con emoji/texto grande).
            var icon = UIFactory.Label("Icon", root, "\U0001F697", 140, UITheme.SkyGlow,
                TextAnchor.MiddleCenter, FontStyle.Normal);
            UIFactory.SetRect(icon.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 180), new Vector2(400, 200));

            // Título dos tonos.
            var t1 = UIFactory.Label("Title1", root, "\u00a1Habla,", 64, UITheme.TextCream,
                TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            UIFactory.SetRect(t1.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-150, 40), new Vector2(500, 90));
            var t2 = UIFactory.Label("Title2", root, "Camar\u00f3n!", 64, UITheme.Gold,
                TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            UIFactory.SetRect(t2.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(190, 40), new Vector2(520, 90));

            // Barra de carga.
            var barBg = UIFactory.Panel("BarBG", root, UITheme.PanelDark,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 4);
            UIFactory.SetRect(barBg.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -80), new Vector2(240, 6));

            _barFill = UIFactory.Panel("BarFill", barBg, Color.white,
                new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero, 4);
            _barFill.anchorMax = new Vector2(0, 1);
            _barFill.pivot = new Vector2(0, 0.5f);
            _barFill.sizeDelta = new Vector2(0, 0);
            var fillGrad = _barFill.gameObject.AddComponent<VerticalGradient>();
            fillGrad.SetColors(UITheme.Gold, UITheme.AccentTop);

            var loading = UIFactory.Label("Loading", root, "Cargando entorno de Quito...", 22,
                UITheme.TextMuted, TextAnchor.MiddleCenter, FontStyle.Italic);
            UIFactory.SetRect(loading.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -120), new Vector2(600, 30));

            var footer = UIFactory.Label("Footer", root,
                "Universidad de las Fuerzas Armadas ESPE  \u00b7  Pulsa cualquier tecla para saltar", 18,
                UITheme.TextSoft, TextAnchor.LowerCenter, FontStyle.Normal);
            UIFactory.SetRect(footer.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 40), new Vector2(1100, 30));
        }

        private IEnumerator LoadRoutine()
        {
            const float duration = 2.5f;
            float t = 0f;
            while (t < duration && !Input.anyKeyDown)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                _barFill.anchorMax = new Vector2(p, 1);
                _barFill.offsetMin = Vector2.zero;
                _barFill.offsetMax = Vector2.zero;
                yield return null;
            }
            GoToMenu();
        }

        private void GoToMenu()
        {
            if (_done) return;
            _done = true;
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.Load(SceneLoader.SCENE_MAIN_MENU);
        }
    }
}
