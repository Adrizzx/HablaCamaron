using NUnit.Framework;
using UnityEngine;
using HablaCamaron.UI;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>La proyección mundo → minimapa: norte arriba, escala uniforme
    /// y todo punto (aunque el jugador se salga del grafo) dentro del marco.</summary>
    public class MiniMapMathTests
    {
        [Test]
        public void ElCentroDelMundo_CaeEnElCentroDelMapa()
        {
            var p = MiniMapMath.WorldToMap(new Vector3(50f, 3f, -20f),
                new Vector3(50f, 0f, -20f), 100f, 100f);
            Assert.AreEqual(Vector2.zero, p);
        }

        [Test]
        public void ElNorte_QuedaArriba_YLaEscalaEsProporcional()
        {
            Vector3 center = Vector3.zero;
            var norte = MiniMapMath.WorldToMap(new Vector3(0f, 0f, 50f), center, 100f, 100f);
            Assert.AreEqual(new Vector2(0f, 50f), norte, "z del mundo = y del mapa");
            var este = MiniMapMath.WorldToMap(new Vector3(25f, 0f, 0f), center, 100f, 100f);
            Assert.AreEqual(new Vector2(25f, 0f), este);
        }

        [Test]
        public void FueraDelGrafo_SeAcotaAlMarco_NoSeSaleDelPanel()
        {
            var p = MiniMapMath.WorldToMap(new Vector3(500f, 0f, -900f),
                Vector3.zero, 100f, 100f);
            Assert.AreEqual(new Vector2(100f, -100f), p);
        }

        [Test]
        public void MundoDegenerado_NoDividePorCero()
        {
            var p = MiniMapMath.WorldToMap(Vector3.one, Vector3.zero, 0f, 100f);
            Assert.AreEqual(Vector2.zero, p);
        }
    }

    /// <summary>El cambio secuencial del mando: R → N → 1..5 sin saltos.</summary>
    public class NextGearTests
    {
        [Test]
        public void SubeYBaja_DeUnaEnUna_PasandoPorNeutro()
        {
            Assert.AreEqual(1, VehicleController.NextGear(0, true, 5));
            Assert.AreEqual(2, VehicleController.NextGear(1, true, 5));
            Assert.AreEqual(0, VehicleController.NextGear(1, false, 5));
            Assert.AreEqual(-1, VehicleController.NextGear(0, false, 5), "bajar de N = reversa");
            Assert.AreEqual(0, VehicleController.NextGear(-1, true, 5), "subir de R = neutro");
        }

        [Test]
        public void NoSeSaleDeLaCaja()
        {
            Assert.AreEqual(5, VehicleController.NextGear(5, true, 5), "no hay 6ª");
            Assert.AreEqual(-1, VehicleController.NextGear(-1, false, 5), "no hay doble reversa");
        }

        [Test]
        public void LaR_EsToggle_EnReversaVuelveANeutro()
        {
            // El pedido del playtest: "un botón para dejar de ir en reversa".
            Assert.AreEqual(0, VehicleController.ReverseToggleTarget(-1), "R en reversa = neutro");
            Assert.AreEqual(-1, VehicleController.ReverseToggleTarget(0), "R en neutro = reversa");
            Assert.AreEqual(-1, VehicleController.ReverseToggleTarget(3), "R en marcha = reversa");
        }

        [Test]
        public void ElEmbrague_EstaListoDesdeElUmbral()
        {
            Assert.IsFalse(VehicleController.ClutchReady(0.69f), "pedal a medio camino: no");
            Assert.IsTrue(VehicleController.ClutchReady(VehicleController.ClutchShiftThreshold));
            Assert.IsTrue(VehicleController.ClutchReady(1f));
        }
    }
}
