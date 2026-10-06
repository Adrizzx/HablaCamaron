using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Core;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El compositor procedural de la Fase 4: dónde suena la música y que los
    /// samples generados sean sanos (rango, silencio en los bordes del loop,
    /// determinismo). Tasa baja (8 kHz) para que los tests corran al vuelo.
    /// </summary>
    public class MusicKitTests
    {
        private const int Rate = 8000;

        [Test]
        public void LosMenusLlevanMusica_YLasZonasDeManejoNo()
        {
            Assert.AreEqual(MusicMood.Warm, MusicKit.MoodForScene("MainMenu"));
            Assert.AreEqual(MusicMood.Warm, MusicKit.MoodForScene("CampaignMap"));
            Assert.AreEqual(MusicMood.Warm, MusicKit.MoodForScene("Garage"));
            Assert.AreEqual(MusicMood.Silent, MusicKit.MoodForScene("N1_ZonaSur"));
            Assert.AreEqual(MusicMood.Silent, MusicKit.MoodForScene("N0_TestDrive"));
            Assert.AreEqual(MusicMood.Silent, MusicKit.MoodForScene("N2_QuitoCiudad"));
            Assert.AreEqual(MusicMood.Silent, MusicKit.MoodForScene(null));
        }

        [Test]
        public void ElLoopDeMenus_TieneLaDuracionYElRangoCorrectos()
        {
            var data = MusicKit.RenderMenuLoop(Rate);
            Assert.AreEqual(Mathf.RoundToInt(MusicKit.LoopSeconds * Rate), data.Length);
            foreach (float s in data)
                Assert.IsTrue(s >= -1f && s <= 1f, "sample fuera de rango");
        }

        [Test]
        public void ElLoop_AbreYCierraEnSilencio_ParaNoHacerClicAlRepetir()
        {
            var data = MusicKit.RenderMenuLoop(Rate);
            Assert.AreEqual(0f, data[0], 0.001f, "debe arrancar en silencio");
            Assert.AreEqual(0f, data[data.Length - 1], 0.01f, "debe cerrar casi en silencio");
        }

        [Test]
        public void ElLoop_EsDeterminista_SiempreLaMismaPieza()
        {
            var a = MusicKit.RenderMenuLoop(Rate);
            var b = MusicKit.RenderMenuLoop(Rate);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void LosStings_SonDistintosSegunElVeredicto()
        {
            var pass = MusicKit.RenderSting(true, Rate);
            var fail = MusicKit.RenderSting(false, Rate);
            Assert.AreEqual(Mathf.RoundToInt(MusicKit.StingSeconds * Rate), pass.Length);
            CollectionAssert.AreNotEqual(pass, fail, "aprobar y reprobar deben sonar distinto");
        }
    }

    /// <summary>El ambiente por zona (Fase 4): qué carácter lleva cada escena.</summary>
    public class ZoneAmbienceTests
    {
        [Test]
        public void CadaZona_TieneSuCaracter()
        {
            Assert.AreEqual(AmbienceProfile.Barrio, ZoneAmbience.ProfileForScene("N1_ZonaSur"));
            Assert.AreEqual(AmbienceProfile.Barrio, ZoneAmbience.ProfileForScene("N0_TestDrive"));
            Assert.AreEqual(AmbienceProfile.Autopista, ZoneAmbience.ProfileForScene("N1_SimonBolivar"));
            Assert.AreEqual(AmbienceProfile.Ciudad, ZoneAmbience.ProfileForScene("N1_CorredorExamen"));
            Assert.AreEqual(AmbienceProfile.Ciudad, ZoneAmbience.ProfileForScene("N2_QuitoCiudad"));
        }

        [Test]
        public void LosMenus_NoLlevanAmbiente()
        {
            Assert.AreEqual(AmbienceProfile.None, ZoneAmbience.ProfileForScene("MainMenu"));
            Assert.AreEqual(AmbienceProfile.None, ZoneAmbience.ProfileForScene("CampaignMap"));
            Assert.AreEqual(AmbienceProfile.None, ZoneAmbience.ProfileForScene("Splash"));
        }
    }
}
