using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Core;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Pruebas de la lógica de progresión: desbloqueo con 70, mejor puntaje, límites.
    /// GameData usa PlayerPrefs, así que se respalda y restaura el progreso real
    /// del jugador para que correr los tests nunca borre una partida.
    /// </summary>
    public class GameDataTests
    {
        private int _savedUnlocked;
        private int[] _savedScores;
        private bool _hadUnlocked;

        [SetUp]
        public void BackupPlayerPrefs()
        {
            _hadUnlocked = PlayerPrefs.HasKey("hc_unlocked");
            _savedUnlocked = PlayerPrefs.GetInt("hc_unlocked", 0);
            _savedScores = new int[GameData.MaxMissions];
            for (int i = 0; i < GameData.MaxMissions; i++)
                _savedScores[i] = PlayerPrefs.GetInt("hc_score_" + i, 0);
        }

        [TearDown]
        public void RestorePlayerPrefs()
        {
            if (_hadUnlocked) PlayerPrefs.SetInt("hc_unlocked", _savedUnlocked);
            else PlayerPrefs.DeleteKey("hc_unlocked");
            for (int i = 0; i < GameData.MaxMissions; i++)
                PlayerPrefs.SetInt("hc_score_" + i, _savedScores[i]);
            PlayerPrefs.Save();
        }

        [Test]
        public void Aprobar_Con70_DesbloqueaLaSiguienteMision()
        {
            var data = new GameData();
            data.RecordMissionResult(0, 70);
            Assert.AreEqual(1, data.HighestUnlockedMission);
        }

        [Test]
        public void Reprobar_Con69_NoDesbloquea()
        {
            var data = new GameData();
            data.RecordMissionResult(0, 69);
            Assert.AreEqual(0, data.HighestUnlockedMission);
        }

        [Test]
        public void RepetirMision_ConservaElMejorPuntaje()
        {
            var data = new GameData();
            data.RecordMissionResult(0, 85);
            data.RecordMissionResult(0, 72);
            Assert.AreEqual(85, data.MissionScores[0]);
        }

        [Test]
        public void RepetirMisionAnterior_NoRetrocedeElDesbloqueo()
        {
            var data = new GameData();
            data.RecordMissionResult(0, 90);
            data.RecordMissionResult(1, 90);
            data.RecordMissionResult(0, 75); // repite la primera
            Assert.AreEqual(2, data.HighestUnlockedMission);
        }

        [Test]
        public void IdDeMisionInvalido_SeIgnoraSinExplotar()
        {
            var data = new GameData();
            Assert.DoesNotThrow(() => data.RecordMissionResult(-1, 100));
            Assert.DoesNotThrow(() => data.RecordMissionResult(99, 100));
            Assert.AreEqual(0, data.HighestUnlockedMission);
        }

        [Test]
        public void LaCampanaCompleta_SePuedeAprobarDePunta_APunta()
        {
            // Las 7 misiones del catálogo caben y encadenan el desbloqueo:
            // aprobar la última (el examen, id 6) debe registrarse sin perderse.
            var data = new GameData();
            for (int id = 0; id < 7; id++)
                data.RecordMissionResult(id, 80);

            Assert.AreEqual(7, data.HighestUnlockedMission);
            Assert.AreEqual(80, data.MissionScores[6], "el examen debe guardar su puntaje");
        }
    }
}
