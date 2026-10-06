using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La bocina de los NPCs (pitan al que los bloquea, no lo embisten):
    /// el clip procedural debe ser sano y siempre el mismo.
    /// </summary>
    public class NpcHornTests
    {
        private const int Rate = 8000; // tasa baja: tests al vuelo

        [Test]
        public void ElPitazo_TieneLaDuracionYElRangoCorrectos()
        {
            var data = NpcHorn.Render(Rate);
            Assert.AreEqual(Mathf.RoundToInt(NpcHorn.Seconds * Rate), data.Length);
            foreach (float s in data)
                Assert.IsTrue(s >= -1f && s <= 1f, "sample fuera de rango");
        }

        [Test]
        public void ElPitazo_AbreYCierraEnSilencio_SinClics()
        {
            var data = NpcHorn.Render(Rate);
            Assert.AreEqual(0f, data[0], 0.01f, "arranca en silencio");
            Assert.AreEqual(0f, data[data.Length - 1], 0.01f, "cierra en silencio");
        }

        [Test]
        public void ElPitazo_EsDeterminista_ParaCompartirloEntreTodos()
        {
            CollectionAssert.AreEqual(NpcHorn.Render(Rate), NpcHorn.Render(Rate));
        }
    }
}
