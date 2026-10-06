using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Missions;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La regla del paso cebra: bloquear el cruce (detenido encima, semáforo
    /// en rojo) castiga UNA vez por estadía; pisarla rodando o en verde, no.
    /// </summary>
    public class CrosswalkJudgeTests
    {
        [Test]
        public void PasarRodandoPorLaCebra_NoCastiga()
        {
            var j = new CrosswalkJudge();
            Assert.IsFalse(j.Tick(onCrosswalk: true, lightIsRed: true, speedKmh: 20f, dt: 2f));
        }

        [Test]
        public void DetenidoEnRojoSobreLaCebra_CastigaUnaVez_YNoAmetralla()
        {
            var j = new CrosswalkJudge();
            Assert.IsFalse(j.Tick(true, true, 0f, 1.0f));
            Assert.IsTrue(j.Tick(true, true, 0f, 0.6f), "a los 1.6 s bloqueando: infracción");
            Assert.IsFalse(j.Tick(true, true, 0f, 5f), "la misma estadía no re-castiga");
        }

        [Test]
        public void DetenidoEnVerde_NoEsBloqueo()
        {
            var j = new CrosswalkJudge();
            Assert.IsFalse(j.Tick(true, lightIsRed: false, 0f, 5f));
        }

        [Test]
        public void SalirYVolverAtascarse_Rearma()
        {
            var j = new CrosswalkJudge();
            j.Tick(true, true, 0f, 2f);            // castigó
            j.Tick(false, true, 0f, 0.5f);         // salió de la cebra
            Assert.IsFalse(j.Tick(true, true, 0f, 1.0f));
            Assert.IsTrue(j.Tick(true, true, 0f, 0.6f), "nueva estadía = nueva falta");
        }

        [Test]
        public void LaCajaDeLaCebra_ContieneEnLocal_XZ()
        {
            var go = new GameObject("CebraTest");
            var cw = go.AddComponent<Crosswalk>();
            cw.HalfExtents = new Vector3(4f, 2f, 1.5f);
            go.transform.position = new Vector3(10f, 0f, 5f);
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // girada

            Assert.IsTrue(cw.Contains(new Vector3(10f, 0f, 5f)));
            // Girada 90°: su "ancho" x local corre a lo largo de z mundial.
            Assert.IsTrue(cw.Contains(new Vector3(10f, 0f, 8.5f)));
            Assert.IsFalse(cw.Contains(new Vector3(10f, 0f, 9.8f)));
            Assert.IsFalse(cw.Contains(new Vector3(12f, 0f, 5f)));
            Object.DestroyImmediate(go);
        }
    }
}
