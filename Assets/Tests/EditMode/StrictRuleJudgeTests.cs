using NUnit.Framework;
using HablaCamaron.Missions;

public class StrictRuleJudgeTests
{
    [Test]
    public void CruzarEnRojoRepruebaAlInstante()
    {
        var juez = new StrictRuleJudge();
        Assert.AreEqual(StrictFail.RedLight,
            juez.Evaluate(cruceEnRojo: true, LaneVerdict.OnRoad, 0.02f));
    }

    [Test]
    public void ContraviaBreveNoReprueba_ElCruceDiagonalEsLegitimo()
    {
        var juez = new StrictRuleJudge();
        for (float t = 0f; t < 1.9f; t += 0.1f)
            Assert.AreEqual(StrictFail.None,
                juez.Evaluate(false, LaneVerdict.WrongWay, 0.1f));
    }

    [Test]
    public void ContraviaSostenidaDosSegundosReprueba()
    {
        var juez = new StrictRuleJudge();
        StrictFail ultimo = StrictFail.None;
        for (float t = 0f; t < 2.2f; t += 0.1f)
            ultimo = juez.Evaluate(false, LaneVerdict.WrongWay, 0.1f);
        Assert.AreEqual(StrictFail.WrongWay, ultimo);
    }

    [Test]
    public void VolverAlCarrilReiniciaElContador()
    {
        var juez = new StrictRuleJudge();
        for (float t = 0f; t < 1.5f; t += 0.1f)
            juez.Evaluate(false, LaneVerdict.WrongWay, 0.1f);
        juez.Evaluate(false, LaneVerdict.OnRoad, 0.1f); // se enderezó
        for (float t = 0f; t < 1.5f; t += 0.1f)
            Assert.AreEqual(StrictFail.None,
                juez.Evaluate(false, LaneVerdict.WrongWay, 0.1f));
    }

    [Test]
    public void FueraDeLaViaNoEsAsuntoDeEsteJuez()
    {
        // DrivingFailJudge ya cubre salirse (>10 m / 2 s); aquí ni cuenta.
        var juez = new StrictRuleJudge();
        for (float t = 0f; t < 5f; t += 0.1f)
            Assert.AreEqual(StrictFail.None,
                juez.Evaluate(false, LaneVerdict.OffRoad, 0.1f));
    }
}
