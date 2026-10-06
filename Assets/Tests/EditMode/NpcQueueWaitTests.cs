using NUnit.Framework;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La espera de semáforo se propaga hacia atrás en la cola: el 3º o 4º
    /// auto de una fila larga no ve el semáforo (Sense() solo mira 25 m /
    /// 3 nodos), pero si el de adelante ya espera y él va casi parado,
    /// también cuenta como esperando — no como atasco.
    /// </summary>
    public class NpcQueueWaitTests
    {
        [Test]
        public void VeElRojoYObedece_Espera()
        {
            Assert.IsTrue(NpcQueueWait.Espera(true, 0f, false));
        }

        [Test]
        public void DetrasDeUnoQueEsperaYCasiParado_Espera()
        {
            Assert.IsTrue(NpcQueueWait.Espera(false, 0.1f, true));
        }

        [Test]
        public void DetrasDeUnoQueEsperaPeroCirculandoRapido_NoEspera()
        {
            Assert.IsFalse(NpcQueueWait.Espera(false, 5f, true));
        }

        [Test]
        public void SinAutoAdelanteEsperando_NoEspera()
        {
            Assert.IsFalse(NpcQueueWait.Espera(false, 0f, false));
        }
    }
}
