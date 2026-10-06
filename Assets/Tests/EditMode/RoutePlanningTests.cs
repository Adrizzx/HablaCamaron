using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El nodo donde un NPC arranca su ruta: adelante y en su carril, nunca
    /// el más cercano a secas (el bug de los buses que daban media vuelta en
    /// el redondel al replanificar).
    /// </summary>
    public class RoutePlanningTests
    {
        /// <summary>Dos carriles rectos paralelos: el propio va a +Z (x=0) y
        /// el contrario a −Z (x=6). Devuelve el grafo.</summary>
        private static RoadGraphData DosCarriles()
        {
            var g = new RoadGraphData();
            var a0 = g.AddNode(new Vector3(0, 0, 0));
            var a1 = g.AddNode(new Vector3(0, 0, 20));
            var a2 = g.AddNode(new Vector3(0, 0, 40));
            var b0 = g.AddNode(new Vector3(6, 0, 40));
            var b1 = g.AddNode(new Vector3(6, 0, 20));
            var b2 = g.AddNode(new Vector3(6, 0, 0));
            g.Connect(a0.Id, a1.Id); g.Connect(a1.Id, a2.Id);
            g.Connect(b0.Id, b1.Id); g.Connect(b1.Id, b2.Id);
            // Retornos en los extremos (circuito cerrado, como las zonas reales).
            g.Connect(a2.Id, b0.Id); g.Connect(b2.Id, a0.Id);
            return g;
        }

        [Test]
        public void EligeElNodoDeAdelante_NoElDeAtras()
        {
            var g = DosCarriles();
            // En el carril propio, entre el nodo de atrás (z=0) y el de
            // adelante (z=20), un pelo más cerca del de atrás.
            var best = RoutePlanning.BestStartNode(g, new Vector3(0, 0, 9f), Vector3.forward);
            Assert.AreEqual(new Vector3(0, 0, 20), best.Position);
        }

        [Test]
        public void NoEligeElCarrilContrario()
        {
            var g = DosCarriles();
            // Parado en medio de los dos carriles, yendo hacia +Z: el nodo del
            // carril contrario (x=6, z=20) está igual de cerca que el propio,
            // pero sus salidas van a contramano.
            var best = RoutePlanning.BestStartNode(g, new Vector3(3f, 0, 12f), Vector3.forward);
            Assert.AreEqual(0f, best.Position.x, 1e-3f);
        }

        [Test]
        public void SinCandidatosEnElRadio_CaeAlMasCercano()
        {
            var g = DosCarriles();
            var best = RoutePlanning.BestStartNode(g, new Vector3(0, 0, -500f), Vector3.forward);
            Assert.AreEqual(new Vector3(0, 0, 0), best.Position);
        }

        [Test]
        public void SinRumbo_CaeAlMasCercano()
        {
            var g = DosCarriles();
            var best = RoutePlanning.BestStartNode(g, new Vector3(0, 0, 1f), Vector3.zero);
            Assert.AreEqual(new Vector3(0, 0, 0), best.Position);
        }

        [Test]
        public void GrafoVacio_DevuelveNull()
        {
            Assert.IsNull(RoutePlanning.BestStartNode(new RoadGraphData(),
                Vector3.zero, Vector3.forward));
        }
    }
}
