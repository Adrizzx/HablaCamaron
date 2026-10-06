using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Missions;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El corredor de ruta: en la ciudad grande la misión tiene que llevarte
    /// por su camino, no dejarte pasear. Pero perdona los desvíos cortos —
    /// esquivar un bus o cortar una esquina no puede costar el nivel.
    /// </summary>
    public class RouteCorridorJudgeTests
    {
        private static List<Vector3> RutaRecta() => new List<Vector3>
        {
            new Vector3(0, 0, 0), new Vector3(0, 0, 50), new Vector3(0, 0, 100),
        };

        [Test]
        public void SobreLaRuta_LaDistanciaEsCero()
        {
            Assert.AreEqual(0f, RouteCorridorJudge.DistanceToRoute(
                RutaRecta(), new Vector3(0, 0, 30)), 0.01f);
        }

        [Test]
        public void LaDistanciaIgnoraLaAltura()
        {
            // Un paso a desnivel encima de la ruta sigue estando "en" la ruta:
            // el corredor se mide en planta, como las calles.
            Assert.AreEqual(0f, RouteCorridorJudge.DistanceToRoute(
                RutaRecta(), new Vector3(0, 12, 30)), 0.01f);
        }

        [Test]
        public void FueraDeLaRuta_MideLaSeparacionLateral()
        {
            Assert.AreEqual(40f, RouteCorridorJudge.DistanceToRoute(
                RutaRecta(), new Vector3(40, 0, 30)), 0.01f);
        }

        [Test]
        public void SinRuta_NoSePuedeJuzgar()
        {
            Assert.AreEqual(float.MaxValue,
                RouteCorridorJudge.DistanceToRoute(null, Vector3.zero));
            Assert.AreEqual(float.MaxValue,
                RouteCorridorJudge.DistanceToRoute(new List<Vector3>(), Vector3.zero));
        }

        [Test]
        public void DentroDelCorredor_NuncaMolesta()
        {
            var juez = new RouteCorridorJudge();
            for (int i = 0; i < 200; i++)
                Assert.AreEqual(CorridorVerdict.OnRoute, juez.Tick(10f, 0.1f));
        }

        [Test]
        public void UnDesvioCorto_SePerdona()
        {
            // Dos segundos fuera (esquivar algo) y de vuelta: sin aviso.
            var juez = new RouteCorridorJudge();
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(CorridorVerdict.OnRoute, juez.Tick(80f, 0.1f));

            Assert.AreEqual(CorridorVerdict.OnRoute, juez.Tick(5f, 0.1f));
            Assert.AreEqual(0f, juez.OutFor, "volver a la ruta reinicia la cuenta");
        }

        [Test]
        public void InsistirFueraDeLaRuta_PrimeroAvisa()
        {
            var juez = new RouteCorridorJudge();
            CorridorVerdict v = CorridorVerdict.OnRoute;
            for (int i = 0; i < 40; i++) v = juez.Tick(80f, 0.1f);
            Assert.AreEqual(CorridorVerdict.Straying, v,
                "a los ~3 s fuera debe avisar, todavía sin reprobar");
        }

        [Test]
        public void IrseDeLaRuta_TerminaPerdiendoLaMision()
        {
            var juez = new RouteCorridorJudge();
            CorridorVerdict v = CorridorVerdict.OnRoute;
            for (int i = 0; i < 200; i++) v = juez.Tick(120f, 0.1f);
            Assert.AreEqual(CorridorVerdict.Lost, v);
        }

        [Test]
        public void VolverAtiempo_SalvaLaMision()
        {
            // Se desvía 10 s (ya avisado) pero vuelve: no se pierde nada.
            var juez = new RouteCorridorJudge();
            for (int i = 0; i < 100; i++) juez.Tick(80f, 0.1f);
            Assert.AreEqual(CorridorVerdict.OnRoute, juez.Tick(3f, 0.1f));

            // Y desde cero puede volver a desviarse sin heredar la cuenta.
            for (int i = 0; i < 40; i++) juez.Tick(80f, 0.1f);
            Assert.Less(juez.OutFor, RouteCorridorJudge.LostSeconds);
        }
    }
}
