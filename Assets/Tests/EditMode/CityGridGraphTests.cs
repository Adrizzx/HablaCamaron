using NUnit.Framework;
using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La cuadrícula de la ciudad completa: grafo válido, todo alcanzable en
    /// ambos sentidos, carriles a la DERECHA del sentido de marcha y semáforos
    /// solo en las intersecciones interiores (N/S contra E/O).
    /// </summary>
    public class CityGridGraphTests
    {
        private const int Cols = 5, Rows = 4;
        private const float Spacing = 46f, Lane = 2.6f, Inset = 8f;

        private static RoadGraphData Ciudad(bool luces = true) =>
            CityGridGraph.Build(Cols, Rows, Spacing, Lane, Inset, luces);

        [Test]
        public void ElGrafoEsValido_SinCallejones()
        {
            var problems = Ciudad().Validate();
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void LasEsquinasOpuestas_SeAlcanzanEnAmbosSentidos()
        {
            var g = Ciudad();
            var sw = g.NearestNode(CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, 0, 0));
            var ne = g.NearestNode(CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, Cols - 1, Rows - 1));
            Assert.IsTrue(g.IsReachable(sw.Id, ne.Id), "suroeste → noreste");
            Assert.IsTrue(g.IsReachable(ne.Id, sw.Id), "noreste → suroeste");
        }

        [Test]
        public void LosCarrilesVanALaDerechaDelSentido()
        {
            // Toda arista recta hacia +X (rumbo este) debe correr DESPLAZADA
            // −Z del eje de su calle (derecha del que maneja): z + laneOffset
            // cae exactamente sobre una línea de la cuadrícula.
            var g = Ciudad();
            int rectasEste = 0;
            foreach (var e in g.Edges)
            {
                var a = g.GetNode(e.FromId).Position;
                var b = g.GetNode(e.ToId).Position;
                Vector3 d = b - a;
                if (d.x < 1f || Mathf.Abs(d.z) > 0.01f) continue; // solo rectas al este
                rectasEste++;

                float zEje = a.z + Lane;
                float linea = (zEje - CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, 0, 0).z)
                              / Spacing;
                Assert.AreEqual(Mathf.Round(linea), linea, 1e-3f,
                    $"carril este fuera de su lado en z={a.z}");
            }
            Assert.Greater(rectasEste, 0, "no se encontraron rectas al este");
        }

        [Test]
        public void SemaforosSoloEnInterseccionesInteriores()
        {
            var g = Ciudad();
            int grupo0 = 0, grupo1 = 0;
            float borde = Mathf.Abs(CityGridGraph.IntersectionCenter(Cols, Rows, Spacing, 0, 0).x);
            foreach (var n in g.Nodes)
            {
                if (n.TrafficLightGroup < 0) continue;
                if (n.TrafficLightGroup == 0) grupo0++; else grupo1++;
                Assert.Less(Mathf.Abs(n.Position.x), borde - 1f, "semáforo en el borde");
            }
            Assert.Greater(grupo0, 0, "sin semáforos N/S");
            Assert.Greater(grupo1, 0, "sin semáforos E/O");
        }

        [Test]
        public void SinLuces_NingunNodoTieneGrupo()
        {
            foreach (var n in Ciudad(luces: false).Nodes)
                Assert.AreEqual(-1, n.TrafficLightGroup);
        }

        [Test]
        public void UnaCalleDeUnaCuadra_PermiteElRetorno()
        {
            // 2×1: dos intersecciones unidas por una sola calle. Sin la regla
            // del retorno en U, ambos extremos serían callejones para la IA.
            var g = CityGridGraph.Build(2, 1, Spacing, Lane, Inset, false);
            Assert.IsEmpty(g.Validate());
            var a = g.NearestNode(new Vector3(-Spacing, 0, 0));
            var b = g.NearestNode(new Vector3(+Spacing, 0, 0));
            Assert.IsTrue(g.IsReachable(a.Id, b.Id) && g.IsReachable(b.Id, a.Id));
        }
    }
}
