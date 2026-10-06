using NUnit.Framework;
using HablaCamaron.Core;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Los textos por idioma: el toggle de Opciones guardaba `opt_lang` pero
    /// nadie lo leía (cambiar idioma no hacía NADA). Estas pruebas fijan que
    /// cada clave tenga sus dos versiones y que el fallback nunca deje una
    /// clave cruda en pantalla.
    /// </summary>
    public class UIStringsTests
    {
        [Test]
        public void CadaIdioma_DevuelveSuTexto()
        {
            Assert.AreEqual("Opciones", UIStrings.Get("opt.title", Lang.Es));
            Assert.AreEqual("Settings", UIStrings.Get("opt.title", Lang.En));
        }

        [Test]
        public void ElIngles_EsDistintoDelEspanol_EnLasClavesDelMenu()
        {
            // Si alguien agrega una clave sin traducir, el menú en inglés se
            // vería igual que en español: esto lo delata.
            foreach (var clave in new[] { "menu.new", "menu.options", "menu.quit",
                                          "opt.save", "opt.reset", "opt.lang" })
                Assert.AreNotEqual(UIStrings.Get(clave, Lang.Es),
                                   UIStrings.Get(clave, Lang.En),
                                   $"la clave '{clave}' no está traducida");
        }

        [Test]
        public void ClaveDesconocida_DevuelveLaClave_SinExplotar()
        {
            Assert.AreEqual("no.existe", UIStrings.Get("no.existe", Lang.Es));
            Assert.AreEqual("no.existe", UIStrings.Get("no.existe", Lang.En));
        }

        [Test]
        public void GuardarElIdioma_CambiaLoQueSeLee()
        {
            // Respaldo/restauración de PlayerPrefs (patrón de GameDataTests).
            int previo = UnityEngine.PlayerPrefs.GetInt(UIStrings.KEY_LANG, 0);
            try
            {
                UIStrings.Current = Lang.En;
                Assert.AreEqual(Lang.En, UIStrings.Current);
                Assert.AreEqual("Save changes", UIStrings.T("opt.save"));

                UIStrings.Current = Lang.Es;
                Assert.AreEqual("Guardar cambios", UIStrings.T("opt.save"));
            }
            finally
            {
                UnityEngine.PlayerPrefs.SetInt(UIStrings.KEY_LANG, previo);
                UnityEngine.PlayerPrefs.Save();
            }
        }
    }
}
