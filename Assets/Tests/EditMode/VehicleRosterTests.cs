using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Core;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La flota de la Fase 4: el Aveo siempre disponible, la BT-50 con candado
    /// hasta aprobar "La Simón de noche", y la selección guardada que nunca
    /// puede apuntar a un vehículo bloqueado (anti-trampas).
    /// </summary>
    public class VehicleRosterTests
    {
        private bool _hadSelected;
        private int _savedSelected;

        [SetUp]
        public void BackupPrefs()
        {
            _hadSelected = PlayerPrefs.HasKey(VehicleRoster.KEY_SELECTED);
            _savedSelected = PlayerPrefs.GetInt(VehicleRoster.KEY_SELECTED, 0);
        }

        [TearDown]
        public void RestorePrefs()
        {
            if (_hadSelected) PlayerPrefs.SetInt(VehicleRoster.KEY_SELECTED, _savedSelected);
            else PlayerPrefs.DeleteKey(VehicleRoster.KEY_SELECTED);
            PlayerPrefs.Save();
        }

        private static GameData ConSimonAprobada(int score = 80)
        {
            var data = new GameData();
            data.MissionScores[VehicleRoster.Bt50UnlockMissionId] = score;
            return data;
        }

        [Test]
        public void ElAveo_SiempreEstaDisponible()
        {
            Assert.IsTrue(VehicleRoster.IsUnlocked(VehicleRoster.AveoId, new GameData()));
            Assert.IsTrue(VehicleRoster.IsUnlocked(VehicleRoster.AveoId, null), "hasta sin partida");
        }

        [Test]
        public void LaBt50_SeDesbloqueaAprobandoLaSimonDeNoche()
        {
            Assert.IsFalse(VehicleRoster.IsUnlocked(VehicleRoster.Bt50Id, new GameData()),
                "partida nueva: bloqueada");
            Assert.IsFalse(VehicleRoster.IsUnlocked(VehicleRoster.Bt50Id, ConSimonAprobada(69)),
                "69 no es aprobar");
            Assert.IsTrue(VehicleRoster.IsUnlocked(VehicleRoster.Bt50Id, ConSimonAprobada(70)),
                "70 aprueba justo");
        }

        [Test]
        public void SeleccionGuardada_DeUnVehiculoBloqueado_CaeAlAveo()
        {
            PlayerPrefs.SetInt(VehicleRoster.KEY_SELECTED, VehicleRoster.Bt50Id);
            Assert.AreEqual(VehicleRoster.AveoId, VehicleRoster.SelectedId(new GameData()),
                "sin desbloquear no hay BT-50 aunque el guardado lo diga");
            Assert.AreEqual(VehicleRoster.Bt50Id, VehicleRoster.SelectedId(ConSimonAprobada()),
                "desbloqueada sí se respeta");
        }

        [Test]
        public void SeleccionBasura_CaeAlAveo()
        {
            PlayerPrefs.SetInt(VehicleRoster.KEY_SELECTED, 99);
            Assert.AreEqual(VehicleRoster.AveoId, VehicleRoster.SelectedId(ConSimonAprobada()));
        }

        [Test]
        public void LaBt50_EsPesadaConMasTorque_YMenosPerdonadora()
        {
            var aveo = VehicleRoster.CreateSpec(VehicleRoster.AveoId);
            var bt50 = VehicleRoster.CreateSpec(VehicleRoster.Bt50Id);

            StringAssert.Contains("BT-50", bt50.DisplayName);
            Assert.Greater(bt50.Mass, aveo.Mass, "camioneta: más pesada");
            Assert.Greater(bt50.MaxTorque, aveo.MaxTorque, "pero con más fuerza");
            Assert.Less(bt50.MaxRpm, aveo.MaxRpm, "motor de trabajo, no revienta");
            Assert.Greater(bt50.StallRpm, aveo.StallRpm, "cala más fácil (exige embrague fino)");
            Assert.Greater(bt50.SpringForce, aveo.SpringForce, "suspensión para el peso");

            Object.DestroyImmediate(aveo);
            Object.DestroyImmediate(bt50);
        }
    }
}
