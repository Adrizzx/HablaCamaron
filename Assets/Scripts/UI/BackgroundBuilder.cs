using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.UI
{
    /// <summary>
    /// Construye el fondo "atardecer quiteño" por código: degradado vertical de cielo a
    /// tierra, un sol con glow dorado y una viñeta oscura en los bordes.
    /// Todas las pantallas lo usan para verse como videojuego (no app plana).
    /// </summary>
    public static class BackgroundBuilder
    {
        private static Sprite _gradientCache;
        private static Sprite _radialSolid;   // radial blanco→transparente (para soles/glows)
        private static Sprite _radialVignette; // radial transparente centro→oscuro borde

        /// <summary>
        /// Crea el fondo completo (degradado + sol + viñeta) como hijos del canvasRoot.
        /// Devuelve el RectTransform raíz del fondo para apilar contenido encima.
        /// </summary>
        public static RectTransform BuildSunsetBackground(Transform canvasRoot, bool withSun = true)
        {
            var root = NewFullScreen("Background", canvasRoot);

            // 1) Degradado de cielo a tierra.
            var sky = NewFullScreen("SkyGradient", root);
            var skyImg = sky.gameObject.AddComponent<Image>();
            skyImg.sprite = GetGradientSprite();
            skyImg.type = Image.Type.Simple;
            skyImg.raycastTarget = false;

            // 2) Sol con glow dorado, arriba-derecha.
            if (withSun)
            {
                var sun = new GameObject("Sun", typeof(RectTransform));
                sun.transform.SetParent(root, false);
                var sunImg = sun.AddComponent<Image>();
                sunImg.sprite = GetRadialSprite();
                sunImg.color = UITheme.GoldLight;
                sunImg.raycastTarget = false;
                var srt = (RectTransform)sun.transform;
                srt.anchorMin = srt.anchorMax = new Vector2(1, 1);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = new Vector2(-320, -160);
                srt.sizeDelta = new Vector2(360, 360);
            }

            // 3) Viñeta en los bordes.
            var vig = NewFullScreen("Vignette", root);
            var vigImg = vig.gameObject.AddComponent<Image>();
            vigImg.sprite = GetVignetteSprite();
            vigImg.color = Color.white;
            vigImg.raycastTarget = false;

            return root;
        }

        /// <summary>Crea un círculo con glow radial (para soles, focos, halos).</summary>
        public static GameObject GlowCircle(Transform parent, Color color, float size)
        {
            var go = new GameObject("Glow", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = GetRadialSprite();
            img.color = color;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            return go;
        }

        // ---------------- Helpers de sprite generados por código ----------------

        private static RectTransform NewFullScreen(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Sprite GetGradientSprite()
        {
            if (_gradientCache != null) return _gradientCache;

            const int h = 256;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            // Paradas del degradado (de arriba=1 a abajo=0).
            var stops = new (float pos, Color col)[]
            {
                (1.00f, UITheme.SkyTop),
                (0.78f, UITheme.SkyMid),
                (0.60f, UITheme.SkyWarm),
                (0.42f, UITheme.SkyAmber),
                (0.28f, UITheme.SkyGlow),
                (0.14f, UITheme.GroundDark),
                (0.00f, UITheme.GroundDeep),
            };

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                Color c = SampleStops(stops, t);
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply();

            _gradientCache = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
            return _gradientCache;
        }

        private static Color SampleStops((float pos, Color col)[] stops, float t)
        {
            // stops ordenados de mayor a menor pos.
            for (int i = 0; i < stops.Length - 1; i++)
            {
                float hi = stops[i].pos;
                float lo = stops[i + 1].pos;
                if (t <= hi && t >= lo)
                {
                    float k = Mathf.InverseLerp(lo, hi, t);
                    return Color.Lerp(stops[i + 1].col, stops[i].col, k);
                }
            }
            return t > stops[0].pos ? stops[0].col : stops[stops.Length - 1].col;
        }

        /// <summary>Radial blanco opaco al centro → transparente al borde.</summary>
        private static Sprite GetRadialSprite()
        {
            if (_radialSolid != null) return _radialSolid;
            _radialSolid = GenerateRadialSprite(Color.white, new Color(1, 1, 1, 0), 128, 2.2f);
            return _radialSolid;
        }

        /// <summary>Radial transparente al centro → oscuro en los bordes (viñeta).</summary>
        private static Sprite GetVignetteSprite()
        {
            if (_radialVignette != null) return _radialVignette;
            _radialVignette = GenerateRadialSprite(
                new Color(0.08f, 0.05f, 0.024f, 0f),
                new Color(0.08f, 0.05f, 0.024f, 0.6f), 128, 1.6f, invert: true);
            return _radialVignette;
        }

        /// <summary>Genera un sprite radial reutilizable (center → edge).</summary>
        public static Sprite GenerateRadialSprite(Color center, Color edge, int size = 128,
            float falloff = 2f, bool invert = false)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float half = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float t = Mathf.Pow(d, falloff);
                    Color c = Color.Lerp(center, edge, t);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
