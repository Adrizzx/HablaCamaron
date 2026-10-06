using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Reglas de aparición y retirada de NPCs (Paso 1 del plan de IA). El
    /// playtest reportaba "hay NPCs que aparecen de la nada" y el boletín lo
    /// midió: en la Zona Sur, el 100% de los nacimientos caía dentro del
    /// frustum de la cámara (6/6, 7/7, 8/8, 9/9 en cuatro corridas).
    /// El "frustum" de estos tests es una CAJA construida a mano (seis planos
    /// con la normal hacia adentro): TestPlanesAABB no distingue una caja de
    /// una pirámide, y así el test no depende de una cámara ni de una escena.
    /// </summary>
    public class NpcSpawnRulesTests
    {
        // Caja de visión: x ∈ [-10, 10], y ∈ [-10, 10], z ∈ [0, 100].
        private static Plane[] CajaDeVision() => new[]
        {
            new Plane(Vector3.right, 10f),    // x > -10
            new Plane(Vector3.left, 10f),     // x <  10
            new Plane(Vector3.up, 10f),       // y > -10
            new Plane(Vector3.down, 10f),     // y <  10
            new Plane(Vector3.forward, 0f),   // z >   0
            new Plane(Vector3.back, 100f),    // z < 100
        };

        // ---------------- Visibilidad ----------------

        [Test]
        public void LoQueEstaDeFrente_CuentaComoVisible()
        {
            Assert.IsTrue(NpcSpawnRules.EnFrustum(CajaDeVision(), new Vector3(0f, 0f, 50f)));
        }

        [Test]
        public void LoQueEstaMuyALado_NoEsVisible()
        {
            Assert.IsFalse(NpcSpawnRules.EnFrustum(CajaDeVision(), new Vector3(60f, 0f, 50f)));
        }

        [Test]
        public void LoQueQuedaAtras_NoEsVisible()
        {
            Assert.IsFalse(NpcSpawnRules.EnFrustum(CajaDeVision(), new Vector3(0f, 0f, -40f)));
        }

        [Test]
        public void ElBordeJustoDeLaVista_TambienCuentaComoVisible()
        {
            // A 14 m del eje, la caja de visión llega a 10: sin margen esto
            // sería "invisible" y el auto aparecería en cuanto el jugador
            // girara un poco la cabeza. El margen de seguridad lo rechaza.
            Assert.IsTrue(NpcSpawnRules.EnFrustum(CajaDeVision(), new Vector3(14f, 0f, 50f)),
                "El margen de seguridad debe tratar el borde de la vista como visible.");
        }

        [Test]
        public void SinCamara_NadaCuentaComoVisible()
        {
            // Escena que todavía no creó la DriverCamera: no se puede bloquear
            // el tráfico entero, o la ciudad se queda vacía.
            Assert.IsFalse(NpcSpawnRules.EnFrustum(null, Vector3.zero));
            Assert.IsFalse(NpcSpawnRules.EnFrustum(new Plane[0], Vector3.zero));
        }

        [Test]
        public void TrasUnEdificio_NoSeLeVeAunqueEsteEnElCono()
        {
            // La distinción no es cosmética: medido con SpawnVisibilidadDiagTests,
            // en la Zona Sur NINGUNO de los 15 nodos en rango queda fuera del
            // frustum (el jugador arranca en el garaje mirando la única calle),
            // pero 4 están tapados. Con el criterio de solo-frustum esa zona se
            // quedaba con CERO tráfico ambiental.
            Assert.IsFalse(NpcSpawnRules.SeLeVeria(enFrustum: true, hayLineaDeVista: false));
            Assert.IsTrue(NpcSpawnRules.SeLeVeria(enFrustum: true, hayLineaDeVista: true));
            Assert.IsFalse(NpcSpawnRules.SeLeVeria(enFrustum: false, hayLineaDeVista: true),
                "Fuera del cono da igual que no haya nada de por medio.");
        }

        // ---------------- Aparición ----------------

        [Test]
        public void NoNaceDondeElJugadorEstaMirando()
        {
            Assert.IsFalse(NpcSpawnRules.PuedeNacer(70f, 45f, 110f, seLeVeria: true));
        }

        [Test]
        public void NaceEnElAnilloSiNadieLoVe()
        {
            Assert.IsTrue(NpcSpawnRules.PuedeNacer(70f, 45f, 110f, seLeVeria: false));
        }

        [Test]
        public void NoNaceDemasiadoCercaNiDemasiadoLejos()
        {
            Assert.IsFalse(NpcSpawnRules.PuedeNacer(10f, 45f, 110f, seLeVeria: false),
                "Encima del jugador: aparecería dentro del retrovisor.");
            Assert.IsFalse(NpcSpawnRules.PuedeNacer(300f, 45f, 110f, seLeVeria: false),
                "Tan lejos que se despawnearía antes de servir de nada.");
        }

        [Test]
        public void FueraDeLaVista_SeRelajaElRadioMinimo()
        {
            // El mínimo de 45 m era un PROXY de "que no lo vean nacer".
            // Comprobada la visibilidad de verdad, exigir las dos cosas dejó la
            // Zona Sur con CERO tráfico ambiental (medido: 0 spawns en 4
            // corridas, porque todo su anillo 45-110 m cae en el frustum).
            Assert.IsTrue(NpcSpawnRules.PuedeNacer(30f, 45f, 110f, seLeVeria: false),
                "A 30 m POR DETRÁS del jugador no se ve nacer a nadie: es un spawn legítimo.");
            Assert.IsFalse(NpcSpawnRules.PuedeNacer(30f, 45f, 110f, seLeVeria: true),
                "...pero si lo está mirando, no nace, por cerca o lejos que esté.");
        }

        [Test]
        public void LosBordesDelAnilloSonValidos()
        {
            Assert.IsTrue(NpcSpawnRules.DistanciaValida(45f, 45f, 110f));
            Assert.IsTrue(NpcSpawnRules.DistanciaValida(110f, 45f, 110f));
        }

        // ---------------- Retirada ----------------

        [Test]
        public void NoSeRetiraLoQueElJugadorEstaViendo()
        {
            Assert.IsFalse(NpcSpawnRules.DebeRetirarse(200f, 150f, seLeVeria: true),
                "Desaparecer a la vista es tan feo como aparecer a la vista.");
        }

        [Test]
        public void SeRetiraLoLejanoQueNadieVe()
        {
            Assert.IsTrue(NpcSpawnRules.DebeRetirarse(200f, 150f, seLeVeria: false));
        }

        [Test]
        public void NoSeRetiraLoQueSigueCerca()
        {
            Assert.IsFalse(NpcSpawnRules.DebeRetirarse(100f, 150f, seLeVeria: false));
        }

        [Test]
        public void PasadoElTopeDuro_SeRetiraAunqueSeVea()
        {
            // Sin este tope, un jugador parado mirando una avenida recta
            // acumularía NPCs sin límite: fuga disfrazada de "no desaparecer".
            float lejisimos = 150f * NpcSpawnRules.FactorDespawnDuro + 1f;
            Assert.IsTrue(NpcSpawnRules.DebeRetirarse(lejisimos, 150f, seLeVeria: true));
        }
    }
}
