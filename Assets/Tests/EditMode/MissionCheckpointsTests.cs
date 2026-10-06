using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Missions;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Checkpoints invisibles que arman la meta del nivel 3 (playtest: la meta
    /// se podía tocar desde el arranque). Cómo se siembran sobre la ruta A*,
    /// cómo avanzan (solo en orden) y cuándo queda armada la meta.
    /// </summary>
    public class MissionCheckpointsTests
    {
        // Ruta recta de `largo` metros, un punto cada 10 m.
        private static List<Vector3> Ruta(float largo)
        {
            var r = new List<Vector3>();
            for (float z = 0f; z <= largo; z += 10f) r.Add(new Vector3(0f, 0f, z));
            return r;
        }

        [Test]
        public void UnaRutaLargaSeSiembraCadaOchentaMetrosConTope()
        {
            var cps = MissionCheckpoints.PickPositions(Ruta(465f)); // la ruta de la ciudad
            Assert.GreaterOrEqual(cps.Count, MissionCheckpoints.MinCheckpoints);
            Assert.LessOrEqual(cps.Count, MissionCheckpoints.MaxCheckpoints);
        }

        [Test]
        public void UnaRutaCortaIgualTieneElMinimo()
        {
            var cps = MissionCheckpoints.PickPositions(Ruta(100f));
            Assert.AreEqual(MissionCheckpoints.MinCheckpoints, cps.Count);
        }

        [Test]
        public void LosCheckpointsVanEnOrdenYNoSeRepiten()
        {
            var cps = MissionCheckpoints.PickPositions(Ruta(465f));
            for (int i = 1; i < cps.Count; i++)
                Assert.Greater(cps[i].z, cps[i - 1].z, "cada checkpoint va más adelante");
            Assert.AreEqual(cps.Count, cps.Distinct().Count());
        }

        [Test]
        public void SoloAvanzaElCheckpointQueTocaEnOrden()
        {
            Assert.AreEqual(1, MissionCheckpoints.Advance(0, 0));  // el primero: cuenta
            Assert.AreEqual(1, MissionCheckpoints.Advance(1, 0));  // repetir el 0: no cuenta
            Assert.AreEqual(1, MissionCheckpoints.Advance(1, 3));  // saltarse al 3: no cuenta
            Assert.AreEqual(2, MissionCheckpoints.Advance(1, 1));  // el siguiente: cuenta
        }

        [Test]
        public void LaMetaSeArmaSoloConTodosLosCheckpoints()
        {
            Assert.IsFalse(MissionCheckpoints.GoalArmed(2, 4));
            Assert.IsTrue(MissionCheckpoints.GoalArmed(4, 4));
            Assert.IsTrue(MissionCheckpoints.GoalArmed(0, 0)); // misión sin checkpoints
        }
    }
}
