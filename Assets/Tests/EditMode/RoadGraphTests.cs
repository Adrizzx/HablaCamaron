using NUnit.Framework;
using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Pruebas del grafo de calles: la estructura sobre la que correrá el A* de
    /// los NPCs (Fase 2). Si el grafo miente (aristas fantasma, nodos trampa,
    /// costos mal calculados), toda la IA miente — por eso se fija aquí.
    /// </summary>
    public class RoadGraphTests
    {
        private RoadGraphData _g;

        [SetUp]
        public void CreateGraph() => _g = new RoadGraphData();

        [Test]
        public void Conectar_CreaAristaConCostoIgualALaDistancia()
        {
            var a = _g.AddNode(Vector3.zero);
            var b = _g.AddNode(new Vector3(10f, 0f, 0f));

            Assert.IsTrue(_g.Connect(a.Id, b.Id));
            Assert.AreEqual(1, _g.Edges.Count);
            Assert.AreEqual(10f, _g.Edges[0].Cost, 0.001f);
        }

        [Test]
        public void ElGrafoEsDirigido_LaVueltaNoExisteSalvoQueSeConecte()
        {
            var a = _g.AddNode(Vector3.zero);
            var b = _g.AddNode(Vector3.forward * 5f);
            _g.Connect(a.Id, b.Id);

            CollectionAssert.Contains(_g.Neighbors(a.Id), b.Id);
            CollectionAssert.DoesNotContain(_g.Neighbors(b.Id), a.Id);
        }

        [Test]
        public void Conectar_RechazaDuplicadasSelfLoopsYNodosInexistentes()
        {
            var a = _g.AddNode(Vector3.zero);
            var b = _g.AddNode(Vector3.one);
            _g.Connect(a.Id, b.Id);

            Assert.IsFalse(_g.Connect(a.Id, b.Id), "duplicada");
            Assert.IsFalse(_g.Connect(a.Id, a.Id), "self-loop");
            Assert.IsFalse(_g.Connect(a.Id, 999), "nodo inexistente");
            Assert.AreEqual(1, _g.Edges.Count);
        }

        [Test]
        public void NearestNode_EncuentraElMasCercano()
        {
            _g.AddNode(new Vector3(0f, 0f, 0f));
            var cerca = _g.AddNode(new Vector3(3f, 0f, 0f));
            _g.AddNode(new Vector3(50f, 0f, 0f));

            Assert.AreEqual(cerca.Id, _g.NearestNode(new Vector3(4f, 0f, 1f)).Id);
        }

        [Test]
        public void IsReachable_SigueElSentidoDeLaVia()
        {
            // a → b → c en un solo sentido.
            var a = _g.AddNode(Vector3.zero);
            var b = _g.AddNode(Vector3.forward * 5f);
            var c = _g.AddNode(Vector3.forward * 10f);
            _g.Connect(a.Id, b.Id);
            _g.Connect(b.Id, c.Id);

            Assert.IsTrue(_g.IsReachable(a.Id, c.Id));
            Assert.IsFalse(_g.IsReachable(c.Id, a.Id), "contravía no debe ser alcanzable");
        }

        [Test]
        public void Validate_DetectaNodosSinSalida()
        {
            var a = _g.AddNode(Vector3.zero);
            var trampa = _g.AddNode(Vector3.forward * 5f);
            _g.Connect(a.Id, trampa.Id); // trampa no tiene salidas

            var problemas = _g.Validate();
            Assert.IsTrue(problemas.Exists(p => p.Contains($"Nodo {trampa.Id}")),
                "Debía reportar el nodo sin salidas");
        }

        [Test]
        public void Validate_GrafoSanoNoReportaNada()
        {
            // Circuito cerrado a → b → a: todos tienen salida.
            var a = _g.AddNode(Vector3.zero);
            var b = _g.AddNode(Vector3.forward * 5f);
            _g.Connect(a.Id, b.Id);
            _g.Connect(b.Id, a.Id);

            Assert.IsEmpty(_g.Validate());
        }
    }
}
