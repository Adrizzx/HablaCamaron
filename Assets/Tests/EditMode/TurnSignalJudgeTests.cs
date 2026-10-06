using NUnit.Framework;
using HablaCamaron.Missions;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El juez de direccionales (GDD: penalizar girar sin avisar) caso por
    /// caso. Se simula el giro alimentando Tick() frame a frame, igual que
    /// hace PlayerInfractions en el juego.
    /// </summary>
    public class TurnSignalJudgeTests
    {
        private const float Dt = 1f / 60f;

        /// <summary>Simula girar 'grados' totales en 'segundos', con la direccional dada.</summary>
        private static bool Girar(TurnSignalJudge j, float grados, float segundos,
                                  int blinker = 0, bool moving = true)
        {
            int frames = (int)(segundos / Dt);
            float porFrame = grados / frames;
            bool omision = false;
            for (int i = 0; i < frames; i++)
                omision |= j.Tick(porFrame, blinker, moving, Dt);
            return omision;
        }

        [Test]
        public void GiroFrancoSinDireccional_EsOmision()
        {
            var j = new TurnSignalJudge();
            Assert.IsTrue(Girar(j, 90f, 1.5f), "giró 90° sin avisar");
            Assert.AreEqual(1, j.Misses);
        }

        [Test]
        public void GiroConLaDireccionalCorrecta_NoCastiga()
        {
            var j = new TurnSignalJudge();
            Assert.IsFalse(Girar(j, 90f, 1.5f, blinker: 1), "derecha con direccional derecha");
            Assert.AreEqual(0, j.Misses);
        }

        [Test]
        public void AvisarUnosSegundosAntes_TambienVale()
        {
            var j = new TurnSignalJudge();
            // Direccional derecha encendida 1 s yendo recto...
            Girar(j, 0f, 1f, blinker: 1);
            // ...se apaga y ENSEGUIDA gira (la memoria de 3 s lo cubre).
            Assert.IsFalse(Girar(j, 90f, 1.5f, blinker: 0));
            Assert.AreEqual(0, j.Misses);
        }

        [Test]
        public void LaDireccionalEquivocada_NoSalva()
        {
            var j = new TurnSignalJudge();
            // Gira a la DERECHA (+) avisando a la izquierda (-1).
            Assert.IsTrue(Girar(j, 90f, 1.5f, blinker: -1));
            Assert.AreEqual(1, j.Misses);
        }

        [Test]
        public void GiroALaIzquierda_TambienSeJuzga()
        {
            var j = new TurnSignalJudge();
            Assert.IsTrue(Girar(j, -90f, 1.5f), "izquierda sin avisar");
            Assert.IsFalse(Girar(new TurnSignalJudge(), -90f, 1.5f, blinker: -1),
                "izquierda avisando a la izquierda");
        }

        [Test]
        public void CurvaSuaveYLarga_SePerdonaPorElDecaimiento()
        {
            var j = new TurnSignalJudge();
            // 90° repartidos en 12 s: el decaimiento (12°/s) come el acumulado.
            Assert.IsFalse(Girar(j, 90f, 12f));
            Assert.AreEqual(0, j.Misses);
        }

        [Test]
        public void ManiobrarDetenido_NoEsGiro()
        {
            var j = new TurnSignalJudge();
            Assert.IsFalse(Girar(j, 180f, 2f, moving: false), "parqueo no se juzga");
            Assert.AreEqual(0, j.Misses);
        }

        [Test]
        public void DosGirosSeguidos_ElCooldownEvitaAmetrallar()
        {
            var j = new TurnSignalJudge();
            Girar(j, 90f, 1.5f);            // primera omisión
            Girar(j, 90f, 1.5f);            // dentro de la tregua de 5 s
            Assert.AreEqual(1, j.Misses, "el redondel no debe ametrallar");
            Girar(j, 0f, TurnSignalJudge.Cooldown, blinker: 0); // pasa la tregua
            Girar(j, 90f, 1.5f);
            Assert.AreEqual(2, j.Misses, "pasada la tregua vuelve a juzgar");
        }
    }
}
