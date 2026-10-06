using NUnit.Framework;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El apoyo al piso de los NPCs: elegir la superficie PISABLE, nunca el
    /// techo de un arco o una valla (el bug de los autos "que se iban para
    /// arriba" al pasar bajo la meta del examen).
    /// </summary>
    public class NpcGroundMathTests
    {
        [Test]
        public void PrefiereLaViaSobreElRellenoDeAbajo()
        {
            // Vía a 2.0 y relleno de la meseta a 0.5: manda la vía.
            Assert.IsTrue(NpcGroundMath.TryPick(new[] { 0.5f, 2.0f }, 2.1f, out float y));
            Assert.AreEqual(2.0f, y, 1e-4f);
        }

        [Test]
        public void IgnoraElTechoDelArcoPorEncima()
        {
            // Bajo el arco de la meta: techo a 6 m y vía a 0. Jamás subirse al arco.
            Assert.IsTrue(NpcGroundMath.TryPick(new[] { 6.0f, 0.0f }, 0.05f, out float y));
            Assert.AreEqual(0.0f, y, 1e-4f);
        }

        [Test]
        public void AceptaElEscalonDeLaCuesta()
        {
            // La vía sube un poco frente al auto: eso sí es pisable.
            Assert.IsTrue(NpcGroundMath.TryPick(new[] { 0.8f }, 0.0f, out float y));
            Assert.AreEqual(0.8f, y, 1e-4f);
        }

        [Test]
        public void SinSuperficiePisable_NoMueveAlAuto()
        {
            // Solo hay techos inalcanzables: conservar la altura actual.
            Assert.IsFalse(NpcGroundMath.TryPick(new[] { 5.0f, 8.0f }, 0.0f, out _));
        }

        [Test]
        public void SinHits_NoMueveAlAuto()
        {
            Assert.IsFalse(NpcGroundMath.TryPick(new float[0], 1.0f, out _));
        }
    }
}
