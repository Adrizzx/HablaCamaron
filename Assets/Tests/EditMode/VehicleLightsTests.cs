using NUnit.Framework;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La receta de los faros es lo que hace jugable la misión nocturna:
    /// cortas = poco alcance apuntando al piso; largas = lejos y planas.
    /// Si alguien las iguala "porque se veía oscuro", la misión pierde sentido.
    /// </summary>
    public class VehicleLightsTests
    {
        [Test]
        public void Apagadas_NoAlumbranNada()
        {
            var beam = VehicleLights.BeamFor(0);
            Assert.AreEqual(0f, beam.Range);
            Assert.AreEqual(0f, beam.Intensity);
        }

        [Test]
        public void Largas_AlcanzanMasLejosQueLasCortas()
        {
            Assert.Greater(VehicleLights.BeamFor(2).Range, VehicleLights.BeamFor(1).Range);
            Assert.Greater(VehicleLights.BeamFor(2).Intensity, VehicleLights.BeamFor(1).Intensity);
        }

        [Test]
        public void Cortas_ApuntanMasAlPisoQueLasLargas()
        {
            // Las cortas se inclinan hacia abajo (no deslumbran); las largas van planas.
            Assert.Greater(VehicleLights.BeamFor(1).PitchDown, VehicleLights.BeamFor(2).PitchDown);
        }

        [Test]
        public void EstadoDesconocido_SeTrataComoApagadas()
        {
            Assert.AreEqual(0f, VehicleLights.BeamFor(99).Range);
        }
    }
}
