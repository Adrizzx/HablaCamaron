using System.Linq;
using NUnit.Framework;
using HablaCamaron.Missions;

public class MissionCatalogStrictTests
{
    [Test]
    public void MuerteSubitaDesdeElNivelTresInclusoExamenYBonus()
    {
        foreach (var m in MissionCatalog.All)
            Assert.AreEqual(m.Id >= 2, m.StrictRules,
                $"'{m.Title}' (id {m.Id}): StrictRules debe ser {(m.Id >= 2)}");
    }

    [Test]
    public void LosTutorialesSiguenSiendoPedagogicos()
    {
        Assert.IsFalse(MissionCatalog.All.First(m => m.Id == 0).StrictRules);
        Assert.IsFalse(MissionCatalog.All.First(m => m.Id == 1).StrictRules);
    }

    [Test]
    public void ElCorredorDeRutaEmpiezaEnElNivelCuatro()
    {
        // La dificultad sube por escalones: los tutoriales son libres, el
        // nivel 3 añade la cuesta y la muerte súbita, y del 4 en adelante
        // además hay que seguir el camino de la misión (playtest).
        foreach (var m in MissionCatalog.All)
            Assert.AreEqual(m.Id >= 3, m.RouteLocked,
                $"'{m.Title}' (id {m.Id}): RouteLocked debe ser {(m.Id >= 3)}");
    }

    [Test]
    public void ElCorredorNuncaVaSinMuerteSubita()
    {
        // Seguir la ruta es MÁS exigente que las reglas estrictas: no tiene
        // sentido una misión que reprueba por desviarse pero perdona un rojo.
        foreach (var m in MissionCatalog.All)
            if (m.RouteLocked)
                Assert.IsTrue(m.StrictRules,
                    $"'{m.Title}' pide seguir la ruta pero no aplica reglas estrictas");
    }
}
