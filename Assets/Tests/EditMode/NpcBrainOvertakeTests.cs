using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Las reglas nuevas de la FSM (auditoría): ADELANTAMIENTO seguro de
    /// obstáculos estáticos y DESEMPATE anti-deadlock. Incluye regresión: sin
    /// los flags nuevos, el comportamiento de siempre no cambia.
    /// </summary>
    public class NpcBrainOvertakeTests
    {
        private DriverProfile _p;

        [SetUp]
        public void Setup() => _p = DriverProfile.Particular();

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(_p);

        private static NpcSensors Clear() =>
            new NpcSensors { AheadDistance = float.MaxValue };

        [Test]
        public void ObstaculoEstaticoConCarrilLibre_Adelanta()
        {
            var s = Clear();
            s.AheadDistance = _p.MinGap * 1.5f; // dentro del colchón
            s.AheadIsStatic = true;
            s.LeftLaneClear = true;

            var d = NpcBrain.Decide(s, _p, obeysThisLight: true);
            Assert.AreEqual(NpcState.Overtaking, d.State);
            Assert.Greater(d.TargetSpeed, 0f, "adelantar es en movimiento, no parado");
        }

        [Test]
        public void ObstaculoEstaticoSinCarrilLibre_NoAdelanta_Sigue()
        {
            var s = Clear();
            s.AheadDistance = _p.MinGap * 1.5f;
            s.AheadIsStatic = true;
            s.LeftLaneClear = false; // carril ocupado: no invade

            var d = NpcBrain.Decide(s, _p, obeysThisLight: true);
            Assert.AreNotEqual(NpcState.Overtaking, d.State);
            Assert.AreEqual(NpcState.Following, d.State);
        }

        [Test]
        public void DosParados_ElDePrioridad_Reanuda()
        {
            var s = Clear();
            s.AheadDistance = _p.MinGap * 1.2f;
            s.HasPriorityOverBlocker = true;

            var d = NpcBrain.Decide(s, _p, obeysThisLight: true);
            Assert.AreEqual(NpcState.Cruising, d.State, "el de prioridad deshace el atasco");
            Assert.Greater(d.TargetSpeed, 0f);
        }

        [Test]
        public void DosParados_ElSinPrioridad_SigueEsperando()
        {
            var s = Clear();
            s.AheadDistance = _p.MinGap * 1.2f;
            s.HasPriorityOverBlocker = false; // NO tengo prioridad

            var d = NpcBrain.Decide(s, _p, obeysThisLight: true);
            Assert.AreNotEqual(NpcState.Cruising, d.State);
            Assert.AreEqual(NpcState.Following, d.State);
        }

        [Test]
        public void SinFlagsNuevos_ComportamientoDeSiempre_NoCambia()
        {
            // Regresión: un vehículo adelante en el colchón, sin flags nuevos,
            // sigue dando Following (no Overtaking ni Cruising).
            var s = Clear();
            s.AheadDistance = _p.MinGap * 1.5f;

            var d = NpcBrain.Decide(s, _p, obeysThisLight: true);
            Assert.AreEqual(NpcState.Following, d.State);
        }

        [Test]
        public void ChoqueInminente_MandaSobreElAdelantamiento()
        {
            // La emergencia tiene prioridad: aunque haya carril libre, si algo
            // está PEGADO, frena — no intenta rodearlo.
            var s = Clear();
            s.AheadDistance = _p.MinGap * 0.4f; // pegadísimo
            s.AheadIsStatic = true;
            s.LeftLaneClear = true;

            var d = NpcBrain.Decide(s, _p, obeysThisLight: true);
            Assert.AreEqual(NpcState.Braking, d.State);
        }
    }
}
