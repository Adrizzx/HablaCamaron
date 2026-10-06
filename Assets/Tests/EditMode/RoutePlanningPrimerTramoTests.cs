using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Al replanificar, el primer waypoint de la ruta nueva no debe obligar
    /// a un giro en U: si queda atrás del rumbo actual, se salta (el NPC
    /// sigue por su calle y da la vuelta a la manzana en el próximo cruce).
    /// </summary>
    public class RoutePlanningPrimerTramoTests
    {
        // RoadNode no tiene constructor público (int, Vector3): se arma por
        // inicializador de objeto, como en RoadGraphData.AddNode.
        private static RoadNode Nodo(int id, Vector3 p) => new RoadNode { Id = id, Position = p };

        [Test]
        public void SeSaltaElWaypointQueQuedaAtras()
        {
            // Mirando al norte con el primer nodo al SUR: seguirlo sería el
            // giro en U que reportó el playtest.
            var path = new List<RoadNode>
            {
                Nodo(0, new Vector3(0f, 0f, -20f)), // atrás
                Nodo(1, new Vector3(0f, 0f, 40f)),  // adelante
            };
            int i = RoutePlanning.FirstUsefulWaypoint(path, Vector3.zero, Vector3.forward);
            Assert.AreEqual(1, i);
        }

        [Test]
        public void UnaCurvaNormalNoSeSalta()
        {
            var path = new List<RoadNode> { Nodo(0, new Vector3(25f, 0f, 25f)) }; // 45°
            Assert.AreEqual(0, RoutePlanning.FirstUsefulWaypoint(path, Vector3.zero, Vector3.forward));
        }

        [Test]
        public void SiTodoQuedaAtrasDevuelveElLargoDeLaRuta()
        {
            // -15 m: atrás Y dentro de MaxSkipMeters (25 m), así que se salta;
            // al no haber otro waypoint, el resultado es path.Count. (Ajustado
            // de -30 a -15: con el tope de distancia de la ronda 1 de revisión,
            // -30 quedaría FUERA del radio y ya no probaría "todo se salta".)
            var path = new List<RoadNode> { Nodo(0, new Vector3(0f, 0f, -15f)) };
            Assert.AreEqual(path.Count,
                RoutePlanning.FirstUsefulWaypoint(path, Vector3.zero, Vector3.forward));
        }

        [Test]
        public void SinRumboNoSeSaltaNada()
        {
            var path = new List<RoadNode> { Nodo(0, new Vector3(0f, 0f, -30f)) };
            Assert.AreEqual(0, RoutePlanning.FirstUsefulWaypoint(path, Vector3.zero, Vector3.zero));
        }

        [Test]
        public void WaypointAtrasYLejosNoSeSalta()
        {
            // A 60 m detrás: saltarlo sería cortar camino en línea recta
            // ignorando la curvatura real de la calle. Se respeta aunque
            // quede atrás del rumbo.
            var path = new List<RoadNode> { Nodo(0, new Vector3(0f, 0f, -60f)) };
            Assert.AreEqual(0, RoutePlanning.FirstUsefulWaypoint(path, Vector3.zero, Vector3.forward));
        }

        [Test]
        public void WaypointAtrasYCercaSeSaltaYDevuelveElSiguiente()
        {
            var path = new List<RoadNode>
            {
                Nodo(0, new Vector3(0f, 0f, -10f)), // atrás y cerca: se salta
                Nodo(1, new Vector3(0f, 0f, 40f)),  // adelante
            };
            Assert.AreEqual(1, RoutePlanning.FirstUsefulWaypoint(path, Vector3.zero, Vector3.forward));
        }
    }
}
