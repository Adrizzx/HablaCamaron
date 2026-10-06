using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Missions;
using HablaCamaron.UI;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La selección de META por misión (varias misiones comparten zona, así que
    /// la meta define a cada una): nodo más lejano, semáforo más cercano y el
    /// armado de la meta "volver al inicio".
    /// </summary>
    public class MissionGoalsTests
    {
        private RoadGraphData _g;

        [SetUp]
        public void CreateGraph() => _g = new RoadGraphData();

        [Test]
        public void Farthest_EligeElNodoMasLejanoAlPuntoDePartida()
        {
            _g.AddNode(Vector3.zero);
            _g.AddNode(Vector3.forward * 20f);
            var cima = _g.AddNode(Vector3.forward * 80f);

            Assert.AreEqual(cima.Id, MissionGoals.Farthest(_g, Vector3.zero).Id);
        }

        [Test]
        public void Farthest_GrafoVacioONulo_DevuelveNull()
        {
            Assert.IsNull(MissionGoals.Farthest(_g, Vector3.zero));
            Assert.IsNull(MissionGoals.Farthest(null, Vector3.zero));
        }

        [Test]
        public void NearestTrafficLight_EligeElSemaforoMasCercano_IgnorandoNodosComunes()
        {
            var comun = _g.AddNode(Vector3.forward * 2f); // más cerca, pero sin semáforo
            var lejos = _g.AddNode(Vector3.forward * 50f);
            var cerca = _g.AddNode(Vector3.forward * 12f);
            lejos.TrafficLightGroup = 0;
            cerca.TrafficLightGroup = 1;

            var goal = MissionGoals.NearestTrafficLight(_g, Vector3.zero);
            Assert.AreEqual(cerca.Id, goal.Id);
            Assert.AreNotEqual(comun.Id, goal.Id);
        }

        [Test]
        public void NearestTrafficLight_SinSemaforos_DevuelveNull()
        {
            _g.AddNode(Vector3.zero);
            Assert.IsNull(MissionGoals.NearestTrafficLight(_g, Vector3.zero));
        }

        [Test]
        public void AsideFromStart_EligeUnNodoEnLaBanda_NuncaElDelSpawn()
        {
            // Playtest: la meta de "volver" caía ENCIMA del punto de inicio.
            // Ahora se elige un nodo A UN LADO: dentro de la banda [min, max].
            var bajoElAuto = _g.AddNode(Vector3.zero);          // < min: prohibido
            var vecino = _g.AddNode(Vector3.forward * 25f);     // en banda: ¡este!
            _g.AddNode(Vector3.forward * 48f);                  // en banda, más lejos
            _g.AddNode(Vector3.forward * 90f);                  // > max

            var goal = MissionGoals.AsideFromStart(_g, Vector3.zero, 18f, 55f);
            Assert.AreEqual(vecino.Id, goal.Id, "el más cercano DENTRO de la banda");
            Assert.AreNotEqual(bajoElAuto.Id, goal.Id);
        }

        [Test]
        public void AsideFromStart_SinNodosEnLaBanda_TomaElMasCercanoPasadoElMinimo()
        {
            _g.AddNode(Vector3.zero);
            var lejano = _g.AddNode(Vector3.forward * 80f); // fuera de banda (max 55)
            _g.AddNode(Vector3.forward * 120f);

            var goal = MissionGoals.AsideFromStart(_g, Vector3.zero, 18f, 55f);
            Assert.AreEqual(lejano.Id, goal.Id, "mejor lejos que encima del spawn");
        }

        [Test]
        public void AsideFromStart_GrafoVacioOSoloElSpawn_DevuelveNull()
        {
            Assert.IsNull(MissionGoals.AsideFromStart(_g, Vector3.zero, 18f, 55f));
            Assert.IsNull(MissionGoals.AsideFromStart(null, Vector3.zero, 18f, 55f));
            _g.AddNode(Vector3.zero); // todo queda bajo el mínimo
            Assert.IsNull(MissionGoals.AsideFromStart(_g, Vector3.zero, 18f, 55f));
        }

        [Test]
        public void LaEtiquetaDeVueltas_CuentaDesdeUno_YNoSePasa()
        {
            // C2 pide dos vueltas: el HUD dice en cuál va.
            Assert.AreEqual("VUELTA 1/2", MissionGoals.LapLabel(0, 2));
            Assert.AreEqual("VUELTA 2/2", MissionGoals.LapLabel(1, 2));
            // Nunca "3/2" aunque el contador se pase por un frame.
            Assert.AreEqual("VUELTA 2/2", MissionGoals.LapLabel(2, 2));
        }

        [Test]
        public void MetaDeVolver_SeArmaSoloAlAlejarse_YQuedaArmada()
        {
            const float armDist = 60f;
            // Al inicio (distancia 0) NO debe armarse: la misión terminaría sola.
            Assert.IsFalse(MissionGoals.UpdateReturnArmed(false, 0f, armDist));
            Assert.IsFalse(MissionGoals.UpdateReturnArmed(false, 59f, armDist));
            // Al alejarse se arma...
            Assert.IsTrue(MissionGoals.UpdateReturnArmed(false, 61f, armDist));
            // ...y armada se queda aunque el jugador vuelva a acercarse.
            Assert.IsTrue(MissionGoals.UpdateReturnArmed(true, 5f, armDist));
        }
    }

    /// <summary>Las señales de velocidad como dato (las usa el puntaje).</summary>
    public class RoadSignTests
    {
        [Test]
        public void LimitKmh_DevuelveElLimiteDeCadaSenalDeVelocidad()
        {
            Assert.AreEqual(30, RoadSign.LimitKmh(SignType.SpeedLimit30));
            Assert.AreEqual(50, RoadSign.LimitKmh(SignType.SpeedLimit50));
            Assert.AreEqual(90, RoadSign.LimitKmh(SignType.SpeedLimit90));
        }

        [Test]
        public void LimitKmh_SenalesQueNoSonDeVelocidad_DevuelvenCero()
        {
            Assert.AreEqual(0, RoadSign.LimitKmh(SignType.None));
            Assert.AreEqual(0, RoadSign.LimitKmh(SignType.Stop));
            Assert.AreEqual(0, RoadSign.LimitKmh(SignType.Yield));
        }
    }

    /// <summary>El cooldown por evento de Don Pancho (que no sature al jugador).</summary>
    public class DonPanchoCooldownTests
    {
        [Test]
        public void UnEventoQueNuncaSono_EstaListo()
        {
            Assert.IsTrue(DonPanchoDialogue.CooldownReady(
                float.NegativeInfinity, 0f, DonPanchoDialogue.EventCooldown));
        }

        [Test]
        public void DentroDelCooldown_NoRepite_YDespuesSi()
        {
            const float cd = DonPanchoDialogue.EventCooldown;
            Assert.IsFalse(DonPanchoDialogue.CooldownReady(10f, 10f + cd * 0.5f, cd));
            Assert.IsTrue(DonPanchoDialogue.CooldownReady(10f, 10f + cd, cd));
        }
    }
}
