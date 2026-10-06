using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El emparejador de PUERTOS de vía: la pieza clave para autogenerar el
    /// RoadGraph sobre la ciudad YA CONSTRUIDA de Demo_Scene_1. Cada pieza
    /// aporta puertos (borde + dirección saliente); dos puertos enfrentados
    /// se cosen, los carriles van por la derecha, cada pieza conecta sus
    /// entradas con sus salidas y los extremos sueltos ganan retorno.
    /// </summary>
    public class RoadPortGraphTests
    {
        private static RoadPort P(float x, float z, Vector3 outward, int piece) =>
            new RoadPort
            {
                Position = new Vector3(x, 0f, z),
                Outward = outward.normalized,
                PieceId = piece,
                LaneOffset = 1.5f,
            };

        [Test]
        public void DosRectasAlineadas_SeCosen_YSePuedeCruzarDeUnaAOtra()
        {
            // Recta A de z=0 a z=8; recta B de z=8 a z=16 (borde compartido en 8).
            var ports = new List<RoadPort>
            {
                P(0, 0, Vector3.back, 0), P(0, 8, Vector3.forward, 0),
                P(0, 8, Vector3.back, 1), P(0, 16, Vector3.forward, 1),
            };
            var pairs = RoadPortGraph.Match(ports, tolerance: 1f);
            Assert.AreEqual(1, pairs.Count, "solo el borde z=8 debe coserse");

            var g = new RoadGraphData();
            RoadPortGraph.Build(g, ports, pairs);
            // De la punta sur de A (entrada) a la punta norte de B (salida).
            var desde = g.NearestNode(new Vector3(1.5f, 0f, 0f));
            var hasta = g.NearestNode(new Vector3(1.5f, 0f, 16f));
            Assert.IsTrue(g.IsReachable(desde.Id, hasta.Id), "la costura debe dejar pasar");
        }

        [Test]
        public void ConHuecoMayorALaTolerancia_NoSeCosen()
        {
            var ports = new List<RoadPort>
            {
                P(0, 8, Vector3.forward, 0),
                P(0, 11, Vector3.back, 1), // 3 m de hueco
            };
            Assert.AreEqual(0, RoadPortGraph.Match(ports, 1f).Count);
        }

        [Test]
        public void PuertosCercanos_PeroNoEnfrentados_NoSeCosen()
        {
            // Misma posición pero mirando en la MISMA dirección: no es costura.
            var ports = new List<RoadPort>
            {
                P(0, 8, Vector3.forward, 0),
                P(0, 8, Vector3.forward, 1),
            };
            Assert.AreEqual(0, RoadPortGraph.Match(ports, 1f).Count);
        }

        [Test]
        public void UnaInterseccionT_ConectaSusTresBrazos()
        {
            // La T (pieza 0) con puertos al sur, norte y este.
            var ports = new List<RoadPort>
            {
                P(0, 0, Vector3.back, 0), P(0, 8, Vector3.forward, 0), P(4, 4, Vector3.right, 0),
            };
            var g = new RoadGraphData();
            RoadPortGraph.Build(g, ports, RoadPortGraph.Match(ports, 1f));

            // Entrando por el sur se puede salir por el norte Y por el este.
            // (mano derecha: el que entra desde el sur va por x = +1.5)
            var entradaSur = g.NearestNode(new Vector3(1.5f, 0f, 0f));
            var salidaNorte = g.NearestNode(new Vector3(1.5f, 0f, 8f));
            var salidaEste = g.NearestNode(new Vector3(4f, 0f, 4f - 1.5f));
            Assert.IsTrue(g.IsReachable(entradaSur.Id, salidaNorte.Id));
            Assert.IsTrue(g.IsReachable(entradaSur.Id, salidaEste.Id));
        }

        [Test]
        public void LosCarriles_VanPorLaDerecha()
        {
            // Puerto mirando a +z: el carril de SALIDA va en +x (mano derecha).
            var ports = new List<RoadPort> { P(0, 8, Vector3.forward, 0) };
            var g = new RoadGraphData();
            RoadPortGraph.Build(g, ports, new List<(int, int)>());

            bool haySalidaEnMasX = false;
            foreach (var n in g.Nodes)
                if (n.Position.x > 1f && Mathf.Abs(n.Position.z - 8f) < 0.5f)
                    haySalidaEnMasX = true;
            Assert.IsTrue(haySalidaEnMasX, "la salida por un puerto +z debe ir en +x");
        }

        [Test]
        public void UnPuenteSobreLaCalle_NoSeCose_ConLaCalleDeAbajo()
        {
            // Mismo XZ pero 10 m arriba (la autopista elevada de la demo):
            // coserlos mandaría a los autos a volar entre niveles.
            var arriba = P(0, 8, Vector3.forward, 0);
            arriba.Position += Vector3.up * 10f;
            var ports = new List<RoadPort> { arriba, P(0, 8, Vector3.back, 1) };
            Assert.AreEqual(0, RoadPortGraph.Match(ports, 2f).Count);
        }

        [Test]
        public void UnaPiezaDeUnSoloPuerto_TieneRetornoInterno_SinCallejones()
        {
            // Pieza con un único puerto detectado (pasa en los bordes del
            // puente): quien entra debe poder dar la vuelta adentro y salir.
            var ports = new List<RoadPort> { P(0, 0, Vector3.back, 0) };
            var g = new RoadGraphData();
            RoadPortGraph.Build(g, ports, new List<(int, int)>());

            Assert.AreEqual(0, g.Validate().Count,
                "ni la pieza de un puerto puede dejar nodos sin salida");
        }

        [Test]
        public void UnExtremoSuelto_GanaRetorno_NadieQuedaAtrapado()
        {
            // Una recta sola: sus dos puertos quedan sin coser → retorno en ambos.
            var ports = new List<RoadPort>
            {
                P(0, 0, Vector3.back, 0), P(0, 8, Vector3.forward, 0),
            };
            var g = new RoadGraphData();
            RoadPortGraph.Build(g, ports, new List<(int, int)>());

            // Desde cualquier nodo se puede volver a cualquier otro (circuito).
            foreach (var a in g.Nodes)
                foreach (var b in g.Nodes)
                    Assert.IsTrue(g.IsReachable(a.Id, b.Id),
                        $"de {a.Id} a {b.Id} debería haber camino (retornos)");
        }

        // ---------------- Costura de rescate (islas del escaneo) ----------------

        [Test]
        public void DosIslasSeparadas_ElRescateLasUne_YSePuedeIrDeUnaALaOtra()
        {
            // Dos rectas con un separador de 6 m: Match (tolerancia 4.5) NO las
            // cose y quedan dos islas — el caso real de la ciudad Toon.
            var ports = new List<RoadPort>
            {
                P(0, 0, Vector3.back, 0), P(0, 8, Vector3.forward, 0),
                P(0, 14, Vector3.back, 1), P(0, 22, Vector3.forward, 1),
            };
            var pairs = RoadPortGraph.Match(ports, tolerance: 4.5f);
            Assert.AreEqual(0, pairs.Count, "con 6 m de separador Match no debe coser");

            var extra = RoadPortGraph.StitchComponents(ports, pairs,
                maxDistance: 12f, maxHeightDelta: 1.2f);
            Assert.AreEqual(1, extra.Count, "una sola costura basta para unir dos islas");

            var todas = new List<(int, int)>(pairs);
            todas.AddRange(extra);
            var g = new RoadGraphData();
            RoadPortGraph.Build(g, ports, todas);

            foreach (var a in g.Nodes)
                foreach (var b in g.Nodes)
                    Assert.IsTrue(g.IsReachable(a.Id, b.Id),
                        $"tras el rescate debería haber camino de {a.Id} a {b.Id}");
        }

        [Test]
        public void ElRescateNoDuplicaCosturas_SiYaEstabaTodoUnido()
        {
            // Dos rectas que Match YA cosió: el rescate no tiene nada que hacer.
            var ports = new List<RoadPort>
            {
                P(0, 0, Vector3.back, 0), P(0, 8, Vector3.forward, 0),
                P(0, 8, Vector3.back, 1), P(0, 16, Vector3.forward, 1),
            };
            var pairs = RoadPortGraph.Match(ports, tolerance: 1f);
            Assert.AreEqual(1, pairs.Count);

            var extra = RoadPortGraph.StitchComponents(ports, pairs,
                maxDistance: 12f, maxHeightDelta: 1.2f);
            Assert.AreEqual(0, extra.Count, "ya estaba unido: el rescate no agrega nada");
        }

        [Test]
        public void ElRescateNoCoseUnPuenteConLaCalleDeAbajo()
        {
            // Misma vertical, 6 m de altura de diferencia: son dos niveles
            // distintos y JAMÁS deben unirse (se cruzaría por el aire).
            var alto = new RoadPort
            {
                Position = new Vector3(0f, 6f, 14f),
                Outward = Vector3.back,
                PieceId = 1,
                LaneOffset = 1.5f,
            };
            var ports = new List<RoadPort> { P(0, 8, Vector3.forward, 0), alto };

            var extra = RoadPortGraph.StitchComponents(ports, new List<(int, int)>(),
                maxDistance: 12f, maxHeightDelta: 1.2f);
            Assert.AreEqual(0, extra.Count, "un puente no se cose con la calle de abajo");
        }

        [Test]
        public void ElRescateNoCoseUnaPared()
        {
            // Puertos casi encima (1 m en planta) pero con 1.2 m de desnivel:
            // unirlos daría una arista de ~50°, una PARED que ningún auto sube.
            // Pasa el filtro de altura y aun así debe rechazarse.
            var abajo = new RoadPort
            {
                Position = new Vector3(0f, 0f, 8f),
                Outward = Vector3.forward,
                PieceId = 0,
                LaneOffset = 1.5f,
            };
            var arriba = new RoadPort
            {
                Position = new Vector3(0f, 1.15f, 9f),
                Outward = Vector3.back,
                PieceId = 1,
                LaneOffset = 1.5f,
            };
            var extra = RoadPortGraph.StitchComponents(
                new List<RoadPort> { abajo, arriba }, new List<(int, int)>(),
                maxDistance: 12f, maxHeightDelta: 1.2f);
            Assert.AreEqual(0, extra.Count, "una costura casi vertical no es una vía");
        }

        [Test]
        public void ElRescateSiCoseUnaRampaSuave()
        {
            // Mismo desnivel pero repartido en 12 m: eso sí es una cuesta.
            var abajo = new RoadPort
            {
                Position = new Vector3(0f, 0f, 8f),
                Outward = Vector3.forward,
                PieceId = 0,
                LaneOffset = 1.5f,
            };
            var arriba = new RoadPort
            {
                Position = new Vector3(0f, 1.15f, 19f),
                Outward = Vector3.back,
                PieceId = 1,
                LaneOffset = 1.5f,
            };
            var extra = RoadPortGraph.StitchComponents(
                new List<RoadPort> { abajo, arriba }, new List<(int, int)>(),
                maxDistance: 14f, maxHeightDelta: 1.2f);
            Assert.AreEqual(1, extra.Count, "una rampa suave sí se cose");
        }

        [Test]
        public void ElRescateNoAlcanzaMasAllaDeSuDistancia()
        {
            // Islas a 40 m: eso ya no es una unión, es otro barrio.
            var ports = new List<RoadPort>
            {
                P(0, 0, Vector3.back, 0), P(0, 8, Vector3.forward, 0),
                P(0, 48, Vector3.back, 1), P(0, 56, Vector3.forward, 1),
            };
            var extra = RoadPortGraph.StitchComponents(ports, new List<(int, int)>(),
                maxDistance: 12f, maxHeightDelta: 1.2f);
            Assert.AreEqual(0, extra.Count, "fuera de alcance: no se inventan calles");
        }

        [Test]
        public void UnaPiezaConPuertosADistintoNivel_NoGeneraUnaParedComoCalle()
        {
            // El caso real de la ciudad Toon: el arranque de la autopista
            // elevada tiene sus dos puertos a 8 m de desnivel y 6 m en planta.
            // Unirlos metía en el grafo una "calle" de 53.9° por la que el A*
            // encaminaba al jugador.
            var bajo = new RoadPort
            {
                Position = new Vector3(0f, 0.1f, 0f),
                Outward = Vector3.back,
                PieceId = 0,
                LaneOffset = 1.5f,
            };
            var alto = new RoadPort
            {
                Position = new Vector3(0f, 8.25f, 6f),
                Outward = Vector3.forward,
                PieceId = 0,
                LaneOffset = 1.5f,
            };

            Assert.IsFalse(RoadPortGraph.TramoConducible(bajo.Position, alto.Position),
                "8 m de desnivel en 6 m de planta no es una calle, es una pared");

            var g = new RoadGraphData();
            RoadPortGraph.Build(g, new List<RoadPort> { bajo, alto }, new List<(int, int)>());

            // Ninguna arista del grafo puede ser esa pared.
            foreach (var e in g.Edges)
            {
                var a = g.GetNode(e.FromId);
                var b = g.GetNode(e.ToId);
                Assert.IsTrue(RoadPortGraph.TramoConducible(a.Position, b.Position),
                    $"quedó una arista impracticable de {a.Position} a {b.Position}");
            }
        }

        [Test]
        public void UnaRampaRAZONABLE_SiSeConecta()
        {
            // Y no se pasa de celoso: una pieza en cuesta normal (2 m en 20)
            // tiene que seguir siendo transitable.
            Assert.IsTrue(RoadPortGraph.TramoConducible(
                new Vector3(0f, 0f, 0f), new Vector3(0f, 2f, 20f)));
        }

        [Test]
        public void TresIslasEnFila_SeUnenConDosCosturas_NoConTres()
        {
            // Kruskal: solo las costuras que UNEN islas nuevas (árbol, sin ciclos).
            var ports = new List<RoadPort>
            {
                P(0, 0, Vector3.back, 0), P(0, 8, Vector3.forward, 0),
                P(0, 14, Vector3.back, 1), P(0, 22, Vector3.forward, 1),
                P(0, 28, Vector3.back, 2), P(0, 36, Vector3.forward, 2),
            };
            var extra = RoadPortGraph.StitchComponents(ports, new List<(int, int)>(),
                maxDistance: 12f, maxHeightDelta: 1.2f);
            Assert.AreEqual(2, extra.Count, "tres islas se unen con DOS costuras");
        }

        /// <summary>
        /// Una costura de RESCATE es asfalto tendido sobre lo que no era calle,
        /// así que tiene que costar más que una calle de verdad: A* solo debe
        /// pasar por ahí si no hay alternativa. Sin esto valía lo mismo que una
        /// avenida y el trazado de la misión metía al jugador por encima de la
        /// vereda (playtest nivel 4: "esa parte antes era vereda y le hicieron
        /// calle... y la dirección me dice que vaya por ahí").
        /// </summary>
        [Test]
        public void LasCosturasDeRescate_CuestanMasQueUnaCalleNormal()
        {
            var ports = new List<RoadPort>
            {
                P(0, 0, Vector3.back, 0), P(0, 8, Vector3.forward, 0),
                P(0, 14, Vector3.back, 1), P(0, 22, Vector3.forward, 1),
            };
            var pares = new List<(int, int)>();
            var rescate = RoadPortGraph.StitchComponents(ports, pares,
                maxDistance: 12f, maxHeightDelta: 1.2f);
            Assert.IsNotEmpty(rescate, "las dos piezas sueltas deben coserse");
            pares.AddRange(rescate);

            var normal = new RoadGraphData();
            RoadPortGraph.Build(normal, ports, new List<(int, int)>(pares));
            var penalizado = new RoadGraphData();
            RoadPortGraph.Build(penalizado, ports, new List<(int, int)>(pares), rescate);

            float SumaCostes(RoadGraphData g)
            {
                float s = 0f;
                foreach (var e in g.Edges) s += e.Cost;
                return s;
            }

            Assert.Greater(SumaCostes(penalizado), SumaCostes(normal),
                "cruzar una costura de rescate tiene que salir más caro");
            // Pero sigue siendo transitable: no se corta el mapa.
            Assert.AreEqual(normal.Edges.Count, penalizado.Edges.Count,
                "penalizar NO puede quitar aristas: a veces es el único paso");
        }
    }
}
