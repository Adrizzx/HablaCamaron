using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HablaCamaron.Core;

namespace HablaCamaron.UI
{
    /// <summary>Resultado de una misión con su desglose por categoría.</summary>
    public struct EvaluationResult
    {
        public int missionId;
        public int totalScore;
        public int scoreObjective;  // /30
        public int scoreSignals;    // /20
        public int scoreTechnique;  // /20
        public int scoreDefensive;  // /15
        public int scoreTime;       // /10
        public int scoreVehicle;    // /5
        public string donPanchoMessage;
    }

    /// <summary>
    /// PANTALLA 11 — EVALUACIÓN 0-100. Overlay con desglose animado.
    /// Aprobado = total >= 70. Se invoca con EvaluationScreen.Show(result).
    /// </summary>
    public class EvaluationScreen : MonoBehaviour
    {
        private EvaluationResult _r;
        private Text _totalText;
        private Image _ring;
        private readonly System.Collections.Generic.List<(RectTransform fill, int value, int max)> _bars = new();

        public static EvaluationScreen Show(EvaluationResult result)
        {
            var go = new GameObject("EvaluationScreen");
            var ctrl = go.AddComponent<EvaluationScreen>();
            ctrl._r = result;
            ctrl.Build();
            return ctrl;
        }

        private void Build()
        {
            var canvasGO = new GameObject("EvalCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            var root = canvas.transform;

            bool passed = _r.totalScore >= 70;
            MusicDirector.Instance?.PlayVerdict(passed); // sting + música de veredicto

            BackgroundBuilder.BuildSunsetBackground(root, withSun: false);
            UIFactory.Panel("Dim", root, new Color(0.05f, 0.03f, 0.02f, 0.62f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ---- Encabezado ----
            var kicker = UIFactory.Label("Kicker", root, "EVALUACI\u00d3N DE LA MISI\u00d3N", 22, UITheme.TextMuted,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.SetRect(kicker.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -44), new Vector2(900, 28), new Vector2(0.5f, 1));
            var verdict = UIFactory.Label("Verdict", root,
                passed ? "\u00a1Habla, camar\u00f3n!" : "Casi, mijo. Otra vuelta.", 46,
                passed ? UITheme.Gold : UITheme.TextSand, TextAnchor.UpperCenter, FontStyle.BoldAndItalic);
            UIFactory.AddDropShadow(verdict.gameObject, 0.5f, -3f);
            UIFactory.SetRect(verdict.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -78), new Vector2(1000, 60), new Vector2(0.5f, 1));

            // Sello APROBADO / REPROBADO.
            var stamp = UIFactory.Pill("Stamp", root, passed ? "APROBADO" : "REPROBADO",
                passed ? UITheme.Success : UITheme.Danger, UITheme.TextCream, 22);
            UIFactory.SetRect(stamp.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -146), new Vector2(220, 42), new Vector2(0.5f, 1));

            // ---- Tarjeta del puntaje (izquierda) ----
            var scoreCard = UIFactory.GradientCard("ScoreCard", root, UITheme.PanelWarm, UITheme.PanelDark,
                UITheme.RadiusLg);
            UIFactory.SetRect(scoreCard.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-470, 20), new Vector2(420, 460));

            var sh = UIFactory.Label("SHdr", scoreCard, "PUNTAJE FINAL", 20, UITheme.Gold,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.SetRect(sh.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -26), new Vector2(360, 26), new Vector2(0.5f, 1));

            var ringBg = UIFactory.Circle("RingBG", scoreCard, UITheme.A(UITheme.GroundDeep, 0.7f), 280);
            UIFactory.SetRect(ringBg.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 18), new Vector2(280, 280), new Vector2(0.5f, 0.5f));
            var ringGO = new GameObject("Ring", typeof(RectTransform));
            ringGO.transform.SetParent(ringBg.transform, false);
            _ring = ringGO.AddComponent<Image>();
            _ring.sprite = ringBg.sprite;
            _ring.color = passed ? UITheme.SuccessLight : UITheme.AccentTop;
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _ring.fillClockwise = true;
            _ring.fillAmount = 0f;
            _ring.raycastTarget = false;
            var rrt = (RectTransform)ringGO.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = new Vector2(8, 8); rrt.offsetMax = new Vector2(-8, -8);
            var inner = UIFactory.Circle("Inner", ringBg.transform, UITheme.GroundDeep, 228);
            UIFactory.SetRect(inner.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            inner.rectTransform.offsetMin = new Vector2(26, 26); inner.rectTransform.offsetMax = new Vector2(-26, -26);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            _totalText = UIFactory.Label("Total", inner.transform, "0", 92,
                passed ? UITheme.SuccessLight : UITheme.AccentTop, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.FitTextInside(_totalText, 92, 48);
            UIFactory.SetRect(_totalText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 6), new Vector2(220, 100), new Vector2(0.5f, 0.5f));
            var of = UIFactory.Label("OutOf", inner.transform, "/ 100", 20, UITheme.TextMuted,
                TextAnchor.MiddleCenter, FontStyle.Normal);
            UIFactory.FitTextInside(of, 20, 14);
            UIFactory.SetRect(of.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -50), new Vector2(220, 24), new Vector2(0.5f, 0.5f));

