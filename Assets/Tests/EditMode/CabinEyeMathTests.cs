using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Vehicle;

public class CabinEyeMathTests
{
    // Bounds reales del Car_2D medidos por el builder: 5.6 × 2.7 × 3.9 m.
    private static Bounds Cabina() =>
        new Bounds(new Vector3(0f, 1.95f, 0f), new Vector3(2.7f, 3.9f, 5.6f));

    [Test]
    public void ElOjoQuedaMasBajoQueElFactorHistorico()
    {
        var ojo = CabinEyeMath.EyeLocalFor(Cabina());
        // El 0.80 del max.y era la altura vieja (pedido del playtest: bajarla).
        Assert.Less(ojo.y, Cabina().max.y * 0.80f);
        Assert.AreEqual(Cabina().max.y * CabinEyeMath.AlturaOjos, ojo.y, 1e-4f);
    }

    [Test]
    public void LateralYProfundidadConservanLaRecetaDelBuilder()
    {
        var b = Cabina();
        var ojo = CabinEyeMath.EyeLocalFor(b);
        Assert.AreEqual(-b.size.x * 0.18f, ojo.x, 1e-4f);            // asiento izquierdo
        Assert.AreEqual(b.center.z + b.size.z * 0.08f, ojo.z, 1e-4f); // tras el volante
    }
}
