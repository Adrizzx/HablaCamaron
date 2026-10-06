using NUnit.Framework;
using HablaCamaron.AI;

public class NpcUnstuckRoutineTests
{
    private static UnstuckMove Correr(NpcUnstuckRoutine r, float segundos)
    {
        var ultimo = UnstuckMove.Nada;
        for (float t = 0f; t < segundos; t += 0.1f) ultimo = r.Tick(true, 0.1f);
        return ultimo;
    }

    [Test]
    public void LosPrimerosSegundosNoHaceNada()
    {
        // Un semáforo o una cola legítima no son un atasco.
        Assert.AreEqual(UnstuckMove.Nada, Correr(new NpcUnstuckRoutine(), 2.5f));
    }

    [Test]
    public void AlosTresSegundosRetrocede()
    {
        Assert.AreEqual(UnstuckMove.Retroceso, Correr(new NpcUnstuckRoutine(), 3.6f));
    }

    [Test]
    public void DespuesEsquivaDentroDelCarril()
    {
        Assert.AreEqual(UnstuckMove.Esquive, Correr(new NpcUnstuckRoutine(), 5.2f));
    }

    [Test]
    public void AlosSeisSegundosReplanificaUnaSolaVez()
    {
        var r = new NpcUnstuckRoutine();
        Correr(r, 6.05f);
        int replanificaciones = 0;
        for (float t = 0f; t < 6f; t += 0.1f)
            if (r.Tick(true, 0.1f) == UnstuckMove.Replanificar) replanificaciones++;
        Assert.LessOrEqual(replanificaciones, 1, "replanificar en bucle es peor que el atasco");
    }

    [Test]
    public void ReciclarEsElUltimoRecursoALosTreintaSegundos()
    {
        // El dueño lo pidió explícito: nada de Destroy() temprano.
        var r = new NpcUnstuckRoutine();
        Assert.AreNotEqual(UnstuckMove.Reciclar, Correr(r, 20f));
        Assert.AreEqual(UnstuckMove.Reciclar, Correr(r, 11f));
    }

    [Test]
    public void DestrabarseReiniciaLaRutina()
    {
        var r = new NpcUnstuckRoutine();
        Correr(r, 5f);
        r.Tick(false, 0.1f); // se destrabó
        Assert.AreEqual(UnstuckMove.Nada, Correr(r, 2f));
    }

    [Test]
    public void UnAtascoLargoNoSuperaElTechoDeDerivaLateral()
    {
        // Hallazgo de revisión: sin techo, un atasco de "Hora pico" (12 NPC)
        // podía arrastrar al NPC 14-16 m fuera de su carril. 25 s de bloqueo
        // sostenido cubren varios ciclos de Esquive tras la replanificación.
        var r = new NpcUnstuckRoutine();
        Correr(r, 25f);
        Assert.LessOrEqual(r.DerivaAcumulada, NpcUnstuckRoutine.MaxDerivaMetros);
    }

    [Test]
    public void DestrabarseReiniciaLaDerivaAcumulada()
    {
        var r = new NpcUnstuckRoutine();
        Correr(r, 6f); // ya pasó por el primer bloque de Esquive
        Assert.Greater(r.DerivaAcumulada, 0f, "la deriva debía haber empezado a acumularse");
        r.Tick(false, 0.1f); // se destrabó
        Assert.AreEqual(0f, r.DerivaAcumulada);
    }
}
