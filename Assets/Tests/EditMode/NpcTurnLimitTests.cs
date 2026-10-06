using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;

public class NpcTurnLimitTests
{
    [Test]
    public void ParadoCasiNoGira()
    {
        // El bug del playtest: el NPC giraba 180° sobre su eje en plena calle.
        Assert.AreEqual(NpcTurnLimit.ParadoDegPerSec, NpcTurnLimit.MaxYawRate(0f), 1e-3f);
        Assert.Less(NpcTurnLimit.MaxYawRate(0f), 30f);
    }

    [Test]
    public void RodandoGiraComoUnAuto()
    {
        Assert.AreEqual(NpcTurnLimit.RodandoDegPerSec,
            NpcTurnLimit.MaxYawRate(NpcTurnLimit.VelocidadPlena + 5f), 1e-3f);
    }

    [Test]
    public void UnGiroDeMediaVueltaTomaVariosFrames()
    {
        var actual = Quaternion.identity;
        var deseada = Quaternion.Euler(0f, 180f, 0f);
        var paso = NpcTurnLimit.Steer(actual, deseada, 6f, 0.02f);
        Assert.Less(Quaternion.Angle(actual, paso), 3f, "un frame no puede voltear el auto");
    }

    [Test]
    public void ConTiempoSuficienteLlegaAlRumboPedido()
    {
        var rot = Quaternion.identity;
        var deseada = Quaternion.Euler(0f, 90f, 0f);
        for (int i = 0; i < 200; i++) rot = NpcTurnLimit.Steer(rot, deseada, 8f, 0.02f);
        Assert.Less(Quaternion.Angle(rot, deseada), 0.5f);
    }
}