            // Estrellas (70 = 1, 80 = 2, 90 = 3).
            int stars = _r.totalScore >= 90 ? 3 : _r.totalScore >= 80 ? 2 : _r.totalScore >= 70 ? 1 : 0;
            var starRow = UIFactory.Label("Stars", scoreCard, MakeStars(stars), 38, UITheme.Gold,
                TextAnchor.MiddleCenter, FontStyle.Normal);
            UIFactory.SetRect(starRow.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 30), new Vector2(300, 46), new Vector2(0.5f, 0));

            // ---- Desglose (derecha) ----
            var card = UIFactory.GradientCard("Breakdown", root, UITheme.PanelWarm, UITheme.PanelDark,
                UITheme.RadiusLg);
            UIFactory.SetRect(card.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(300, 20), new Vector2(760, 460));

            var bh = UIFactory.Label("BHdr", card, "DESGLOSE", 20, UITheme.Gold,
                TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetRect(bh.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, -24), new Vector2(300, 26), new Vector2(0, 1));

            float y = -68f;
            AddRow(card, "Cumplimiento del objetivo", _r.scoreObjective, 30, ref y);
            AddRow(card, "Respeto de se\u00f1ales", _r.scoreSignals, 20, ref y);
            AddRow(card, "T\u00e9cnica de manejo", _r.scoreTechnique, 20, ref y);
            AddRow(card, "Conducci\u00f3n defensiva", _r.scoreDefensive, 15, ref y);
            AddRow(card, "Tiempo", _r.scoreTime, 10, ref y);
            AddRow(card, "Estado del veh\u00edculo", _r.scoreVehicle, 5, ref y);

            // ---- Mensaje de Don Pancho (burbuja con avatar) ----
            if (!string.IsNullOrEmpty(_r.donPanchoMessage))
            {
                var bubble = UIFactory.GradientCard("DonBubble", root, UITheme.A(UITheme.PanelDark, 0.85f),
                    UITheme.A(UITheme.GroundDeep, 0.85f), UITheme.RadiusMd);
                UIFactory.SetRect(bubble.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(36, 150), new Vector2(1040, 92));
                var av = UIFactory.GlowCircle("Av", bubble, UITheme.Gold, 64);
                UIFactory.SetRect(av, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(46, 0), new Vector2(64, 64), new Vector2(0.5f, 0.5f));
                var face = UIFactory.Label("Face", bubble, "\U0001F468\u200D\U0001F527", 30, UITheme.GroundDeep,
                    TextAnchor.MiddleCenter, FontStyle.Normal);
                UIFactory.FitTextInside(face, 30, 18);
                UIFactory.SetRect(face.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(46, 0), new Vector2(54, 54), new Vector2(0.5f, 0.5f));
                var msg = UIFactory.Label("DonMsg", bubble, "\u201C" + _r.donPanchoMessage + "\u201D", 23,
                    UITheme.TextSand, TextAnchor.MiddleLeft, FontStyle.Italic);
                msg.horizontalOverflow = HorizontalWrapMode.Wrap;
                UIFactory.SetRect(msg.gameObject, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
                msg.rectTransform.offsetMin = new Vector2(96, 8); msg.rectTransform.offsetMax = new Vector2(-20, -8);
            }

            // Botones.
            if (passed)
            {
                var cont = UIFactory.GradientButton("Continuar", root, OnContinue, true, true, 28);
                UIFactory.SetRect(cont, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(160, 60), new Vector2(300, 70), new Vector2(0.5f, 0));
                var retry = UIFactory.GradientButton("Reintentar", root, Retry, false, true, 24);
                UIFactory.SetRect(retry, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-160, 60), new Vector2(260, 70), new Vector2(0.5f, 0));
            }
            else
            {
                var retry = UIFactory.GradientButton("Reintentar", root, Retry, true, true, 28);
                UIFactory.SetRect(retry, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(0, 60), new Vector2(320, 70), new Vector2(0.5f, 0));
            }

