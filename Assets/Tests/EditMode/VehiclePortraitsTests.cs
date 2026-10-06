using NUnit.Framework;
using HablaCamaron.UI;
using HablaCamaron.Vehicle;

public class VehiclePortraitsTests
{
    [Test]
    public void CadaVehiculoDelRosterTieneSuRutaDeFoto()
    {
        Assert.AreEqual("Portraits/TAXI_chevrolet_aveo",
            VehiclePortraits.ResourcePathFor(VehicleRoster.AveoId));
        Assert.AreEqual("Portraits/mazda_bt",
            VehiclePortraits.ResourcePathFor(VehicleRoster.Bt50Id));
    }

    [Test]
    public void UnVehiculoDesconocidoNoTieneRutaYNoRevienta()
    {
        Assert.IsNull(VehiclePortraits.ResourcePathFor(99));
        Assert.DoesNotThrow(() => VehiclePortraits.TryPhoto(99, out _));
    }

    [Test]
    public void LosDosVehiculosDelRosterCarganSuFotoDeVerdad()
    {
        // El .meta importado como Default hacía que Resources.Load<Sprite>
        // devolviera null y el garaje cayera siempre al glifo.
        Assert.IsTrue(VehiclePortraits.TryPhoto(VehicleRoster.AveoId, out var aveo));
        Assert.IsNotNull(aveo);
        Assert.IsTrue(VehiclePortraits.TryPhoto(VehicleRoster.Bt50Id, out var bt50));
        Assert.IsNotNull(bt50);
    }
}
