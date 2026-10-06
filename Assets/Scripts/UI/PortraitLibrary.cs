using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.UI
{
    /// <summary>
    /// LAS ILUSTRACIONES reales de Don Pancho y Mishel (`Assets/Resources/Portraits/`,
    /// importadas como Sprite por `PortraitImportTool`), cargadas con
    /// `Resources.Load` y CACHEADAS (regla del proyecto: nada de recargar por
    /// frame). Si el asset no está disponible — por ejemplo en un test EditMode
    /// sin esas Resources garantizadas — `TryFace`/`TryFull` devuelven `false` y
    /// `CharacterPortrait` cae al retrato dibujado por código: la foto es un
    /// lujo, nunca una dependencia dura.
    /// </summary>
    public static class PortraitLibrary
    {
        // Ruta (dentro de Resources) de la ilustración completa por personaje.
        private static readonly Dictionary<Character, string> RutaCompleto = new Dictionary<Character, string>
        {
            { Character.DonPancho, "Portraits/donpancho_completo" },
            { Character.Mishel, "Portraits/mishel_telefono" },
        };

        /// <summary>Recorte de CARA dentro de la ilustración, como fracciones del
        /// ancho/alto (convención Unity: y=0 abajo de la textura, y=1 arriba).</summary>
        private struct RecorteCara
        {
            public float CenterX, CenterY, Lado;
        }

        // Afinado mirando los PNG reales:
        // - Don Pancho ("donpancho_completo.png"): retrato de cuerpo con la cara
        //   Y el sombrero arriba, ligeramente a la izquierda del centro.
        // - Mishel ("mishel_telefono.png"): retrato de cuerpo con la cara arriba,
        //   casi centrada (la melena se reparte pareja a los dos lados).
        // "Lado" es fracción del ALTO (el límite natural en un retrato vertical);
        // como el PNG tiene píxeles cuadrados, el recorte en píxeles sale cuadrado.
        private static readonly Dictionary<Character, RecorteCara> Caras = new Dictionary<Character, RecorteCara>
        {
            { Character.DonPancho, new RecorteCara { CenterX = 0.375f, CenterY = 0.70f, Lado = 0.25f } },
            { Character.Mishel, new RecorteCara { CenterX = 0.47f, CenterY = 0.79f, Lado = 0.28f } },
        };

        /// <summary>
        /// Rectángulo (en píxeles de la textura) donde cae la cara, PURA —
        /// sin tocar Resources, para poder testearla sin assets importados.
        /// </summary>
        public static Rect FaceRectFor(Character who, int texW, int texH)
        {
            var r = Caras[who];
            float lado = r.Lado * texH;
            float cx = r.CenterX * texW;
            float cy = r.CenterY * texH;
            return new Rect(cx - lado * 0.5f, cy - lado * 0.5f, lado, lado);
        }

        // Cachés: un solo intento de Resources.Load por personaje (incluido el
        // caso "no existe", para no reintentar Resources.Load cada llamada).
        private static readonly Dictionary<Character, Texture2D> _texCache = new Dictionary<Character, Texture2D>();
        private static readonly Dictionary<Character, Sprite> _caraCache = new Dictionary<Character, Sprite>();
        private static readonly Dictionary<Character, Sprite> _completoCache = new Dictionary<Character, Sprite>();

        private static Texture2D Textura(Character who)
        {
            if (_texCache.TryGetValue(who, out var cached)) return cached;

            Texture2D tex = null;
            if (RutaCompleto.TryGetValue(who, out var ruta))
                tex = Resources.Load<Texture2D>(ruta);
            _texCache[who] = tex;
            return tex;
        }

        /// <summary>La CARA recortada en círculo (HUD, chat). false si no hay ilustración.</summary>
        public static bool TryFace(Character who, out Sprite cara)
        {
            if (_caraCache.TryGetValue(who, out cara) && cara != null) return true;

            var tex = Textura(who);
            if (tex == null) { cara = null; return false; }

            var rect = FaceRectFor(who, tex.width, tex.height);
            cara = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            _caraCache[who] = cara;
            return true;
        }

        /// <summary>La ilustración COMPLETA (briefing, "que se luzca"). false si no hay ilustración.</summary>
        public static bool TryFull(Character who, out Sprite completo)
        {
            if (_completoCache.TryGetValue(who, out completo) && completo != null) return true;

            var tex = Textura(who);
            if (tex == null) { completo = null; return false; }

            completo = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            _completoCache[who] = completo;
            return true;
        }
    }
}