            StartCoroutine(Animate());
        }

        private static string MakeStars(int filled)
        {
            string s = "";
            for (int i = 0; i < 3; i++) s += (i < filled ? "\u2605" : "\u2606") + "  ";
            return s.TrimEnd();
        }

        private void AddRow(Transform parent, string label, int value, int max, ref float y)
        {
            var name = UIFactory.Label("L_" + label, parent, label, 22, UITheme.TextCream,
                TextAnchor.MiddleLeft, FontStyle.Normal);
            UIFactory.SetRect(name.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, y), new Vector2(320, 32), new Vector2(0, 1));

            var barBg = UIFactory.Panel("Bar_" + label, parent, UITheme.A(UITheme.GroundDeep, 0.6f),
                new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero, 6);
            UIFactory.SetRect(barBg.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(370, y - 4), new Vector2(260, 18), new Vector2(0, 1));

            var fill = UIFactory.Panel("Fill", barBg, Color.white,
                new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero, 6);
            fill.GetComponent<Image>().raycastTarget = false;
            fill.gameObject.AddComponent<VerticalGradient>().SetColors(UITheme.Gold, UITheme.AccentTop);
            fill.anchorMax = new Vector2(0, 1); fill.sizeDelta = new Vector2(0, 0);
            _bars.Add((fill, value, max));

            var pts = UIFactory.Label("P_" + label, parent, value + "/" + max, 22, UITheme.Gold,
                TextAnchor.MiddleRight, FontStyle.Bold);
            UIFactory.SetRect(pts.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(650, y), new Vector2(80, 32), new Vector2(0, 1));

            y -= 62f;
        }

        // OJO: MissionRunner congela el juego (timeScale = 0) antes de mostrar
        // esta pantalla, así que TODA la animación usa tiempo sin escalar.
        private IEnumerator Animate()
        {
            // Anillo + puntaje total.
            float dur = 0.8f, t = 0f;
            float targetFill = Mathf.Clamp01(_r.totalScore / 100f);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                _ring.fillAmount = Mathf.Lerp(0, targetFill, k);
                _totalText.text = Mathf.RoundToInt(Mathf.Lerp(0, _r.totalScore, k)).ToString();
                yield return null;
            }
            _ring.fillAmount = targetFill;
            _totalText.text = _r.totalScore.ToString();

            // Barras escalonadas.
            foreach (var (fill, value, max) in _bars)
            {
                float bt = 0f; const float bdur = 0.4f;
                float target = max > 0 ? (float)value / max : 0f;
                while (bt < bdur)
                {
                    bt += Time.unscaledDeltaTime;
                    fill.anchorMax = new Vector2(Mathf.Lerp(0, target, bt / bdur), 1);
                    fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
                    yield return null;
                }
                fill.anchorMax = new Vector2(target, 1);
                fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        // El resultado ya lo registró MissionRunner.Finish (única fuente de
        // verdad): aquí solo se navega.
        private void OnContinue() => SceneLoader.Instance?.Load(SceneLoader.SCENE_CAMPAIGN);

        private void Retry()
        {
            Time.timeScale = 1f;
            // Reintentar recarga la escena de zona con ESTA misión activa
            // (varias misiones comparten zona: sin la clave se jugaría otra).
            PlayerPrefs.SetInt(Missions.MissionCatalog.KEY_CURRENT, _r.missionId);
            PlayerPrefs.Save();
            string scene = Missions.MissionCatalog.SceneFor(_r.missionId) ?? SceneLoader.SCENE_GAMEPLAY;
            SceneManager.LoadScene(scene);
        }
    }
}
