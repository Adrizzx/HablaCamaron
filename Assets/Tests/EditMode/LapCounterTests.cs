using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Missions;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Dar la vuelta al redondel es RODEARLO, no alejarse y volver. Estas
    /// pruebas fijan lo que el jugador entiende por "una vuelta".
    /// </summary>
    public class LapCounterTests
    {
        /// <summary>Simula al jugador girando `grados` alrededor del centro.</summary>
        private static int Girar(LapCounter c, float grados, float radio = 20f, int pasos = 360)
        {
            int vueltas = 0;
            for (int i = 0; i <= pasos; i++)
            {
                float a = Mathf.Deg2Rad * (grados * i / pasos);
                if (c.Tick(new Vector2(Mathf.Cos(a) * radio, Mathf.Sin(a) * radio))) vueltas++;
            }
            return vueltas;
        }

        [Test]
        public void UnaVueltaCompleta_CuentaUna()
        {
            var c = new LapCounter();
            Assert.AreEqual(1, Girar(c, 360f));
            Assert.AreEqual(1, c.Laps);
        }

        [Test]
        public void DosVueltas_CuentanDos()
        {
            var c = new LapCounter();
            Assert.AreEqual(2, Girar(c, 720f, pasos: 720));
            Assert.AreEqual(2, c.Laps);
        }

        [Test]
        public void MediaVuelta_NoCuenta()
        {
            var c = new LapCounter();
            Assert.AreEqual(0, Girar(c, 180f));
            Assert.AreEqual(0, c.Laps);
            Assert.Greater(c.Progress, 0.4f, "pero el progreso sí avanza");
        }

        [Test]
        public void IrYVolverSinRodear_NoCuentaVuelta()
        {
            // El error de la mecánica vieja: alejarse y regresar NO es dar la
            // vuelta. Aquí el jugador va y viene por el mismo lado.
            var c = new LapCounter();
            for (int i = 0; i < 200; i++) c.Tick(new Vector2(10f + i * 0.1f, 0f));
            for (int i = 200; i > 0; i--) c.Tick(new Vector2(10f + i * 0.1f, 0f));
            Assert.AreEqual(0, c.Laps);
        }

        [Test]
        public void GirarEnElOtroSentido_TambienCuenta()
        {
            // Un redondel se puede tomar por donde toque: lo que importa es
            // haberlo rodeado.
            var c = new LapCounter();
            Assert.AreEqual(1, Girar(c, -360f));
        }

        [Test]
        public void LejosDelRedondel_NoAcumula()
        {
            // Girar en una plaza al otro lado de la ciudad no cuenta.
            var c = new LapCounter();
            Assert.AreEqual(0, Girar(c, 360f, radio: LapCounter.RingRadius + 30f));
            Assert.AreEqual(0, c.Laps);
        }

        /// <summary>
        /// EL PUNTO ALREDEDOR DEL CUAL SE MIDE IMPORTA (playtest nivel 4: "no
        /// se puede regresar para la segunda vuelta"). El contador acumula el
        /// ángulo respecto a un centro; si el jugador rodea el redondel pero
        /// ese centro queda FUERA del anillo que conduce, el ángulo va y viene
        /// y nunca cierra los 360°: se dan vueltas y no cuenta ninguna.
        /// Pasaba de verdad — la meta `Meta_Redondel` está 14 m descentrada del
        /// redondel real, así que rodearlo pegado (12 m) daba 0 vueltas.
        /// Por eso MissionRunner cuenta alrededor del redondel, no de la meta.
        /// </summary>
        [Test]
        public void SiElCentroQuedaFueraDelAnillo_NoCuentaNingunaVuelta()
        {
            // Se rodea un punto a 12 m, pero midiendo respecto a otro que está
            // 14 m al lado: ese punto NO queda encerrado por el recorrido.
            var c = new LapCounter();
            var desfase = new Vector2(14f, 0f);
            int vueltas = 0;
            for (int i = 0; i <= 720; i++)
            {
                float a = Mathf.Deg2Rad * i; // dos vueltas enteras
                var pos = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12f;
                if (c.Tick(pos - desfase)) vueltas++;
            }
            Assert.AreEqual(0, vueltas,
                "rodear un punto que no está dentro del giro no puede contar vueltas");

            // Y midiendo bien (centro dentro del anillo), las dos salen.
            var b = new LapCounter();
            int buenas = 0;
            for (int i = 0; i <= 720; i++)
            {
                float a = Mathf.Deg2Rad * i;
                if (b.Tick(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12f)) buenas++;
            }
            Assert.AreEqual(2, buenas, "rodeando el centro de verdad, dos vueltas son dos");
        }

        [Test]
        public void SalirDelAnilloYVolver_NoPierdeLoAvanzado()
        {
            // Media vuelta, una escapada, y de vuelta a terminarla.
            var c = new LapCounter();
            Girar(c, 200f);
            for (int i = 0; i < 30; i++) c.Tick(new Vector2(200f, 0f)); // se aleja
            float antes = c.Progress;
            Assert.Greater(antes, 0.4f, "lo acumulado no se borra al salir del anillo");
        }
    }
}
