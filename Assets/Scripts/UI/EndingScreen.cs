using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HablaCamaron.Core;

namespace HablaCamaron.UI
{
    /// <summary>
    /// PANTALLA 12 — FINALES (aprobado / reprobado). Cierre narrativo del examen final.
    /// Se invoca con EndingScreen.Show(passed[, failReason]).
    /// </summary>
    public class EndingScreen : MonoBehaviour
    {
        private bool _passed;
        private string _failReason;

        /// <summary>
        /// failReason: razón concreta de una muerte súbita (StrictRuleJudge,
        /// examen/bonus id 6-7) — opcional, solo se antepone en el final malo.
        /// Sin rediseñar la pantalla: se cuela en la banda de narrativa.
        /// </summary>
        public static EndingScreen Show(bool passed, string failReason = null)
        {
            var go = new GameObject("EndingScreen");
            var ctrl = go.AddComponent<EndingScreen>();
            ctrl._passed = passed;
            ctrl._failReason = failReason;
            ctrl.Build();
            return ctrl;
        }

        private void Build()
        {
            var canvasGO = new GameObject("EndingCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            var root = canvas.transform;

            MusicDirector.Instance?.PlayVerdict(_passed); // sting + música de veredicto
            BackgroundBuilder.BuildSunsetBackground(root, withSun: _passed);

            if (!_passed)
            {
                // Tinte frío/azulado y viñeta más fuerte.
                UIFactory.Panel("ColdTint", root, new Color(0.20f, 0.18f, 0.30f, 0.35f),
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                UIFactory.Panel("Dim", root, new Color(0.03f, 0.02f, 0.04f, 0.45f),
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }

            var group = canvasGO.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // Halo central detrás de la ilustración.
            var halo = UIFactory.GlowCircle("Halo", root,
                _passed ? UITheme.A(UITheme.GoldLight, 0.35f) : UITheme.A(UITheme.TextSoft, 0.12f), 900);
            UIFactory.SetRect(halo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 230), new Vector2(900, 700), new Vector2(0.5f, 0.5f));

            // Ilustración de cierre.
            var scene = UIFactory.Label("Scene", root, _passed ? "\U0001F697  \U0001F3B6  \U0001F389" : "\U0001F695  \U0001F326",
                100, _passed ? UITheme.SkyGlow : UITheme.A(UITheme.TextSoft, 0.8f),
                TextAnchor.MiddleCenter, FontStyle.Normal);
            UIFactory.AddDropShadow(scene.gameObject, 0.4f, -4f);
            UIFactory.SetRect(scene.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 230), new Vector2(900, 170), new Vector2(0.5f, 0.5f));

            // Kicker.
            var kicker = UIFactory.Pill("Kicker", root,
                _passed ? "EXAMEN FINAL  ·  COMPLETADO" : "EXAMEN FINAL  ·  NO SUPERADO",
                _passed ? UITheme.Success : UITheme.A(UITheme.PanelDark, 0.85f),
                _passed ? UITheme.TextCream : UITheme.TextSoft, 18);
            UIFactory.SetRect(kicker.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 110), new Vector2(360, 38), new Vector2(0.5f, 0.5f));

            // Título.
            var title = UIFactory.Label("Title", root,
                _passed ? "\u00a1Llegaste a tiempo!" : "Se hizo tarde...", 76,
                _passed ? UITheme.Gold : UITheme.TextSand, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            UIFactory.AddDropShadow(title.gameObject, 0.5f, -3f);
            UIFactory.SetRect(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 30), new Vector2(1200, 94), new Vector2(0.5f, 0.5f));

            // Banda de narrativa (tarjeta translúcida para legibilidad).
            var band = UIFactory.GradientCard("Band", root, UITheme.A(UITheme.PanelDark, 0.78f),
                UITheme.A(UITheme.GroundDeep, 0.78f), UITheme.RadiusLg);
            UIFactory.SetRect(band.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -80), new Vector2(1040, 140));
            UIFactory.AddGlowEdge(band.gameObject, UITheme.A(UITheme.Gold, 0.25f), 1.5f);

            // Muerte s\u00fabita (StrictRuleJudge): la raz\u00f3n concreta se antepone
            // al final malo, sin tocar el resto del cierre narrativo.
            string faltaPrefix = !_passed && !string.IsNullOrEmpty(_failReason)
                ? _failReason + ". " : "";
            string narrative = _passed
                ? "Recogiste a Mishel y llegaron al concierto de Sal y Mileto en el Itchimb\u00eda. Don Pancho te ve de lejos y sonr\u00ede: aprendiste a manejar en Quito."
                : faltaPrefix + "Mishel lleg\u00f3 en taxi. Te escribe: \u201Ctranquilo, lo importante es que est\u00e1s aprendiendo\u201D. Don Pancho te da una palmada: \u201Cma\u00f1ana le sale, camar\u00f3n\u201D.";
            var nar = UIFactory.Label("Narrative", band, narrative, 25, UITheme.TextSand,
                TextAnchor.MiddleCenter, FontStyle.Normal);
            nar.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetRect(nar.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            nar.rectTransform.offsetMin = new Vector2(40, 16); nar.rectTransform.offsetMax = new Vector2(-40, -16);

            // Botones.
            if (_passed)
            {
                var menu = UIFactory.GradientButton("Volver al men\u00fa", root, ToMenu, true, true, 28);
                UIFactory.SetRect(menu, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0, 120), new Vector2(360, 72), new Vector2(0.5f, 0));
            }
            else
            {
                var retry = UIFactory.GradientButton("Reintentar examen", root, RetryExam, true, true, 28);
                UIFactory.SetRect(retry, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(190, 120), new Vector2(360, 72), new Vector2(0.5f, 0));
                var menu = UIFactory.GradientButton("Volver al men\u00fa", root, ToMenu, false, true, 24);
                UIFactory.SetRect(menu, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-190, 120), new Vector2(300, 72), new Vector2(0.5f, 0));
            }

            StartCoroutine(FadeIn(group));
        }

        private IEnumerator FadeIn(CanvasGroup g)
        {
            float t = 0f; const float dur = 0.9f;
            while (t < dur) { t += Time.unscaledDeltaTime; g.alpha = Mathf.Clamp01(t / dur); yield return null; }
            g.alpha = 1f;
        }

        private void ToMenu() => SceneLoader.Instance?.Load(SceneLoader.SCENE_MAIN_MENU);

        private void RetryExam()
        {
            Time.timeScale = 1f;
            // El examen es la misión final del catálogo (nada de ids a mano).
            var exam = Missions.MissionCatalog.Final;
            PlayerPrefs.SetInt(Missions.MissionCatalog.KEY_CURRENT, exam.Id);
            PlayerPrefs.Save();
            SceneManager.LoadScene(exam.SceneName);
        }
    }
}
