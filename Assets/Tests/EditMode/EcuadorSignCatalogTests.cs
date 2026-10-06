using System.Collections.Generic;
using NUnit.Framework;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La señalética ecuatoriana de la Fase 4 como dato: el GDD pide ≥20
    /// señales, con claves únicas y coherentes con el grafo (los límites del
    /// catálogo tienen que decir lo mismo que RoadSign.LimitKmh).
    /// </summary>
    public class EcuadorSignCatalogTests
    {
        [Test]
        public void HayAlMenos20Senales_ComoPideElGdd()
        {
            Assert.GreaterOrEqual(EcuadorSignCatalog.All.Length, 20);
        }

        [Test]
        public void LasClaves_SonUnicas_YGetLasEncuentra()
        {
            var vistas = new HashSet<string>();
            foreach (var s in EcuadorSignCatalog.All)
            {
                Assert.IsTrue(vistas.Add(s.Key), $"clave repetida: {s.Key}");
                Assert.AreSame(s, EcuadorSignCatalog.Get(s.Key));
            }
            Assert.IsNull(EcuadorSignCatalog.Get("no_existe"));
        }

        [Test]
        public void TodaSenal_TieneTextoYLeyenda()
        {
            foreach (var s in EcuadorSignCatalog.All)
            {
                Assert.IsNotEmpty(s.Texto, s.Key);
                Assert.IsNotEmpty(s.Leyenda, s.Key);
            }
        }

        [Test]
        public void LosLimites_DicenLoMismoQueElGrafo()
        {
            // La placa "30" debe castigar como 30: catálogo y RoadSign.LimitKmh
            // son la misma verdad.
            Assert.AreEqual(30, RoadSign.LimitKmh(EcuadorSignCatalog.Get("lim30").Efecto));
            Assert.AreEqual(50, RoadSign.LimitKmh(EcuadorSignCatalog.Get("lim50").Efecto));
            Assert.AreEqual(90, RoadSign.LimitKmh(EcuadorSignCatalog.Get("lim90").Efecto));
        }

        [Test]
        public void LasFuncionalesClave_LlevanSuEfecto()
        {
            Assert.AreEqual(SignType.Stop, EcuadorSignCatalog.Get("pare").Efecto);
            Assert.AreEqual(SignType.Yield, EcuadorSignCatalog.Get("ceda").Efecto);
        }

        [Test]
        public void LasProhibiciones_LlevanSuBandaDiagonal()
        {
            Assert.IsTrue(EcuadorSignCatalog.Get("no_pitar").Tachada);
            Assert.IsTrue(EcuadorSignCatalog.Get("no_estacionar").Tachada);
            Assert.IsFalse(EcuadorSignCatalog.Get("pare").Tachada, "el PARE no va tachado");
        }

        // ---- Fidelidad al RTE INEN 004-1 (playtest: "no son las de verdad") ----

        [Test]
        public void LosLimites_SonRectangularesBlancos_ComoEnEcuador()
        {
            // Ecuador sigue el modelo INTERAMERICANO: placa rectangular blanca
            // con orla negra, "MÁXIMA" arriba y "km/h" abajo. El círculo con
            // anillo rojo es la señal europea (Convención de Viena) y estuvo
            // mal dibujada hasta el 2026-07-25.
            foreach (var key in new[] { "lim30", "lim50", "lim90" })
            {
                var s = EcuadorSignCatalog.Get(key);
                Assert.AreEqual(SignShape.Rectangulo, s.Shape, key);
                Assert.IsFalse(s.Tachada, key);
                StringAssert.Contains("MÁXIMA", s.Texto, key);
                StringAssert.Contains("km/h", s.Texto, key);
            }
        }

        [Test]
        public void ElPare_EsOctagonoRojo_YElCeda_TrianguloBlanco()
        {
            var pare = EcuadorSignCatalog.Get("pare");
            Assert.AreEqual(SignShape.Octagono, pare.Shape);
            Assert.AreEqual("PARE", pare.Texto);

            var ceda = EcuadorSignCatalog.Get("ceda");
            Assert.AreEqual(SignShape.Triangulo, ceda.Shape);
            StringAssert.Contains("CEDA EL PASO", ceda.Texto.Replace("\n", " "),
                "la señal ecuatoriana dice CEDA EL PASO completo");
        }

        [Test]
        public void LasPreventivas_SonRombosAmarillos()
        {
            foreach (var key in new[] { "curva", "resalto", "zona_escolar", "peatones" })
                Assert.AreEqual(SignShape.Rombo, EcuadorSignCatalog.Get(key).Shape, key);
        }
    }
}
