using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Missions;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El copiloto visual de las misiones: la instrucción sale del ángulo entre
    /// hacia dónde mira el auto y hacia dónde sigue la ruta, y el waypoint guía
    /// es el primero que queda útilmente adelante.
    /// </summary>
    public class RouteGuideTests
    {
        // ---------- StepFor: la tabla de instrucciones ----------

        [Test]
        public void ObjetivoAlFrente_SigueAdelante()
        {
            Assert.AreEqual(GuideStep.Adelante,
                RouteGuideMath.StepFor(Vector3.forward, Vector3.forward * 20f));
        }

        [Test]
        public void ObjetivoALaIzquierda_GiraIzquierda_YAlaDerecha_GiraDerecha()
        {
            Assert.AreEqual(GuideStep.GiraIzquierda,
                RouteGuideMath.StepFor(Vector3.forward, Vector3.left * 10f));
            Assert.AreEqual(GuideStep.GiraDerecha,
                RouteGuideMath.StepFor(Vector3.forward, Vector3.right * 10f));
        }

        [Test]
        public void ObjetivoAtras_MediaVuelta()
        {
            Assert.AreEqual(GuideStep.MediaVuelta,
                RouteGuideMath.StepFor(Vector3.forward, Vector3.back * 10f));
        }

        [Test]
        public void DesviacionLeve_SigueSiendoAdelante_NoMarea()
        {
            // A 20° del eje no se le pide girar a nadie (umbral: 30°).
            var target = Quaternion.Euler(0f, 20f, 0f) * Vector3.forward * 15f;
            Assert.AreEqual(GuideStep.Adelante,
                RouteGuideMath.StepFor(Vector3.forward, target));
        }

        [Test]
        public void LaAlturaNoImporta_SoloElPlano()
        {
            // La cuesta no debe convertir "adelante" en otra cosa.
            Assert.AreEqual(GuideStep.Adelante,
                RouteGuideMath.StepFor(Vector3.forward, new Vector3(0f, 8f, 20f)));
        }

        [Test]
        public void VectoresDegenerados_NoExplotan()
        {
            Assert.AreEqual(GuideStep.Adelante,
                RouteGuideMath.StepFor(Vector3.zero, Vector3.forward));
            Assert.AreEqual(GuideStep.Adelante,
                RouteGuideMath.StepFor(Vector3.forward, Vector3.zero));
        }

        // ---------- PhraseFor: la frase guiada con distancia ----------

        [Test]
        public void FraseDeGiro_AnticipaConMetros_RedondeadosDeCincoEnCinco()
        {
            Assert.AreEqual("EN 40 m GIRA A LA DERECHA",
                RouteGuideMath.PhraseFor(GuideStep.GiraDerecha, 42f));
            Assert.AreEqual("EN 25 m GIRA A LA IZQUIERDA",
                RouteGuideMath.PhraseFor(GuideStep.GiraIzquierda, 27f));
        }

        [Test]
        public void GiroEncima_SinMetros_LaOrdenConAhora()
        {
            // A menos de 12 m ya no se anticipa: se ordena con AHORA.
            Assert.AreEqual("GIRA A LA DERECHA AHORA",
                RouteGuideMath.PhraseFor(GuideStep.GiraDerecha, 8f));
        }

        [Test]
        public void AdelanteYMediaVuelta_NoLlevanMetros()
        {
            Assert.AreEqual("SIGUE ADELANTE",
                RouteGuideMath.PhraseFor(GuideStep.Adelante, 80f));
            Assert.AreEqual("DA LA VUELTA",
                RouteGuideMath.PhraseFor(GuideStep.MediaVuelta, 40f));
        }

        [Test]
        public void ElColorAvisa_GiroDistintoDeAdelante_MediaVueltaDistintaDeGiro()
        {
            Assert.AreNotEqual(RouteGuideMath.ColorFor(GuideStep.Adelante),
                               RouteGuideMath.ColorFor(GuideStep.GiraDerecha));
            Assert.AreNotEqual(RouteGuideMath.ColorFor(GuideStep.GiraDerecha),
                               RouteGuideMath.ColorFor(GuideStep.MediaVuelta));
            Assert.AreEqual(RouteGuideMath.ColorFor(GuideStep.GiraIzquierda),
                            RouteGuideMath.ColorFor(GuideStep.GiraDerecha),
                            "los dos giros comparten color");
        }

        [Test]
        public void LaFlechaDeCadaPaso_EsLaQueSenalaElSentido()
        {
            Assert.AreEqual("↑", RouteGuideMath.ArrowFor(GuideStep.Adelante));
            Assert.AreEqual("←", RouteGuideMath.ArrowFor(GuideStep.GiraIzquierda));
            Assert.AreEqual("→", RouteGuideMath.ArrowFor(GuideStep.GiraDerecha));
            Assert.AreEqual("⟲", RouteGuideMath.ArrowFor(GuideStep.MediaVuelta));
        }

        // ---------- PickWaypoint: el nodo guía de la ruta ----------

        private static List<RoadNode> Ruta(params Vector3[] puntos)
        {
            var g = new RoadGraphData();
            var list = new List<RoadNode>();
            foreach (var p in puntos) list.Add(g.AddNode(p));
            return list;
        }

        [Test]
        public void EligeElPrimerNodo_QueQuedaUtilmenteAdelante()
        {
            var ruta = Ruta(Vector3.forward * 2f, Vector3.forward * 10f, Vector3.forward * 40f);
            var wp = RouteGuideMath.PickWaypoint(ruta, Vector3.zero, 14f);
            Assert.AreEqual(Vector3.forward * 40f, wp, "los nodos bajo las ruedas no orientan");
        }

        [Test]
        public void RutaCorta_DevuelveElUltimoNodo()
        {
            var ruta = Ruta(Vector3.forward * 2f, Vector3.forward * 6f);
            Assert.AreEqual(Vector3.forward * 6f,
                RouteGuideMath.PickWaypoint(ruta, Vector3.zero, 14f));
        }

        [Test]
        public void SinRuta_DevuelveNull()
        {
            Assert.IsNull(RouteGuideMath.PickWaypoint(null, Vector3.zero, 14f));
            Assert.IsNull(RouteGuideMath.PickWaypoint(new List<RoadNode>(), Vector3.zero, 14f));
        }

        /// <summary>
        /// En una plaza/redondel chico TODA la ruta cabe dentro del look-ahead,
        /// así que antes se devolvía el último nodo —al otro lado del anillo— y
        /// la flecha apuntaba POR ENCIMA DE LA ISLA. Medido en la cima del
        /// nivel 3: "SIGUE ADELANTE" desde 40 m antes de la plaza con la recta
        /// cruzando el árbol del centro (playtest: "indica dirección
        /// incorrecta"). El waypoint tiene que quedarse en el anillo.
        /// </summary>
        [Test]
        public void EnUnRedondelChico_NoApuntaPorEncimaDeLaIsla()
        {
            // Plaza de radio ~15 centrada en (0,0,20); se entra por el sur y se
            // rodea por la derecha hasta la meta, que queda al otro lado.
            var isla = new Vector3(0f, 0f, 20f);
            var meta = new Vector3(0f, 0f, 34f);
            var ruta = Ruta(
                new Vector3(0f, 0f, 5f),    // entrada a la plaza
                new Vector3(10f, 0f, 14f),  // el anillo se abre a la derecha
                new Vector3(14f, 0f, 24f),
                new Vector3(6f, 0f, 32f),
                meta);

            var wp = RouteGuideMath.PickWaypoint(ruta, Vector3.zero, 60f);
            Assert.IsNotNull(wp);
            Assert.AreNotEqual(meta, wp.Value,
                "apuntar a la meta del otro lado manda al jugador sobre la isla");

            // Y la recta hasta el waypoint elegido no puede rozar la isla.
            Vector3 dir = wp.Value; dir.y = 0f;
            float largo = dir.magnitude;
            dir /= largo;
            float sobre = Vector3.Dot(isla, dir);
            float alEje = (isla - dir * sobre).magnitude;
            Assert.Greater(alEje, 8f,
                $"la recta al waypoint pasa a {alEje:0.0} m del centro de la isla");
        }
    }
}
