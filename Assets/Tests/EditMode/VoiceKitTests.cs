using NUnit.Framework;
using HablaCamaron.Core;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La voz procedural de Don Pancho: balbuceo proporcional al texto, con
    /// tope, determinista (misma frase = misma melodía) y con samples sanos.
    /// </summary>
    public class VoiceKitTests
    {
        private const int Rate = 8000; // tasa baja: tests al vuelo

        [Test]
        public void LasSilabas_SonProporcionalesAlTexto_ConMinimoYTope()
        {
            Assert.AreEqual(0, VoiceKit.SyllableCount(null));
            Assert.AreEqual(0, VoiceKit.SyllableCount(""));
            Assert.AreEqual(2, VoiceKit.SyllableCount("¡Ojo!"), "corto: al menos dos golpes");
            Assert.AreEqual(VoiceKit.MaxSyllables,
                VoiceKit.SyllableCount(new string('a', 500)), "largo: se acota");
            Assert.LessOrEqual(VoiceKit.SyllableCount("Tranquilo, vuelva a encender."),
                VoiceKit.MaxSyllables);
        }

        [Test]
        public void LaMismaFrase_SuenaSiempreIgual()
        {
            const string frase = "¡Habla, camarón! Así se maneja en Quito.";
            int seed = VoiceKit.SeedFor(frase);
            Assert.AreEqual(seed, VoiceKit.SeedFor(frase), "semilla estable");

            var a = VoiceKit.RenderPhrase(seed, 5, Rate);
            var b = VoiceKit.RenderPhrase(seed, 5, Rate);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void FrasesDistintas_SuenanDistinto()
        {
            Assert.AreNotEqual(VoiceKit.SeedFor("Suave con el embrague."),
                               VoiceKit.SeedFor("¡Esa luz estaba en rojo!"));
        }

        [Test]
        public void ElBalbuceo_TieneDuracionYRangoSanos()
        {
            var data = VoiceKit.RenderPhrase(1234, 4, Rate);
            Assert.AreEqual(UnityEngine.Mathf.RoundToInt(VoiceKit.PhraseSeconds(4) * Rate),
                data.Length);
            foreach (float s in data)
                Assert.IsTrue(s >= -1f && s <= 1f, "sample fuera de rango");
        }

        [Test]
        public void SinSilabas_SilencioSinExplotar()
        {
            var data = VoiceKit.RenderPhrase(99, 0, Rate);
            foreach (float s in data) Assert.AreEqual(0f, s);
        }
    }
}
