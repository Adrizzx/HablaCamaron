using NUnit.Framework;
using HablaCamaron.AI;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// ¿El NPC debe QUEDARSE en el waypoint actual (semáforo) en vez de
    /// saltar al siguiente? Bug real cazado con SemaforosNpcDiagTests: sin
    /// esta espera, CUALQUIER perfil cruzaba en rojo al soltar el freno a
    /// metros de la línea — no era cosa del Taxista.
    /// </summary>
    public class NpcWaypointHoldTests
    {
        [Test]
        public void SinSemaforoEnElNodo_NuncaEspera()
        {
            Assert.IsFalse(NpcWaypointHold.EsperaSemaforo(-1, true, LightState.Red));
        }

        [Test]
        public void ConVerde_NoEspera()
        {
            Assert.IsFalse(NpcWaypointHold.EsperaSemaforo(0, true, LightState.Green));
        }

        [Test]
        public void ConRojoYObedece_Espera()
        {
            Assert.IsTrue(NpcWaypointHold.EsperaSemaforo(0, true, LightState.Red));
        }

        [Test]
        public void ConAmarilloYObedece_Espera()
        {
            Assert.IsTrue(NpcWaypointHold.EsperaSemaforo(0, true, LightState.Yellow));
        }

        [Test]
        public void ConRojoPeroNoObedece_NoEspera()
        {
            // El taxista que decidió lanzarse: sigue pudiendo hacerlo (diseño, no bug).
            Assert.IsFalse(NpcWaypointHold.EsperaSemaforo(0, false, LightState.Red));
        }
    }
}
