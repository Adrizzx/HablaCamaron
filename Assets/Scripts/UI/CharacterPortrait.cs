using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.UI
{
    /// <summary>Quién es quién en los retratos.</summary>
    public enum Character { DonPancho, Mishel }

    /// <summary>
    /// LAS CARAS de Don Pancho y Mishel, dibujadas POR CÓDIGO con uGUI (regla
    /// del proyecto: nada se arma a mano y no hay assets de arte). Cada retrato
    /// es un círculo de piel con cabello, ojos, cejas, boca y los rasgos que
    /// identifican al personaje: Don Pancho lleva sombrero de paño y bigote
    /// tusa; Mishel, melena larga y aretes. Se usa en las burbujas del HUD, en
    /// el briefing y en el chat.
    /// </summary>
    public static class CharacterPortrait
    {
        // Paleta de los personajes (armoniza con UITheme: atardecer quiteño).
        private static readonly Color PielPancho = Hex("C98A5E");
        private static readonly Color PielMishel = Hex("E0A97B");
        private static readonly Color PeloOscuro = Hex("2B1B12");
        private static readonly Color Sombrero = Hex("6B4A2F");
        private static readonly Color CintaSombrero = Hex("3A2718");
        private static readonly Color Arete = Hex("F5B95E");

        // A partir de este tamaño se considera un retrato "grande" (el briefing,
        // 205 px): ahí se luce la ILUSTRACIÓN COMPLETA en vez de solo la cara
        // (HUD 68 px y chat 58 px siguen usando la cara recortada en círculo).
        private const float TamanoRetratoCompleto = 150f;

        /// <summary>
        /// Crea el retrato circular del personaje, de lado `size` px, como hijo
        /// de parent. Devuelve la raíz (RectTransform) para posicionarla.
        /// Si hay una ilustración real importada (`PortraitLibrary`), la usa
        /// (cara recortada o ilustración completa según `size`); si no, dibuja
        /// el retrato por código como siempre (fallback, regla del proyecto).
        /// </summary>
        public static RectTransform Create(Transform parent, Character who, float size)
        {
            bool grande = size >= TamanoRetratoCompleto;
            Sprite foto;
            bool hayFoto = grande ? PortraitLibrary.TryFull(who, out foto) : PortraitLibrary.TryFace(who, out foto);
            if (hayFoto)
                return CreateFoto(parent, who, foto, size);

            var root = new GameObject($"Retrato_{who}", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(size, size);

            bool pancho = who == Character.DonPancho;
            Color piel = pancho ? PielPancho : PielMishel;

            // Disco de fondo (marco cálido) + borde dorado.
            var marco = Circle(rt, "Marco", UITheme.PanelDark, 1f, Vector2.zero);
            var borde = marco.gameObject.AddComponent<Outline>();
            borde.effectColor = UITheme.A(UITheme.Gold, 0.55f);
            borde.effectDistance = new Vector2(1.5f, -1.5f);

            // Melena de Mishel: un disco grande DETRÁS de la cara.
            if (!pancho)
                Circle(rt, "Pelo", PeloOscuro, 0.92f, new Vector2(0f, -0.02f * size));

            // La cara.
            var cara = Circle(rt, "Cara", piel, 0.72f, new Vector2(0f, -0.02f * size));

            // Ojos y cejas.
            float ojoY = 0.03f * size, ojoX = 0.13f * size;
            Oval(rt, "OjoIzq", PeloOscuro, new Vector2(-ojoX, ojoY), new Vector2(0.09f, 0.10f) * size);
            Oval(rt, "OjoDer", PeloOscuro, new Vector2(ojoX, ojoY), new Vector2(0.09f, 0.10f) * size);
            Oval(rt, "CejaIzq", PeloOscuro, new Vector2(-ojoX, ojoY + 0.10f * size),
                new Vector2(0.14f, 0.035f) * size);
            Oval(rt, "CejaDer", PeloOscuro, new Vector2(ojoX, ojoY + 0.10f * size),
                new Vector2(0.14f, 0.035f) * size);

            if (pancho)
            {
                // Bigote tusa (dos brochas bajo la nariz) y sombrero de paño.
                Oval(rt, "Bigote", PeloOscuro, new Vector2(0f, -0.13f * size),
                    new Vector2(0.34f, 0.07f) * size);
                // Ala del sombrero + copa.
                Oval(rt, "AlaSombrero", Sombrero, new Vector2(0f, 0.27f * size),
                    new Vector2(0.86f, 0.11f) * size);
                Oval(rt, "CopaSombrero", Sombrero, new Vector2(0f, 0.36f * size),
                    new Vector2(0.48f, 0.16f) * size);
                Oval(rt, "CintaSombrero", CintaSombrero, new Vector2(0f, 0.30f * size),
                    new Vector2(0.50f, 0.05f) * size);
                // Boca serena (línea).
                Oval(rt, "Boca", Hex("7A3B2E"), new Vector2(0f, -0.22f * size),
                    new Vector2(0.16f, 0.035f) * size);
            }
            else
            {
                // Flequillo, sonrisa y aretes.
                Oval(rt, "Flequillo", PeloOscuro, new Vector2(0f, 0.22f * size),
                    new Vector2(0.66f, 0.20f) * size);
                Oval(rt, "Boca", Hex("B4574C"), new Vector2(0f, -0.17f * size),
                    new Vector2(0.20f, 0.06f) * size);
                Circle(rt, "AreteIzq", Arete, 0.09f, new Vector2(-0.33f * size, -0.06f * size));
                Circle(rt, "AreteDer", Arete, 0.09f, new Vector2(0.33f * size, -0.06f * size));
            }
            return rt;
        }

        /// <summary>
        /// Retrato con la ILUSTRACIÓN real: mismo marco con borde dorado que el
        /// dibujo por código, pero recortada en círculo con una `Mask` de uGUI
        /// (patrón estándar: Image circular como máscara, `showMaskGraphic`
        /// apagado, la foto como hijo que llena el rect).
        /// </summary>
        private static RectTransform CreateFoto(Transform parent, Character who, Sprite foto, float size)
        {
            var root = new GameObject($"Retrato_{who}", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(size, size);

            // Mismo marco + borde dorado que el retrato dibujado.
            var marco = Circle(rt, "Marco", UITheme.PanelDark, 1f, Vector2.zero);
            var borde = marco.gameObject.AddComponent<Outline>();
            borde.effectColor = UITheme.A(UITheme.Gold, 0.55f);
            borde.effectDistance = new Vector2(1.5f, -1.5f);

            // Máscara circular (mismo sprite de círculo que usa el resto de la UI).
            var mascara = Circle(rt, "Mascara", Color.white, 1f, Vector2.zero);
            var mask = mascara.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // La foto llena la máscara.
            var fotoGO = new GameObject("Foto", typeof(RectTransform));
            fotoGO.transform.SetParent(mascara, false);
            var fotoRt = (RectTransform)fotoGO.transform;
            fotoRt.anchorMin = Vector2.zero;
            fotoRt.anchorMax = Vector2.one;
            fotoRt.offsetMin = Vector2.zero;
            fotoRt.offsetMax = Vector2.zero;
            var fotoImg = fotoGO.AddComponent<Image>();
            fotoImg.sprite = foto;
            fotoImg.raycastTarget = false;

            return rt;
        }

        /// <summary>El nombre que se muestra junto al retrato.</summary>
        public static string DisplayName(Character who) =>
            who == Character.DonPancho ? "Don Pancho" : "Mishel";

        // ---------------- primitivas de dibujo ----------------

        private static RectTransform Circle(RectTransform parent, string name, Color c,
            float fraction, Vector2 offset)
        {
            float d = parent.sizeDelta.x * fraction;
            return Oval(parent, name, c, offset, new Vector2(d, d));
        }

        /// <summary>Óvalo: el sprite circular de UIFactory estirado a `size`
        /// (un círculo achatado es una ceja, un bigote o un ala de sombrero).</summary>
        private static RectTransform Oval(RectTransform parent, string name, Color c,
            Vector2 offset, Vector2 size)
        {
            var img = UIFactory.Circle(name, parent, c, size.x);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size; // x ≠ y ⇒ óvalo
            return rt;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
