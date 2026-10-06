using NUnit.Framework;
using HablaCamaron.UI;

namespace HablaCamaron.Tests.EditMode
{
    public class VehiclePreviewMathTests
    {
        [Test]
        public void Encuadre_RespetaLaAlturaDelModelo()
        {
            Assert.AreEqual(2.2f,
                VehiclePreviewMath.OrthographicSize(2f, 2f, 2f, 1.1f), 0.001f);
        }

        [Test]
        public void Encuadre_RespetaElAnchoEnUnaVistaEstrecha()
        {
            Assert.AreEqual(4.4f,
                VehiclePreviewMath.OrthographicSize(4f, 1f, 1f, 1.1f), 0.001f);
        }

        [Test]
        public void Encuadre_ProtegeAspectoYPaddingInvalidos()
        {
            Assert.GreaterOrEqual(VehiclePreviewMath.OrthographicSize(1f, 1f, 0f, 0f), 1f);
        }
    }
}
