using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Core;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El mixer de la Fase 4: los sliders de Opciones tienen que oírse.
    /// Se respaldan/restauran las claves opt_* reales (mismo patrón que
    /// GameDataTests con el progreso del jugador).
    /// </summary>
    public class GameAudioSettingsTests
    {
        private static readonly string[] Keys =
        {
            GameAudioSettings.KEY_MUSIC, GameAudioSettings.KEY_SFX,
            GameAudioSettings.KEY_VOICE, GameAudioSettings.KEY_PEDALS,
        };

        private float[] _saved;
        private bool[] _had;

        [SetUp]
        public void BackupPrefs()
        {
            _saved = new float[Keys.Length];
            _had = new bool[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
            {
                _had[i] = PlayerPrefs.HasKey(Keys[i]);
                _saved[i] = PlayerPrefs.GetFloat(Keys[i], 0f);
            }
        }

        [TearDown]
        public void RestorePrefs()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                if (_had[i]) PlayerPrefs.SetFloat(Keys[i], _saved[i]);
                else PlayerPrefs.DeleteKey(Keys[i]);
            }
            PlayerPrefs.Save();
            GameAudioSettings.Refresh(); // que el mixer no se quede con basura de test
        }

        [Test]
        public void Refresh_LeeLosSlidersDeOpciones()
        {
            PlayerPrefs.SetFloat(GameAudioSettings.KEY_MUSIC, 0.42f);
            PlayerPrefs.SetFloat(GameAudioSettings.KEY_SFX, 0.13f);
            GameAudioSettings.Refresh();
            Assert.AreEqual(0.42f, GameAudioSettings.MusicVolume, 0.001f);
            Assert.AreEqual(0.13f, GameAudioSettings.SfxVolume, 0.001f);
        }

        [Test]
        public void ValoresFueraDeRango_SeAcotanA01()
        {
            PlayerPrefs.SetFloat(GameAudioSettings.KEY_MUSIC, 1.8f);
            PlayerPrefs.SetFloat(GameAudioSettings.KEY_SFX, -0.4f);
            GameAudioSettings.Refresh();
            Assert.AreEqual(1f, GameAudioSettings.MusicVolume);
            Assert.AreEqual(0f, GameAudioSettings.SfxVolume);
        }

        [Test]
        public void SensibilidadPorDefecto_NoCambiaLosPedalesDeLaFase0()
        {
            // 0.5 (el valor por defecto del slider) = multiplicador 1.0 EXACTO.
            Assert.AreEqual(1.0f, GameAudioSettings.PedalMultiplier(0.5f), 0.0001f);
        }

        [Test]
        public void SensibilidadExtrema_TieneLimitesRazonables()
        {
            Assert.AreEqual(0.6f, GameAudioSettings.PedalMultiplier(0f), 0.0001f);
            Assert.AreEqual(1.6f, GameAudioSettings.PedalMultiplier(1f), 0.0001f);
            // Valores basura se acotan, no explotan.
            Assert.AreEqual(0.6f, GameAudioSettings.PedalMultiplier(-3f), 0.0001f);
            Assert.AreEqual(1.6f, GameAudioSettings.PedalMultiplier(9f), 0.0001f);
        }

        [Test]
        public void LaSensibilidad_CreceConElSlider()
        {
            Assert.Less(GameAudioSettings.PedalMultiplier(0.2f),
                        GameAudioSettings.PedalMultiplier(0.8f));
        }

        [Test]
        public void LaMordazaDelVeredicto_SilenciaLosEfectos_YAlQuitarlaVuelven()
        {
            // El motor rugiendo sobre la pantalla de evaluación era el bug:
            // con la mordaza puesta, el volumen efectivo de efectos es CERO
            // (multiplica las muestras: silencio garantizado, no "pausa").
            PlayerPrefs.SetFloat(GameAudioSettings.KEY_SFX, 0.8f);
            GameAudioSettings.Refresh();
            try
            {
                GameAudioSettings.GameplayMuted = true;
                Assert.AreEqual(0f, GameAudioSettings.EffectiveSfxVolume);

                GameAudioSettings.GameplayMuted = false;
                Assert.AreEqual(0.8f, GameAudioSettings.EffectiveSfxVolume, 0.001f);
            }
            finally
            {
                GameAudioSettings.GameplayMuted = false; // no ensuciar otros tests
            }
        }
    }
}
