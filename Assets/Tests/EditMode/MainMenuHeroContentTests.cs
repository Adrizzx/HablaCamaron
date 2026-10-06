using NUnit.Framework;
using HablaCamaron.UI;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests.EditMode
{
    public class MainMenuHeroContentTests
    {
        [Test]
        public void Portada_MuestraElAveoCuandoEsElVehiculoElegido()
        {
            Assert.AreEqual("Chevrolet Aveo 2008",
                MainMenuHeroContent.VehicleName(VehicleRoster.AveoId));
            StringAssert.Contains("carro escuela",
                MainMenuHeroContent.VehicleRole(VehicleRoster.AveoId));
        }

        [Test]
        public void Portada_MuestraLaMazdaCuandoEsElVehiculoElegido()
        {
            Assert.AreEqual("Mazda BT-50",
                MainMenuHeroContent.VehicleName(VehicleRoster.Bt50Id));
            StringAssert.Contains("camioneta avanzada",
                MainMenuHeroContent.VehicleRole(VehicleRoster.Bt50Id));
        }
    }
}
