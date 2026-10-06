using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Pasos 4, 5 y 6 del plan de IA: seguimiento por hueco temporal, la acera
    /// deja de ser suelo, y el perfil de la patrulla.
    /// </summary>
    public class NpcTraficoRealistaTests
    {
        private DriverProfile _p;

        [SetUp]
        public void Setup() => _p = DriverProfile.Particular();

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(_p);

        // ---------------- Paso 4: seguimiento por hueco ----------------

        [Test]
        public void ElSeguimientoPorHuecoTemporal_ESTA_REVERTIDO()
        {
            // PROBADO Y REVERTIDO (2026-07-30). El car-following por headway
            // (`(AheadDistance - MinGap)/Headway`) es mejor sobre el papel,
            // pero medido empeoró la Zona Sur: contravía 5/3/5/9 → 14/12 y
            // bloqueados 5/6/1/3 → 14/9, porque el NPC se detiene MÁS a menudo
            // y cada parada dispara la escalera de desatasco, que lo saca del
            // carril. Este test fija la fórmula ACTUAL para que nadie la
            // cambie sin volver a medir el boletín.
            var d = NpcBrain.Decide(new NpcSensors { AheadDistance = _p.MinGap }, _p, true);
            Assert.Greater(d.TargetSpeed, 0f,
                "Con la fórmula de headway el objetivo sería 0 en MinGap. Si esto falla, " +
                "se reconectó el Paso 4: medir el boletín ANTES de darlo por bueno.");
        }

        [Test]
        public void PegadoDeVerdadAlDeAdelante_SeDetiene()
        {
            var s = new NpcSensors { AheadDistance = _p.MinGap * 0.6f };
            var d = NpcBrain.Decide(s, _p, true);
            Assert.AreEqual(0f, d.TargetSpeed, 0.01f);
        }

        [Test]
        public void ElObjetivoCreceDeFormaMONOTONAConElHueco()
        {
            // Más hueco nunca puede dar menos velocidad objetivo. Se conserva
            // aunque el Paso 4 se revirtiera: la fórmula actual también lo
            // cumple, y es la propiedad que hay que exigirle a cualquier
            // sustituta futura.
            float anterior = -1f;
            for (float d = _p.MinGap; d < _p.MinGap * 2.5f; d += 0.25f)
            {
                var dec = NpcBrain.Decide(new NpcSensors { AheadDistance = d }, _p, true);
                Assert.GreaterOrEqual(dec.TargetSpeed, anterior,
                    $"A {d:0.0} m el objetivo bajó respecto al hueco anterior: eso es el acordeón.");
                anterior = dec.TargetSpeed;
            }
        }

        [Test]
        public void ElObjetivoNuncaSuperaElCrucero()
        {
            var d = NpcBrain.Decide(new NpcSensors { AheadDistance = 11.9f }, _p, true);
            Assert.LessOrEqual(d.TargetSpeed, _p.CruiseSpeed);
        }

        // ---------------- Paso 5: la acera no es suelo ----------------

        [Test]
        public void ConCalzadaYAcera_SeQuedaConLaCalzada()
        {
            // El caso real de la esquina: la vereda (0.15 m más alta) se apoya
            // ENCIMA de la pieza de calle, así que "lo más alto pisable" era el
            // bordillo y el auto se subía solo.
            var ys = new[] { 0.10f, 0.25f };
            var acera = new[] { false, true };
            Assert.IsTrue(NpcGroundMath.TryPickCalzada(ys, acera, 0.10f, out float y));
            Assert.AreEqual(0.10f, y, 0.001f, "Con calzada disponible, jamás la acera.");
        }

        [Test]
        public void SoloAcera_SeAceptaAntesQueDejarloSinSuelo()
        {
            var ys = new[] { 0.25f };
            var acera = new[] { true };
            Assert.IsTrue(NpcGroundMath.TryPickCalzada(ys, acera, 0.20f, out float y));
            Assert.AreEqual(0.25f, y, 0.001f,
                "Sin calzada bajo el auto, quedarse sin suelo sería peor: se hundiría.");
        }

        [Test]
        public void SigueDescartandoTechosInalcanzables()
        {
            // No perder la protección vieja: el techo de un arco no es piso.
            var ys = new[] { 0.10f, 9f };
            var acera = new[] { false, false };
            Assert.IsTrue(NpcGroundMath.TryPickCalzada(ys, acera, 0.10f, out float y));
            Assert.AreEqual(0.10f, y, 0.001f);
        }

        [Test]
        public void SinDatosDeAcera_SeComportaComoAntes()
        {
            var ys = new[] { 0.10f, 0.25f };
            Assert.IsTrue(NpcGroundMath.TryPickCalzada(ys, null, 0.10f, out float y));
            Assert.AreEqual(0.25f, y, 0.001f, "Sin clasificación, la regla de siempre.");
        }

        // ---------------- Paso 6: la patrulla ----------------

        [Test]
        public void LaPatrullaSeReconocePorElModelo()
        {
            Assert.IsTrue(NpcFleetRules.EsPatrulla("NPC_" + NpcFleetRules.ModeloPatrulla));
            Assert.IsFalse(NpcFleetRules.EsPatrulla("NPC_Car_3B"));
            Assert.IsFalse(NpcFleetRules.EsPatrulla(null));
        }

        [Test]
        public void LaPatrullaEsImpecable()
        {
            var pat = DriverProfile.Patrullero();
            Assert.AreEqual(1f, pat.LightObedience, "El rojo no se discute.");
            Assert.AreEqual(0f, pat.DoubleParkChance, "Una patrulla no estorba la vía.");
            Assert.Greater(pat.CruiseSpeed, DriverProfile.Particular().CruiseSpeed,
                "Debe leerse como más ágil que un particular.");
            Object.DestroyImmediate(pat);
        }
    }
}
