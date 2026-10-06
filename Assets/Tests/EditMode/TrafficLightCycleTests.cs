using NUnit.Framework;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El ciclo semafórico es determinista (estado = función del tiempo).
    /// La propiedad sagrada: los dos grupos JAMÁS están en verde a la vez
    /// (eso sería un choque garantizado entre NPCs en la Fase 2).
    /// Duraciones de prueba: verde 8s, amarillo 2s, colchón rojo 1s.
    /// </summary>
    public class TrafficLightCycleTests
    {
        private const float G = 8f, Y = 2f, R = 1f; // media vuelta = 11s, ciclo = 22s

        [Test]
        public void AlInicio_Grupo0Verde_Grupo1Rojo()
        {
            Assert.AreEqual(LightState.Green, TrafficLightCycle.GetState(0, 0f, G, Y, R));
            Assert.AreEqual(LightState.Red, TrafficLightCycle.GetState(1, 0f, G, Y, R));
        }

        [Test]
        public void TrasElVerde_VieneElAmarillo_YLuegoElRojo()
        {
            Assert.AreEqual(LightState.Yellow, TrafficLightCycle.GetState(0, G + 0.1f, G, Y, R));
            Assert.AreEqual(LightState.Red, TrafficLightCycle.GetState(0, G + Y + 0.1f, G, Y, R));
        }

        [Test]
        public void EnLaSegundaMitad_ElGrupo1TieneSuVerde()
        {
            float half = G + Y + R;
            Assert.AreEqual(LightState.Green, TrafficLightCycle.GetState(1, half + 0.1f, G, Y, R));
            Assert.AreEqual(LightState.Red, TrafficLightCycle.GetState(0, half + 0.1f, G, Y, R));
        }

        [Test]
        public void ElCicloSeRepite_DespuesDeUnaVueltaCompleta()
        {
            float cycle = (G + Y + R) * 2f;
            Assert.AreEqual(LightState.Green, TrafficLightCycle.GetState(0, cycle + 0.1f, G, Y, R));
            Assert.AreEqual(LightState.Yellow, TrafficLightCycle.GetState(0, cycle + G + 0.1f, G, Y, R));
        }

        [Test]
        public void PropiedadSagrada_NuncaLosDosGruposEnVerdeALaVez()
        {
            // Muestrear dos ciclos completos cada décima de segundo.
            for (float t = 0f; t < (G + Y + R) * 4f; t += 0.1f)
            {
                bool verde0 = TrafficLightCycle.GetState(0, t, G, Y, R) == LightState.Green;
                bool verde1 = TrafficLightCycle.GetState(1, t, G, Y, R) == LightState.Green;
                Assert.IsFalse(verde0 && verde1, $"¡Ambos en verde en t={t:0.0}s!");
            }
        }

        [Test]
        public void ColchonEnRojo_HayUnInstanteDondeAmbosEstanEnRojo()
        {
            // Justo después del amarillo del grupo 0, ambos deben estar en rojo
            // (el colchón que evita que se crucen los últimos con los primeros).
            float t = G + Y + R * 0.5f;
            Assert.AreEqual(LightState.Red, TrafficLightCycle.GetState(0, t, G, Y, R));
            Assert.AreEqual(LightState.Red, TrafficLightCycle.GetState(1, t, G, Y, R));
        }
    }
}
