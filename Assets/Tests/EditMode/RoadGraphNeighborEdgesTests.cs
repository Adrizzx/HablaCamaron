using NUnit.Framework;
using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La adyacencia con costo que reemplazó el escaneo lineal de aristas del
    /// A*. Fija que devuelve los vecinos correctos con su costo = distancia.
    /// </summary>
    public class RoadGraphNeighborEdgesTests
    {
        [Test]
        public void DevuelveVecinosConCostoIgualADistancia()
        {
            var g = new RoadGraphData();
            var a = g.AddNode(Vector3.zero);
            var b = g.AddNode(Vector3.forward * 10f);
            var c = g.AddNode(Vector3.right * 3f);
            g.Connect(a.Id, b.Id);
            g.Connect(a.Id, c.Id);

            var edges = g.NeighborEdges(a.Id);
            Assert.AreEqual(2, edges.Count);

            foreach (var (to, cost) in edges)
            {
                if (to == b.Id) Assert.AreEqual(10f, cost, 1e-3f);
                else if (to == c.Id) Assert.AreEqual(3f, cost, 1e-3f);
                else Assert.Fail($"vecino inesperado: {to}");
            }
        }

        [Test]
        public void RespetaElSentidoDirigido()
        {
            var g = new RoadGraphData();
            var a = g.AddNode(Vector3.zero);
            var b = g.AddNode(Vector3.forward * 5f);
            g.Connect(a.Id, b.Id); // solo a→b

            Assert.AreEqual(1, g.NeighborEdges(a.Id).Count);
            Assert.AreEqual(0, g.NeighborEdges(b.Id).Count, "b no tiene salida hacia a");
        }

        [Test]
        public void SeInvalidaAlAgregarAristas()
        {
            var g = new RoadGraphData();
            var a = g.AddNode(Vector3.zero);
            var b = g.AddNode(Vector3.forward * 5f);
            g.Connect(a.Id, b.Id);
            Assert.AreEqual(1, g.NeighborEdges(a.Id).Count); // cachea

            var c = g.AddNode(Vector3.right * 5f);
            g.Connect(a.Id, c.Id); // debe invalidar el caché
            Assert.AreEqual(2, g.NeighborEdges(a.Id).Count);
        }
    }
}
