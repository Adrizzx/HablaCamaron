using NUnit.Framework;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El sastre de las placas: el texto de una señal JAMÁS debe salirse del
    /// cartel. Dado cuánto mide el texto (por characterSize = 1) y el espacio
    /// útil que deja la forma, Fit devuelve el tamaño que cabe.
    /// </summary>
    public class SignTextFitTests
    {
        [Test]
        public void TextoCorto_ConservaElTamanoBase()
        {
            // Sobra espacio: no hay por qué encoger (ni agrandar).
            Assert.AreEqual(0.085f, SignTextFit.Fit(0.085f,
                widestLineUnits: 2f, totalHeightUnits: 1f,
                maxWidth: 10f, maxHeight: 10f), 1e-5f);
        }

        [Test]
        public void TextoAncho_SeReduceExactoAlAnchoUtil()
        {
            // 20 unidades de texto en 1 m útil → tamaño 0.05 exacto.
            Assert.AreEqual(0.05f, SignTextFit.Fit(0.1f, 20f, 1f, 1f, 10f), 1e-5f);
        }

        [Test]
        public void MandaElLimiteMasRestrictivo_AnchoOAlto()
        {
            // El alto (0.5/10 = 0.05) es más exigente que el ancho (1/10 = 0.1).
            Assert.AreEqual(0.05f, SignTextFit.Fit(0.1f, 10f, 10f, 1f, 0.5f), 1e-5f);
        }

        [Test]
        public void LasFormasRecortan_CirculoDaMenosAnchoQueRectangulo()
        {
            Assert.Less(SignTextFit.UsableWidth(SignShape.Circulo, 0.42f),
                        SignTextFit.UsableWidth(SignShape.Rectangulo, 0.42f));
        }

        [Test]
        public void ElTrianguloEsElMasTacano_ConElAlto()
        {
            // La punta hacia abajo del ceda deja poquísimo alto útil.
            foreach (SignShape otra in new[] { SignShape.Circulo, SignShape.Rectangulo,
                                               SignShape.Octagono, SignShape.Rombo })
                Assert.Less(SignTextFit.UsableHeight(SignShape.Triangulo, 0.42f),
                            SignTextFit.UsableHeight(otra, 0.42f) + 1e-6f,
                            $"el triángulo debería dar menos alto que {otra}");
        }

        [Test]
        public void NuncaDevuelveTamanoCeroONegativo()
        {
            Assert.Greater(SignTextFit.Fit(0.1f, 1000f, 1000f, 0.5f, 0.5f), 0f);
            Assert.Greater(SignTextFit.Fit(0.1f, 0f, 0f, 0.5f, 0.5f), 0f); // texto vacío
        }
    }
}
