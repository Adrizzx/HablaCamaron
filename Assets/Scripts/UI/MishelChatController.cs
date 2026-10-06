using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.UI
{
    /// <summary>Un mensaje del chat de Mishel.</summary>
    public struct MishelMessage
    {
        public string text;
        public bool fromPlayer;
        public string time;

        public MishelMessage(string text, bool fromPlayer, string time)
        {
            this.text = text; this.fromPlayer = fromPlayer; this.time = time;
        }
    }

    /// <summary>
    /// PANTALLA 9 — CHAT DE MISHEL. Panel lateral derecho tipo teléfono (no invasivo):
    /// se desliza desde el borde y deja ver el juego detrás. Motor emocional/narrativo.
    /// Usa la paleta del juego (NO verde WhatsApp). Las burbujas entran una a una.
    /// Se invoca con MishelChatController.Show(messages).
    /// </summary>
    public class MishelChatController : MonoBehaviour
    {
        private RectTransform _content;
        private RectTransform _phone;
        private MishelMessage[] _messages;

        public static MishelChatController Show(MishelMessage[] messages)
        {
            var go = new GameObject("MishelChat");
            var ctrl = go.AddComponent<MishelChatController>();
            ctrl._messages = messages;
            ctrl.Build();
            return ctrl;
        }

        private void Build()
        {
            var canvasGO = new GameObject("ChatCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            var root = canvas.transform;

            // Velo MUY tenue solo para enfocar (deja ver el juego detrás).
            UIFactory.Panel("Scrim", root, new Color(0.04f, 0.02f, 0.02f, 0.22f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Teléfono docado a la derecha (se desliza desde fuera).
            const float phoneW = 420f;
            _phone = UIFactory.GradientCard("Phone", root, UITheme.PanelWarm, UITheme.PanelDark,
                UITheme.RadiusLg);
            _phone.anchorMin = new Vector2(1, 0.5f); _phone.anchorMax = new Vector2(1, 0.5f);
            _phone.pivot = new Vector2(1, 0.5f);
            _phone.sizeDelta = new Vector2(phoneW, 860);
            _phone.anchoredPosition = new Vector2(phoneW + 60, 0); // fuera de pantalla
            UIFactory.AddGlowEdge(_phone.gameObject, UITheme.A(UITheme.Gold, 0.45f), 2f);

            // Header con avatar.
            var header = UIFactory.Panel("Header", _phone, UITheme.A(UITheme.GroundDeep, 0.6f),
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero, UITheme.RadiusLg);
            UIFactory.SetRect(header.gameObject, new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            header.sizeDelta = new Vector2(0, 96); header.anchoredPosition = new Vector2(0, -48);
            header.pivot = new Vector2(0.5f, 0.5f);
            header.offsetMin = new Vector2(10, header.offsetMin.y); header.offsetMax = new Vector2(-10, header.offsetMax.y);

            var av = UIFactory.GlowCircle("Av", header, UITheme.Gold, 64);
            UIFactory.SetRect(av, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(46, 0), new Vector2(64, 64), new Vector2(0.5f, 0.5f));
            // La CARA de Mishel, dibujada por código (antes un emoji genérico).
            var face = CharacterPortrait.Create(header, Character.Mishel, 58f);
            UIFactory.SetRect(face.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(46, 0), new Vector2(58, 58), new Vector2(0.5f, 0.5f));
            var nm = UIFactory.Label("Name", header,
                CharacterPortrait.DisplayName(Character.Mishel), 26, UITheme.TextCream,
                TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetRect(nm.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(92, 8), new Vector2(220, 30), new Vector2(0, 0.5f));
            var dot = UIFactory.Circle("Dot", header, UITheme.SuccessLight, 12);
            UIFactory.SetRect(dot.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(96, -18), new Vector2(12, 12), new Vector2(0, 0.5f));
            var st = UIFactory.Label("Status", header, "en l\u00ednea", 17, UITheme.SuccessLight,
                TextAnchor.LowerLeft, FontStyle.Normal);
            UIFactory.SetRect(st.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(114, -28), new Vector2(200, 22), new Vector2(0, 0.5f));

            // Contenedor de mensajes.
            _content = UIFactory.Panel("Content", _phone, new Color(0, 0, 0, 0),
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, 0);
            _content.offsetMin = new Vector2(18, 104); _content.offsetMax = new Vector2(-18, -110);

            // Botón Continuar.
            var cont = UIFactory.GradientButton("Continuar", _phone, Dismiss, true, true, 24);
            UIFactory.SetRect(cont, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 28), new Vector2(320, 60), new Vector2(0.5f, 0));

            StartCoroutine(SlideIn());
            StartCoroutine(RevealMessages());
        }

        private IEnumerator SlideIn()
        {
            Vector2 from = _phone.anchoredPosition;
            Vector2 to = new Vector2(-30, 0);
            float t = 0f; const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Pow(1f - t / dur, 3f); // ease-out cúbico
                _phone.anchoredPosition = Vector2.Lerp(from, to, k);
                yield return null;
            }
            _phone.anchoredPosition = to;
        }

        private void Dismiss() => StartCoroutine(SlideOutAndClose());

        private IEnumerator SlideOutAndClose()
        {
            Vector2 from = _phone.anchoredPosition;
            Vector2 to = new Vector2(_phone.sizeDelta.x + 60, 0);
            float t = 0f; const float dur = 0.25f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _phone.anchoredPosition = Vector2.Lerp(from, to, t / dur);
                yield return null;
            }
            Destroy(gameObject);
        }

        private IEnumerator RevealMessages()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            float y = -10f;
            foreach (var m in _messages)
            {
                yield return new WaitForSecondsRealtime(0.55f);
                y -= BuildBubble(m, y) + 16f;
            }
        }

        // Devuelve la altura de la burbuja creada.
        private float BuildBubble(MishelMessage m, float y)
        {
            int len = m.text.Length;
            float h = 56f + Mathf.Floor(len / 30f) * 24f;

            var bubble = UIFactory.Panel("Bubble", _content, Color.white,
                new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero, UITheme.RadiusMd);
            var grad = bubble.gameObject.AddComponent<VerticalGradient>();
            if (m.fromPlayer) grad.SetColors(UITheme.AccentTop, UITheme.AccentBot);
            else grad.SetColors(UITheme.A(UITheme.PanelWarm, 0.95f), UITheme.A(UITheme.PanelDark, 0.95f));
            UIFactory.AddDropShadow(bubble.gameObject, 0.3f, -3f);

            float w = 280f;
            bubble.pivot = new Vector2(m.fromPlayer ? 1f : 0f, 1f);
            bubble.anchorMin = bubble.anchorMax = new Vector2(m.fromPlayer ? 1f : 0f, 1f);
            bubble.anchoredPosition = new Vector2(m.fromPlayer ? -4f : 4f, y);
            bubble.sizeDelta = new Vector2(w, h);

            var txt = UIFactory.Label("Text", bubble, m.text, 19, UITheme.TextCream,
                TextAnchor.UpperLeft, FontStyle.Normal);
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetRect(txt.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            txt.rectTransform.offsetMin = new Vector2(16, 24); txt.rectTransform.offsetMax = new Vector2(-16, -12);

            var time = UIFactory.Label("Time", bubble, m.time, 13, UITheme.A(UITheme.TextSand, 0.7f),
                TextAnchor.LowerRight, FontStyle.Normal);
            UIFactory.SetRect(time.gameObject, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 6), new Vector2(0, 16), new Vector2(0.5f, 0));
            time.rectTransform.offsetMin = new Vector2(12, 6); time.rectTransform.offsetMax = new Vector2(-12, 22);

            // Animación de entrada (fade + slide).
            var cg = bubble.gameObject.AddComponent<CanvasGroup>();
            StartCoroutine(FadeSlide(bubble, cg));
            return h;
        }

        private IEnumerator FadeSlide(RectTransform rt, CanvasGroup cg)
        {
            Vector2 to = rt.anchoredPosition;
            Vector2 from = to + new Vector2(0, -20);
            float t = 0f; const float dur = 0.2f;
            cg.alpha = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = t / dur;
                cg.alpha = k;
                rt.anchoredPosition = Vector2.Lerp(from, to, k);
                yield return null;
            }
            cg.alpha = 1f; rt.anchoredPosition = to;
        }
    }
}
