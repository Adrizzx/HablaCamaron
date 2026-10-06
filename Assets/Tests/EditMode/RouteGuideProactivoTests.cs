using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Missions;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La navegación proactiva del GPS: se mira lejos (60 m) para anticipar
    /// el giro, se anuncia con metros (50 m), y la flecha se acuesta en el
    /// asfalto orientando el sentido de la curva con suavidad en opacidad.
    /// </summary>
    public class RouteGuideProactivoTests
    {
        [Test]
        public void ElGiroSeAnunciaDesdeCincuentaMetros()
        {
            // El playtest: "avisa muy tarde". A 50 m ya debe decir los metros.
            Assert.AreEqual("EN 50 m GIRA A LA DERECHA",
                RouteGuideMath.PhraseFor(GuideStep.GiraDerecha, 50f));
            Assert.AreEqual("EN 30 m GIRA A LA IZQUIERDA",
                RouteGuideMath.PhraseFor(GuideStep.GiraIzquierda, 31f)); // redondeo de 5 en 5
        }

        [Test]
        public void EncimaDelGiroLaOrdenEsAhora()
        {
            Assert.AreEqual("GIRA A LA DERECHA AHORA",
                RouteGuideMath.PhraseFor(GuideStep.GiraDerecha, 6f));
        }

        [Test]
        public void SeMiraMasLejosDeLoQueSeAnuncia()
        {
            // Buscar el giro a 60 m permite anunciarlo a 50 sin sorpresas.
            Assert.Greater(RouteGuideMath.LookAheadMeters, RouteGuideMath.AnnounceMeters);
            Assert.AreEqual(60f, RouteGuideMath.LookAheadMeters, 1e-4f);
            Assert.AreEqual(50f, RouteGuideMath.AnnounceMeters, 1e-4f);
        }

        [Test]
        public void LaFlechaSeAcuestaEnElAsfaltoMirandoElGiro()
        {
            var haciaElGiro = new Vector3(1f, 0f, 0f);
            var rot = RouteGuideMath.ArrowRotation(haciaElGiro);
            // Cara hacia el cielo (se ve desde la cabina) y "arriba" de la
            // textura apuntando a donde hay que doblar.
            Assert.AreEqual(1f, Vector3.Dot(rot * Vector3.forward, Vector3.up), 1e-3f);
            Assert.AreEqual(1f, Vector3.Dot(rot * Vector3.up, haciaElGiro), 1e-3f);
        }

        [Test]
        public void LaFlechaSeVeSoloCercaDelGiro()
        {
            Assert.AreEqual(0f, RouteGuideMath.ArrowAlpha(80f), 1e-3f);  // lejos: nada
            Assert.AreEqual(1f, RouteGuideMath.ArrowAlpha(10f), 1e-3f);  // encima: llena
            float media = RouteGuideMath.ArrowAlpha(30f);
            Assert.Greater(media, 0f);
            Assert.Less(media, 1f);
        }
    }
}
