using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.UI
{
    /// <summary>
    /// Sistema de diseño centralizado. TODA la UI lee de aquí: colores, radios,
    /// tamaños. Cambiar un color aquí lo cambia en todas las pantallas.
    /// Principios: un solo acento (terracota), una escala de esquinas, jerarquía clara.
    /// </summary>
    public static class UITheme
    {
        // ---- Cielos / fondos degradados ----
        public static readonly Color SkyTop    = Hex("33263A"); // morado andino (alto del cielo)
        public static readonly Color SkyMid    = Hex("5A3B43"); // transición
        public static readonly Color SkyWarm   = Hex("8A4F42"); // terracota cielo
        public static readonly Color SkyAmber  = Hex("C16E4A"); // naranja atardecer
        public static readonly Color SkyGlow   = Hex("E0915A"); // ámbar bajo
        public static readonly Color GroundDark = Hex("3A2618"); // tierra/base oscura
        public static readonly Color GroundDeep = Hex("1A1109"); // sombra inferior

        // ---- Superficies UI ----
        public static readonly Color PanelWarm = Hex("4A3828"); // tarjeta clara
        public static readonly Color PanelDark = Hex("302216"); // tarjeta oscura

        // ---- Acentos ----
        public static readonly Color AccentTop  = Hex("C9603F"); // terracota botón (arriba)
        public static readonly Color AccentBot  = Hex("A8472E"); // terracota botón (abajo)
        public static readonly Color AccentEdge = Hex("D6764F"); // borde luminoso primario
        public static readonly Color Gold       = Hex("F5B95E"); // dorado/ámbar (secundario, glows)
        public static readonly Color GoldLight  = Hex("FFD98A"); // dorado claro (highlights, sol)
        public static readonly Color GoldDeep   = Hex("D6943F"); // dorado oscuro

        // ---- Texto ----
        public static readonly Color TextCream  = Hex("FFF6E9"); // texto principal cálido
        public static readonly Color TextSand   = Hex("FBEFDD"); // texto secundario
        public static readonly Color TextSoft   = Hex("D9C0A4"); // texto terciario
        public static readonly Color TextMuted  = Hex("B89878"); // labels apagados
        public static readonly Color TextLocked = Hex("7E6342"); // contenido bloqueado

        // ---- Semánticos ----
        public static readonly Color Success      = Hex("5E7A4F"); // verde aprobado
        public static readonly Color SuccessLight = Hex("7FA968"); // verde check
        public static readonly Color Danger       = Hex("C24A3A"); // rojo error/calado

        // ---- Compatibilidad con scripts previos (alias) ----
        public static readonly Color BgDark    = Hex("241913");
        public static readonly Color BgPanel   = Hex("302216");
        public static readonly Color Surface   = Hex("4A3828");
        public static readonly Color SurfaceHi = Hex("5A4636");
        public static readonly Color Border     = Hex("4A3829");
        public static readonly Color Accent      = Hex("C9603F");
        public static readonly Color AccentPress = Hex("8A3C2E");
        public static readonly Color Ocre        = Hex("F5B95E");
        public static readonly Color Cream       = Hex("FFF6E9");
        public static readonly Color CreamSoft   = Hex("FBEFDD");
        public static readonly Color Muted       = Hex("B89878");

        // ---- Escala de esquinas (una sola, coherente) ----
        public const float RadiusMd = 10f; // botones / tarjetas
        public const float RadiusLg = 14f; // paneles / modales

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var c);
            return c;
        }

        /// <summary>Versión de un color con alpha distinto (helper corto).</summary>
        public static Color A(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        /// <summary>Fuente por defecto de Unity (cámbiala por una .ttf importada para más carácter).</summary>
        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Aplica el set de colores de un botón primario (acento) o secundario.</summary>
        public static void StyleButton(Button btn, Image bg, bool primary, bool interactable = true)
        {
            bg.color = primary ? AccentTop : A(PanelWarm, 0.72f);
            var cb = btn.colors;
            cb.normalColor      = primary ? AccentTop : A(PanelWarm, 0.72f);
            cb.highlightedColor = primary ? AccentBot : PanelWarm;
            cb.pressedColor     = primary ? GoldDeep : PanelDark;
            cb.selectedColor    = primary ? AccentTop : A(PanelWarm, 0.72f);
            cb.disabledColor    = new Color(PanelWarm.r, PanelWarm.g, PanelWarm.b, 0.35f);
            cb.fadeDuration     = 0.10f;
            btn.colors = cb;
            btn.interactable = interactable;
        }
    }
}
