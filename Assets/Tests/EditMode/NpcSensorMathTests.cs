using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El sensor frontal del NPC (SphereCast de "bigotes") en pendiente: el
    /// origen debe elevarse sobre la normal de la vía y la dirección debe
    /// acompañar la pendiente para que el cast nunca muerda el propio
    /// asfalto (playtest: NPCs parados en la cuesta sin razón).
    /// </summary>
    public class NpcSensorMathTests
    {
        [Test]
        public void EnPlanoElRayoSigueSiendoHorizontal()
        {
            var (o, d) = NpcSensorMath.ForwardRay(Vector3.zero, Vector3.forward, Vector3.up);
            Assert.AreEqual(0f, d.y, 1e-4f);
            Assert.Greater(o.y, 0.79f); // el origen sube al menos el radio del cast
        }

        [Test]
        public void EnCuestaLaDireccionAcompanaLaPendiente()
        {
            // Rampa de 16° (la "pared" de la cuesta del nivel 3).
            var normal = Quaternion.AngleAxis(-16f, Vector3.right) * Vector3.up;
            var (o, d) = NpcSensorMath.ForwardRay(Vector3.zero, Vector3.forward, normal);
            // Proyectado sobre el plano de la vía: el rayo sube CON la rampa
            // en vez de clavarse en el asfalto.
            Assert.Greater(d.y, 0.2f);
            Assert.AreEqual(1f, d.magnitude, 1e-3f);
        }

        [Test]
        public void ConElCastDeOchoDecimasElAsfaltoQuedaFueraDelRadio()
        {
            var normal = Quaternion.AngleAxis(-16f, Vector3.right) * Vector3.up;
            var (o, d) = NpcSensorMath.ForwardRay(Vector3.zero, Vector3.forward, normal);
            // A 12 m de rango, la distancia del eje del cast al plano de la vía
            // nunca baja del radio (0.8): sin falso "obstáculo".
            for (float t = 0f; t <= 12f; t += 1f)
            {
                var p = o + d * t;
                float alPlano = Vector3.Dot(p, normal); // plano pasa por el origen
                Assert.GreaterOrEqual(alPlano, 0.8f - 1e-3f, $"a {t} m del morro");
            }
        }
    }
}
