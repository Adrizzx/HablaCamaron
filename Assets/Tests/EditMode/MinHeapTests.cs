using NUnit.Framework;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El heap binario que reemplazó la frontera lineal del A*. Fija que
    /// siempre sale el MENOR primero (sin él, la ruta óptima no está garantizada).
    /// </summary>
    public class MinHeapTests
    {
        [Test]
        public void SacaSiempreElDeMenorPrioridad()
        {
            var h = new MinHeap();
            h.Push(10, 5f);
            h.Push(20, 1f);
            h.Push(30, 3f);
            h.Push(40, 2f);

            Assert.AreEqual(20, h.Pop()); // pri 1
            Assert.AreEqual(40, h.Pop()); // pri 2
            Assert.AreEqual(30, h.Pop()); // pri 3
            Assert.AreEqual(10, h.Pop()); // pri 5
            Assert.AreEqual(0, h.Count);
        }

        [Test]
        public void MantieneElOrdenAunqueSeInserteEntremezclado()
        {
            var h = new MinHeap();
            float[] pris = { 9f, 4f, 7f, 1f, 8f, 2f, 6f, 3f, 5f };
            for (int i = 0; i < pris.Length; i++) h.Push(i, pris[i]);

            float last = float.MinValue;
            int popped = 0;
            while (h.Count > 0)
            {
                int id = h.Pop();
                Assert.GreaterOrEqual(pris[id], last, "el heap devolvió algo fuera de orden");
                last = pris[id];
                popped++;
            }
            Assert.AreEqual(pris.Length, popped);
        }

        [Test]
        public void PushDespuesDePop_SigueOrdenado()
        {
            var h = new MinHeap();
            h.Push(1, 3f);
            h.Push(2, 1f);
            Assert.AreEqual(2, h.Pop()); // pri 1
            h.Push(3, 0.5f);
            h.Push(4, 2f);
            Assert.AreEqual(3, h.Pop()); // pri 0.5
            Assert.AreEqual(4, h.Pop()); // pri 2 (menor que el 1, que es pri 3)
            Assert.AreEqual(1, h.Pop()); // pri 3
        }
    }
}
