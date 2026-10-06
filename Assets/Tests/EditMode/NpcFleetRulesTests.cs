using NUnit.Framework;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La flota NPC por zona (playtest: los vehículos grandes se bugueaban en
    /// los redondeles): grandes solo donde no hay anillo que tomar.
    /// </summary>
    public class NpcFleetRulesTests
    {
        [Test]
        public void UnaBuseta_NoEntraAlRedondel_PeroSiALaSimon()
        {
            // La buseta chica de Toon City mide 7.6 m (medida por el inyector).
            Assert.IsFalse(NpcFleetRules.Fits(zoneHasRoundabout: true, lengthMeters: 7.6f));
            Assert.IsTrue(NpcFleetRules.Fits(zoneHasRoundabout: false, lengthMeters: 7.6f));
        }

        [Test]
        public void UnAutoToonComun_EntraATodasLasZonas()
        {
            // Los autos de Toon City son sobredimensionados (escala toon):
            // el común mide 5.6-6.0 m y DEBE circular en los redondeles.
            Assert.IsTrue(NpcFleetRules.Fits(true, 5.6f));
            Assert.IsTrue(NpcFleetRules.Fits(true, 6.0f));
            Assert.IsTrue(NpcFleetRules.Fits(false, 6.0f));
        }

        [Test]
        public void ElMapaDeZonas_SoloLaSimonRecibeLaFlotaCompleta()
        {
            Assert.IsTrue(NpcFleetRules.ZoneHasRoundabout("Assets/Scenes/N1_ZonaSur.unity"));
            Assert.IsTrue(NpcFleetRules.ZoneHasRoundabout("Assets/Scenes/N1_CorredorExamen.unity"));
            Assert.IsTrue(NpcFleetRules.ZoneHasRoundabout("Assets/Scenes/N1_CiudadToon.unity"),
                "la grilla apretada de la ciudad tampoco es para busetas de 12 m");
            Assert.IsFalse(NpcFleetRules.ZoneHasRoundabout("Assets/Scenes/N1_SimonBolivar.unity"));
        }
    }
}
