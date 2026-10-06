using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Sanidad de la ficha técnica por defecto: si alguien edita el asset del Aveo
    /// con valores rotos (marchas desordenadas, RPM imposibles), esto lo detecta.
    /// </summary>
    public class VehicleSpecTests
    {
        private VehicleSpec _spec;

        [SetUp]
        public void CreateSpec() => _spec = ScriptableObject.CreateInstance<VehicleSpec>();

        [TearDown]
        public void DestroySpec() => Object.DestroyImmediate(_spec);

        [Test]
        public void Rpm_OrdenCoherente_StallMenorQueIdleMenorQueMax()
        {
            Assert.Less(_spec.StallRpm, _spec.IdleRpm, "El motor no puede calarse en ralentí");
            Assert.Less(_spec.IdleRpm, _spec.MaxRpm);
        }

        [Test]
        public void Marchas_RelacionesDescendentes_De1raA5ta()
        {
            for (int i = 1; i < _spec.GearRatios.Length; i++)
                Assert.Less(_spec.GearRatios[i], _spec.GearRatios[i - 1],
                    $"La marcha {i + 1} debe ser más larga que la {i}");
        }

        [Test]
        public void CurvaDeTorque_SiempreEntre0y1()
        {
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                float v = _spec.TorqueCurve.Evaluate(t);
                Assert.That(v, Is.InRange(0f, 1f), $"Torque fuera de rango en RPM {t:0.00}");
            }
        }

        [Test]
        public void PuntoDeFriccion_DentroDelRecorridoDelPedal()
        {
            Assert.That(_spec.BitePoint, Is.InRange(0.2f, 0.8f));
        }

        [Test]
        public void EmbragueDeTeclado_SueltaMasLentoDeLoQuePisa()
        {
            // Es lo que hace aprendible el "soltar suave" con una tecla digital.
            Assert.Less(_spec.ClutchReleaseSpeed, _spec.ClutchPressSpeed);
        }
    }
}
