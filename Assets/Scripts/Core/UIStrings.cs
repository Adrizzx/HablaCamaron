using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.Core
{
    /// <summary>Idiomas disponibles (el toggle de Opciones guarda su índice).</summary>
    public enum Lang { Es = 0, En = 1 }

    /// <summary>
    /// Textos de la interfaz por idioma. PURO y testeado: el toggle de Opciones
    /// guardaba `opt_lang` pero NADIE lo leía — cambiar el idioma no hacía nada
    /// (playtest: "que funcione cuando guardes algún cambio"). Ahora las
    /// pantallas piden sus textos aquí y el cambio se ve al instante.
    /// Fallback: si falta una traducción, devuelve el español (nunca la clave
    /// cruda en pantalla).
    /// </summary>
    public static class UIStrings
    {
        /// <summary>Clave PlayerPrefs del idioma (0 = ES, 1 = EN).</summary>
        public const string KEY_LANG = "opt_lang";

        private static readonly Dictionary<string, (string es, string en)> Table = new()
        {
            // --- Opciones ---
            ["opt.title"] = ("Opciones", "Settings"),
            ["opt.audio"] = ("AUDIO POR CAPA", "AUDIO LAYERS"),
            ["opt.music"] = ("Música", "Music"),
            ["opt.sfx"] = ("Efectos (motor, ciudad)", "Effects (engine, city)"),
            ["opt.voice"] = ("Voz de Don Pancho", "Don Pancho's voice"),
            ["opt.driving"] = ("CONDUCCIÓN", "DRIVING"),
            ["opt.pedals"] = ("Sensibilidad de pedales", "Pedal sensitivity"),
            ["opt.lang"] = ("Idioma", "Language"),
            ["opt.reset"] = ("Restablecer", "Reset"),
            ["opt.save"] = ("Guardar cambios", "Save changes"),
            ["opt.saved"] = ("✓ Cambios guardados", "✓ Changes saved"),
            // --- Menú principal ---
            ["menu.new"] = ("Nueva partida", "New game"),
            ["menu.continue"] = ("Continuar", "Continue"),
            ["menu.options"] = ("Opciones", "Settings"),
            ["menu.credits"] = ("Créditos", "Credits"),
            ["menu.quit"] = ("Salir", "Quit"),
        };

        /// <summary>El idioma guardado (por defecto español).</summary>
        public static Lang Current
        {
            get => (Lang)PlayerPrefs.GetInt(KEY_LANG, 0);
            set { PlayerPrefs.SetInt(KEY_LANG, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>Texto de una clave en el idioma dado. PURA (testeada).</summary>
        public static string Get(string key, Lang lang)
        {
            if (!Table.TryGetValue(key, out var par)) return key;
            return lang == Lang.En && !string.IsNullOrEmpty(par.en) ? par.en : par.es;
        }

        /// <summary>Texto en el idioma guardado.</summary>
        public static string T(string key) => Get(key, Current);
    }
}
