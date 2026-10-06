using NUnit.Framework;
using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Los muros límite nacen de este rectángulo: si sale mal, el jugador
    /// se escapa del mundo jugable o queda encerrado en la vía.
    /// </summary>
    public class WorldLimitsMathTests
    {
        [Test]
        public void ElRectangulo_EncierraLosNodos_ConElMargen()
        {
            var pts = new[]
            {
                new Vector3(-10f, 0f, -50f),
                new Vector3(30f, 2f, 5f),
                new Vector3(0f, 0f, 80f),
            };
            Assert.IsTrue(WorldLimitsMath.Bounds(pts, 20f, out var min, out var max));
            Assert.AreEqual(-30f, min.x, 0.001f); // −10 − 20
            Assert.AreEqual(-70f, min.z, 0.001f); // −50 − 20
            Assert.AreEqual(50f, max.x, 0.001f);  // 30 + 20
            Assert.AreEqual(100f, max.z, 0.001f); // 80 + 20
        }

        [Test]
        public void UnSoloNodo_DaUnCuadradoDelMargen()
        {
            var pts = new[] { new Vector3(5f, 0f, 5f) };
            Assert.IsTrue(WorldLimitsMath.Bounds(pts, 10f, out var min, out var max));
            Assert.AreEqual(-5f, min.x, 0.001f);
            Assert.AreEqual(15f, max.x, 0.001f);
        }

        [Test]
        public void SinNodos_NoHayLimites_YAvisa()
        {
            Assert.IsFalse(WorldLimitsMath.Bounds(new Vector3[0], 10f, out _, out _));
            Assert.IsFalse(WorldLimitsMath.Bounds(null, 10f, out _, out _));
        }

        [Test]
        public void ElMargen_NoTocaLaAltura()
        {
            // Los muros se dimensionan en XZ; la Y de los nodos no cambia por el margen.
            var pts = new[] { new Vector3(0f, 3f, 0f), new Vector3(0f, 7f, 0f) };
            WorldLimitsMath.Bounds(pts, 15f, out var min, out var max);
            Assert.AreEqual(3f, min.y, 0.001f);
            Assert.AreEqual(7f, max.y, 0.001f);
        }
    }
}
