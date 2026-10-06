using System;
using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.UI
{
    /// <summary>
    /// Fábrica de elementos de UI. Métodos cortos para crear paneles, textos y botones
    /// sin repetir código. Todas las pantallas (menú, opciones, mapa) usan esto,
    /// así se ven consistentes y Claude Code puede armar pantallas nuevas rápido.
    /// </summary>
    public static class UIFactory
    {
        public static RectTransform Panel(string name, Transform parent, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            float radius = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            if (radius > 0f) ApplyRoundedSprite(img, radius);
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        public static Text Label(string name, Transform parent, string content, int size,
            Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = UITheme.DefaultFont;
            txt.fontSize = size;
            txt.color = color;
            txt.alignment = anchor;
            txt.fontStyle = style;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        /// <summary>
        /// Impide que un texto contenido (botón, píldora o círculo) se salga
        /// de su figura. Conserva el tamaño solicitado como techo y solo reduce.
        /// </summary>
        public static void FitTextInside(Text text, int maxFontSize, int minFontSize = 12)
        {
            if (text == null) return;
            var range = TextContainmentMath.FontRange(minFontSize, maxFontSize);
            text.fontSize = range.max;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = range.min;
            text.resizeTextMaxSize = range.max;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignByGeometry = true;
        }

        /// <summary>Crea un botón estilizado con label. Devuelve el GameObject para posicionarlo.</summary>
        public static GameObject Button(string label, Transform parent, Action onClick,
            bool primary, bool interactable = true, int fontSize = 26)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            ApplyRoundedSprite(img, UITheme.RadiusMd);

            var btn = go.AddComponent<Button>();
            UITheme.StyleButton(btn, img, primary, interactable);
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txt = Label("Label", go.transform, label, fontSize,
                interactable ? UITheme.Cream : UITheme.Muted,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12, 6); trt.offsetMax = new Vector2(-12, -6);
            FitTextInside(txt, fontSize, Mathf.Max(11, fontSize / 2));

            return go;
        }

        public static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size, Vector2? pivot = null)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        // ======================= Estilo videojuego (gradientes) =======================

        /// <summary>
        /// Botón con gradiente vertical, borde luminoso, sombra proyectada y texto con sombra.
        /// Primario = terracota (AccentTop→AccentBot); secundario = vidrio cálido (PanelWarm).
        /// </summary>
        public static GameObject GradientButton(string label, Transform parent, Action onClick,
            bool primary, bool interactable = true, int fontSize = 26)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            ApplyRoundedSprite(img, UITheme.RadiusMd);
            img.color = Color.white;

            var grad = go.AddComponent<VerticalGradient>();
            if (primary) grad.SetColors(UITheme.AccentTop, UITheme.AccentBot);
            else grad.SetColors(UITheme.A(UITheme.PanelWarm, 0.72f), UITheme.A(UITheme.PanelDark, 0.72f));

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            cb.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            cb.disabledColor = new Color(1, 1, 1, 0.4f);
            cb.fadeDuration = 0.10f;
            btn.colors = cb;
            btn.interactable = interactable;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            // Borde luminoso.
            var outline = go.AddComponent<Outline>();
            outline.effectColor = primary ? UITheme.AccentEdge : new Color(0.84f, 0.59f, 0.35f, 0.4f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Sombra proyectada.
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.45f);
            shadow.effectDistance = new Vector2(0, -4);

            var txt = Label("Label", go.transform, label, fontSize,
                interactable ? UITheme.TextCream : UITheme.TextMuted,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            var tShadow = txt.gameObject.AddComponent<Shadow>();
            tShadow.effectColor = new Color(0, 0, 0, 0.5f);
            tShadow.effectDistance = new Vector2(0, -2);
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12, 6); trt.offsetMax = new Vector2(-12, -6);
            FitTextInside(txt, fontSize, Mathf.Max(11, fontSize / 2));

            return go;
        }

        /// <summary>Tarjeta con gradiente vertical y brillo interno superior simulado.</summary>
        public static RectTransform GradientCard(string name, Transform parent, Color top, Color bottom,
            float radius = UITheme.RadiusLg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            ApplyRoundedSprite(img, radius);
            img.color = Color.white;
            var grad = go.AddComponent<VerticalGradient>();
            grad.SetColors(top, bottom);

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.4f);
            shadow.effectDistance = new Vector2(0, -6);

            var rt = img.rectTransform;

            // Brillo interno superior (línea fina blanca translúcida).
            var sheen = Panel("Sheen", rt, new Color(1, 1, 1, 0.10f),
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -6), new Vector2(-6, -2), radius);
            sheen.sizeDelta = new Vector2(sheen.sizeDelta.x, 6);
            sheen.GetComponent<Image>().raycastTarget = false;

            return rt;
        }

        /// <summary>Círculo con glow radial (soles, halos, focos). Reusa BackgroundBuilder.</summary>
        public static GameObject GlowCircle(string name, Transform parent, Color color, float size)
        {
            var go = BackgroundBuilder.GlowCircle(parent, color, size);
            go.name = name;
            return go;
        }

        /// <summary>Píldora/etiqueta compacta (chip) con texto centrado. Para zona, momento, estados.</summary>
        public static RectTransform Pill(string name, Transform parent, string text, Color bg, Color textColor,
            int fontSize = 18, FontStyle style = FontStyle.Bold)
        {
            var pill = Panel(name, parent, bg, new Vector2(0, 1), new Vector2(0, 1),
                Vector2.zero, Vector2.zero, 999f);
            var t = Label("L", pill, text, fontSize, textColor, TextAnchor.MiddleCenter, style);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(12, 4); t.rectTransform.offsetMax = new Vector2(-12, -4);
            FitTextInside(t, fontSize, Mathf.Max(10, fontSize / 2));
            t.raycastTarget = false;
            return pill;
        }

        /// <summary>Línea divisoria fina (separador horizontal) translúcida.</summary>
        public static RectTransform Divider(string name, Transform parent, Color color)
        {
            var d = Panel(name, parent, color, new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero, 0f);
            d.GetComponent<Image>().raycastTarget = false;
            return d;
        }

        /// <summary>Añade una sombra suave (proyectada) a cualquier Graphic.</summary>
        public static void AddDropShadow(GameObject go, float alpha = 0.45f, float dy = -5f)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha);
            s.effectDistance = new Vector2(0, dy);
        }

        /// <summary>Añade contorno luminoso a cualquier Graphic.</summary>
        public static Outline AddGlowEdge(GameObject go, Color color, float dist = 2f)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(dist, -dist);
            return o;
        }

        /// <summary>Círculo sólido (sin glow): para fichas, badges, avatares.</summary>
        public static Image Circle(string name, Transform parent, Color color, float size)
        {
            if (_circleCache == null) _circleCache = GenerateCircleSprite(64);
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = _circleCache;
            img.color = color;
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            return img;
        }

        /// <summary>
        /// Círculo con un texto centrado y un margen interno proporcional. Es el
        /// patrón obligatorio para números, letras y checks dentro de círculos.
        /// </summary>
        public static (Image circle, Text label) LabeledCircle(string name, Transform parent,
            string content, Color circleColor, Color textColor, float size, int fontSize,
            FontStyle style = FontStyle.Bold)
        {
            size = Mathf.Max(24f, size);
            var circle = Circle(name, parent, circleColor, size);
            circle.preserveAspect = true;
            var mask = circle.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var label = Label("Label", circle.transform, content, fontSize, textColor,
                TextAnchor.MiddleCenter, style);
            float inset = TextContainmentMath.CircleInset(size);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(inset, inset);
            label.rectTransform.offsetMax = new Vector2(-inset, -inset);
            label.raycastTarget = false;
            FitTextInside(label, fontSize, Mathf.Max(10, fontSize / 2));
            return (circle, label);
        }

        private static Sprite _circleCache;
        private static Sprite GenerateCircleSprite(int r)
        {
            int size = r * 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = r;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half));
                    float a = Mathf.Clamp01(half - d + 0.5f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // Genera (una vez) un sprite con esquinas redondeadas y lo aplica como 9-slice.
        // Así los paneles/botones tienen el mismo radio sin importar su tamaño.
        private static Sprite _roundedCache;
        private static void ApplyRoundedSprite(Image img, float radius)
        {
            if (_roundedCache == null) _roundedCache = GenerateRoundedSprite(32);
            img.sprite = _roundedCache;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 32f / radius; // ajusta el radio visual
        }

        private static Sprite GenerateRoundedSprite(int r)
        {
            int size = r * 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(r - x, x - (size - r), 0);
                    float dy = Mathf.Max(r - y, y - (size - r), 0);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - dist + 0.5f); // antialias del borde
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            var border = new Vector4(r, r, r, r);
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }

    /// <summary>Cálculos puros de contención tipográfica.</summary>
    public static class TextContainmentMath
    {
        public static (int min, int max) FontRange(int requestedMin, int requestedMax)
        {
            int max = Mathf.Max(1, requestedMax);
            int min = Mathf.Clamp(requestedMin, 1, max);
            return (min, max);
        }

        public static float CircleInset(float diameter)
        {
            // 15 % por lado mantiene hasta las esquinas de un glifo lejos del
            // borde curvo; nunca deja menos de 3 px de aire.
            return Mathf.Max(3f, Mathf.Max(0f, diameter) * 0.15f);
        }
    }
}
