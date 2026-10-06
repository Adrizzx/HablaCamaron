using NUnit.Framework;
using HablaCamaron.World;

namespace HablaCamaron.Tests.EditMode
{
    /// <summary>
    /// El mapeo estado del ciclo → focos encendidos (semáforo construido por
    /// código con amarillo propio vs. poste viejo de solo verde/rojo).
    /// </summary>
    public class TrafficLampFacesTests
    {
        [Test]
        public void ConAmarillo_CadaEstadoPrendeUnSoloFoco()
        {
            TrafficLampFaces.For(LightState.Green, true, out bool g, out bool y, out bool r);
            Assert.IsTrue(g); Assert.IsFalse(y); Assert.IsFalse(r);

            TrafficLampFaces.For(LightState.Yellow, true, out g, out y, out r);
            Assert.IsFalse(g); Assert.IsTrue(y); Assert.IsFalse(r);

            TrafficLampFaces.For(LightState.Red, true, out g, out y, out r);
            Assert.IsFalse(g); Assert.IsFalse(y); Assert.IsTrue(r);
        }

        [Test]
        public void SinAmarillo_ElAmarilloSeMuestraConAmbosFocos()
        {
            // Postes Toon City (solo verde y rojo): el comportamiento clásico.
            TrafficLampFaces.For(LightState.Yellow, false, out bool g, out bool y, out bool r);
            Assert.IsTrue(g, "amarillo = ambos prendidos");
            Assert.IsTrue(r, "amarillo = ambos prendidos");
            Assert.IsFalse(y, "no hay foco amarillo que prender");
        }

        [Test]
        public void NingunEstado_DejaElSemaforoApagadoDelTodo()
        {
            foreach (var estado in new[] { LightState.Green, LightState.Yellow, LightState.Red })
                foreach (var conAmarillo in new[] { true, false })
                {
                    TrafficLampFaces.For(estado, conAmarillo, out bool g, out bool y, out bool r);
                    Assert.IsTrue(g || y || r,
                        $"semáforo apagado en {estado} (amarillo: {conAmarillo})");
                }
        }
    }
}
