using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Missions;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La reprobación DURA del playtest: salirse de la vía o volcar el auto
    /// termina la misión al instante (a repetir el nivel). El juez es puro:
    /// acumula tiempo sostenido y perdona los parpadeos.
    /// </summary>
    public class DrivingFailJudgeTests
    {
        [Test]
        public void FueraDeVia_UnParpadeo_NoReprueba_YSeRearmaAlVolver()
        {
            var j = new DrivingFailJudge();
            Assert.AreEqual(FailReason.None, j.Tick(distToRoad: 15f, upDot: 1f, dt: 1.0f));
            // Volvió a la vía: el contador se reinicia.
            Assert.AreEqual(FailReason.None, j.Tick(3f, 1f, 0.25f));
            Assert.AreEqual(FailReason.None, j.Tick(15f, 1f, 1.9f));
        }

        [Test]
        public void FueraDeVia_Sostenido_Reprueba()
        {
            // Primero pisa la calle (si no, el guard de "nunca tocó la vía"
            // lo exime) y recién ahí se va, sostenido más que OffRoadSeconds
            // — que ahora es a propósito más largo que
            // RoadDiscipline.OffRoadReturnAt: es el ÚLTIMO RECURSO si el
            // sistema suave no logró devolver al jugador a la vía.
            var j = new DrivingFailJudge();
            Assert.AreEqual(FailReason.None, j.Tick(distToRoad: 0f, upDot: 1f, dt: 0.1f));
            Assert.AreEqual(FailReason.None,
                j.Tick(15f, 1f, DrivingFailJudge.OffRoadSeconds - 0.1f));
            Assert.AreEqual(FailReason.OffRoad, j.Tick(15f, 1f, 0.2f));
        }

        [Test]
        public void SpawnLejosDeLaVia_NoReprueba_HastaQuePiseCalleUnaVez()
        {
            // El garaje/spawn de una misión puede caer a más de OffRoadMeters
            // del grafo (mismo caso que ya resuelve RoadDiscipline con su
            // propio `_wasOnRoad`): mientras el auto NUNCA haya tocado la
            // calle, no se cuenta "fuera de vía" — si no, el tutorial del
            // embrague (misión 0) podía reprobarse solo por tardar en sacar
            // el auto del garaje, calando el motor, que es justo lo que ese
            // nivel enseña a hacer.
            var j = new DrivingFailJudge();
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(FailReason.None, j.Tick(distToRoad: 15f, upDot: 1f, dt: 1f));
        }

        [Test]
        public void Volcado_Sostenido_Reprueba()
        {
            var j = new DrivingFailJudge();
            Assert.AreEqual(FailReason.None, j.Tick(0f, 0.1f, 1.0f));
            Assert.AreEqual(FailReason.Rollover, j.Tick(0f, 0.1f, 0.6f));
        }

        [Test]
        public void UnBrincoDeFisica_BocaAbajoUnInstante_NoReprueba()
        {
            var j = new DrivingFailJudge();
            Assert.AreEqual(FailReason.None, j.Tick(0f, 0.1f, 1.0f));
            Assert.AreEqual(FailReason.None, j.Tick(0f, 1f, 0.25f)); // se enderezó
            Assert.AreEqual(FailReason.None, j.Tick(0f, 0.1f, 1.0f));
        }

        [Test]
        public void ElVuelcoManda_SobreLaSalidaDeVia()
        {
            // Volcado Y fuera de vía a la vez: el motivo es el vuelco.
            var j = new DrivingFailJudge();
            j.Tick(15f, 0.1f, 1.4f);
            Assert.AreEqual(FailReason.Rollover, j.Tick(15f, 0.1f, 0.7f));
        }
    }

    /// <summary>La distancia horizontal del auto a la vía más cercana del grafo.</summary>
    public class RoadGraphEdgeDistanceTests
    {
        [Test]
        public void SobreLaArista_EsCero_YALosDiezMetros_EsDiez()
        {
            var g = new RoadGraphData();
            var a = g.AddNode(Vector3.zero);
            var b = g.AddNode(Vector3.forward * 20f);
            g.Connect(a.Id, b.Id);

            Assert.AreEqual(0f, g.DistanceToNearestEdge(Vector3.forward * 10f), 1e-4f);
            Assert.AreEqual(10f, g.DistanceToNearestEdge(
                new Vector3(10f, 0f, 10f)), 1e-4f);
        }

        [Test]
        public void LaAlturaNoCuenta_SoloElPlano()
        {
            // En la cuesta el auto está más alto que los nodos: no es "fuera de vía".
            var g = new RoadGraphData();
            var a = g.AddNode(Vector3.zero);
            var b = g.AddNode(Vector3.forward * 20f);
            g.Connect(a.Id, b.Id);

            Assert.AreEqual(0f, g.DistanceToNearestEdge(
                new Vector3(0f, 8f, 10f)), 1e-4f);
        }

        [Test]
        public void MasAllaDelExtremo_MideAlPunta_NoALaRectaInfinita()
        {
            var g = new RoadGraphData();
            var a = g.AddNode(Vector3.zero);
            var b = g.AddNode(Vector3.forward * 20f);
            g.Connect(a.Id, b.Id);

            Assert.AreEqual(10f, g.DistanceToNearestEdge(
                Vector3.forward * 30f), 1e-4f);
        }

        [Test]
        public void SinAristas_DevuelveInfinitoPractico()
        {
            var g = new RoadGraphData();
            g.AddNode(Vector3.zero);
            Assert.AreEqual(float.MaxValue, g.DistanceToNearestEdge(Vector3.zero));
        }
    }
}
