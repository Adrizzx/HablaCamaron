using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El A* es la navegación de todos los NPCs: estas pruebas fijan que
    /// encuentra la ruta ÓPTIMA (no una cualquiera), que respeta el sentido
    /// de las vías y que reconoce cuándo no hay camino.
    /// </summary>
    public class AStarPlannerTests
    {
        private RoadGraphData _g;

        [SetUp]
        public void CreateGraph() => _g = new RoadGraphData();

        [Test]
        public void EncuentraLaRutaDirecta()
        {
            var a = _g.AddNode(Vector3.zero);
            var b = _g.AddNode(Vector3.forward * 10f);
            var c = _g.AddNode(Vector3.forward * 20f);
            _g.Connect(a.Id, b.Id);
            _g.Connect(b.Id, c.Id);

            var path = AStarPlanner.FindPath(_g, a.Id, c.Id);

            Assert.IsNotNull(path);
            CollectionAssert.AreEqual(new[] { a.Id, b.Id, c.Id },
                path.ConvertAll(n => n.Id));
        }

        [Test]
        public void EligeElCaminoMasCorto_NoUnoCualquiera()
        {
            // Dos rutas de a → d: por arriba (corta) y por abajo (larga).
            var a = _g.AddNode(Vector3.zero);
            var corto = _g.AddNode(new Vector3(5f, 0f, 5f));
            var largo1 = _g.AddNode(new Vector3(-20f, 0f, 3f));
            var largo2 = _g.AddNode(new Vector3(-20f, 0f, 8f));
            var d = _g.AddNode(Vector3.forward * 10f);
            _g.Connect(a.Id, corto.Id); _g.Connect(corto.Id, d.Id);
            _g.Connect(a.Id, largo1.Id); _g.Connect(largo1.Id, largo2.Id);
            _g.Connect(largo2.Id, d.Id);

            var path = AStarPlanner.FindPath(_g, a.Id, d.Id);

            CollectionAssert.AreEqual(new[] { a.Id, corto.Id, d.Id },
                path.ConvertAll(n => n.Id), "Debía elegir la ruta corta");
        }

        [Test]
        public void RespetaElSentidoDeLaVia_NoHayRutaEnContravia()
        {
            var a = _g.AddNode(Vector3.zero);
            var b = _g.AddNode(Vector3.forward * 10f);
            _g.Connect(a.Id, b.Id); // solo a → b

            Assert.IsNull(AStarPlanner.FindPath(_g, b.Id, a.Id),
                "Ir en contravía no es una ruta válida");
        }

        [Test]
        public void SinCamino_DevuelveNull()
        {
            var a = _g.AddNode(Vector3.zero);
            var isla = _g.AddNode(Vector3.forward * 100f); // sin conexiones

            Assert.IsNull(AStarPlanner.FindPath(_g, a.Id, isla.Id));
        }

        [Test]
        public void OrigenIgualADestino_RutaDeUnSoloNodo()
        {
            var a = _g.AddNode(Vector3.zero);
            var path = AStarPlanner.FindPath(_g, a.Id, a.Id);
            Assert.AreEqual(1, path.Count);
        }

        [Test]
        public void EnUnAnillo_ViajaEnElSentidoDeCirculacion()
        {
            // Anillo antihorario de 4 nodos: de n0 a n3 hay que dar la vuelta
            // (0→1→2→3), nunca el atajo en contravía (0→3 directo no existe).
            var n = new RoadNode[4];
            for (int i = 0; i < 4; i++)
            {
                float ang = i / 4f * Mathf.PI * 2f;
                n[i] = _g.AddNode(new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 10f);
            }
            for (int i = 0; i < 4; i++) _g.Connect(n[i].Id, n[(i + 1) % 4].Id);

            var path = AStarPlanner.FindPath(_g, n[0].Id, n[3].Id);
            Assert.AreEqual(4, path.Count, "Debía recorrer 0→1→2→3");
        }
    }
}
