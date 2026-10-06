using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Missions;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El juez de carril: contravía cuando se avanza CONTRA el sentido del
    /// carril más cercano, fuera de la vía cuando no hay calle cerca, y
    /// legalidad de los cruces perpendiculares (pedidos del playtest: "no debe
    /// dejar ir en el carril contrario" y "solo por las calles").
    /// </summary>
    public class LaneJudgeTests
    {
        /// <summary>Calle recta de dos carriles: el propio a +Z en x=0, el
        /// contrario a −Z en x=6.</summary>
        private static RoadGraphData Calle()
        {
            var g = new RoadGraphData();
            var a0 = g.AddNode(new Vector3(0, 0, 0));
            var a1 = g.AddNode(new Vector3(0, 0, 60));
            var b0 = g.AddNode(new Vector3(6, 0, 60));
            var b1 = g.AddNode(new Vector3(6, 0, 0));
            g.Connect(a0.Id, a1.Id);
            g.Connect(b0.Id, b1.Id);
            return g;
        }

        [Test]
        public void PorSuCarrilConElSentido_EsLegal()
        {
            Assert.AreEqual(LaneVerdict.OnRoad,
                LaneJudge.Judge(Calle(), new Vector3(0, 0, 30), Vector3.forward));
        }

        [Test]
        public void PorElCarrilContrario_EsContravia()
        {
            // Sobre el carril de los que vienen (x=6), avanzando hacia +Z.
            Assert.AreEqual(LaneVerdict.WrongWay,
                LaneJudge.Judge(Calle(), new Vector3(6, 0, 30), Vector3.forward));
        }

        [Test]
        public void ContraviaEnSuPropioCarril_TambienEsContravia()
        {
            // Devolverse marcha adelante por el carril propio (hacia −Z en x=0).
            Assert.AreEqual(LaneVerdict.WrongWay,
                LaneJudge.Judge(Calle(), new Vector3(0, 0, 30), Vector3.back));
        }

        [Test]
        public void LejosDeTodaCalle_EsFueraDeVia()
        {
            Assert.AreEqual(LaneVerdict.OffRoad,
                LaneJudge.Judge(Calle(), new Vector3(40, 0, 30), Vector3.forward));
        }

        [Test]
        public void CruzarPerpendicular_EsLegal()
        {
            // Atravesando la calle de lado (un giro, una entrada): no se castiga.
            Assert.AreEqual(LaneVerdict.OnRoad,
                LaneJudge.Judge(Calle(), new Vector3(0, 0, 30), Vector3.right));
        }

        [Test]
        public void LaAlturaNoInfluye()
        {
            Assert.AreEqual(LaneVerdict.OnRoad,
                LaneJudge.Judge(Calle(), new Vector3(0, 1.2f, 30), Vector3.forward));
        }

        [Test]
        public void SinGrafoONulo_NoJuzga()
        {
            Assert.AreEqual(LaneVerdict.OnRoad,
                LaneJudge.Judge(null, Vector3.zero, Vector3.forward));
            Assert.AreEqual(LaneVerdict.OnRoad,
                LaneJudge.Judge(new RoadGraphData(), Vector3.zero, Vector3.forward));
        }

        [Test]
        public void SinRumbo_NoJuzga()
        {
            Assert.AreEqual(LaneVerdict.OnRoad,
                LaneJudge.Judge(Calle(), new Vector3(6, 0, 30), Vector3.zero));
        }

        [Test]
        public void ElPuntoDeRetornoEsElCarrilMasCercano()
        {
            // Perdido a la derecha de la calle: vuelve al carril de x=6 (el
            // más cercano) mirando hacia −Z (el sentido de ese carril).
            Assert.IsTrue(LaneJudge.NearestLanePoint(Calle(), new Vector3(30, 0, 20),
                out var point, out var dir));
            Assert.AreEqual(6f, point.x, 1e-3f);
            Assert.AreEqual(20f, point.z, 1e-3f);
            Assert.Less(dir.z, -0.9f);
        }

        // ------ La fórmula del GDD con las infracciones nuevas ------

        [Test]
        public void LaContraviaDescuentaSenales()
        {
            var limpio = MissionScoring.Compute(new MissionStats
            { ReachedGoal = true, TimeUsed = 60, TimeLimit = 120 }, 0);
            var conUna = MissionScoring.Compute(new MissionStats
            { ReachedGoal = true, TimeUsed = 60, TimeLimit = 120, WrongWays = 1 }, 0);

            // La contravía cuesta lo mismo que un rojo.
            Assert.AreEqual(limpio.scoreSignals - 5, conUna.scoreSignals);
            Assert.AreEqual(limpio.totalScore - 5, conUna.totalScore);
        }

        [Test]
        public void SalirseDeLaViaDescuentaDefensiva()
        {
            var limpio = MissionScoring.Compute(new MissionStats
            { ReachedGoal = true, TimeUsed = 60, TimeLimit = 120 }, 0);
            var conUna = MissionScoring.Compute(new MissionStats
            { ReachedGoal = true, TimeUsed = 60, TimeLimit = 120, OffRoads = 1 }, 0);

            // Salirse cuesta 2 y además tumba el bono de conducción limpia (6).
            const int costeSalirse = 2 + MissionScoring.DefensiveBonus;
            Assert.AreEqual(limpio.scoreDefensive - costeSalirse, conUna.scoreDefensive);
            Assert.AreEqual(limpio.totalScore - costeSalirse, conUna.totalScore);
        }
    }
}
