using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Sentido de giro de los redondeles. Ecuador conduce por la derecha, así
    /// que se circulan en ANTIHORARIO visto desde arriba. El nivel 3 tenía la
    /// plaza de la cima al revés y las flechas de la guía mandaban a rodearla
    /// por el lado contrario (playtest, con foto).
    /// </summary>
    public class RingOrientationTests
    {
        /// <summary>Anillo de 8 nodos como los construyen los builders.</summary>
        private static List<Vector3> Anillo(bool comoElRedondelCentral, float radio = 20f)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                // El central usa (cos, sin); la cima usaba (sin, cos), que es
                // la MISMA circunferencia recorrida al revés.
                pts.Add(comoElRedondelCentral
                    ? new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio)
                    : new Vector3(Mathf.Sin(a) * radio, 0f, Mathf.Cos(a) * radio));
            }
            return pts;
        }

        [Test]
        public void ElPatronDelRedondelCentral_EsAntihorario()
        {
            Assert.IsTrue(RingOrientation.EsAntihorario(Anillo(true)),
                "(cos, sin) es el patrón bueno: es el que usa RoadStripKit.BuildRingNodes.");
        }

        [Test]
        public void ElPatronESPEJADO_SeDetectaComoHorario()
        {
            // Esta es la prueba que habría cazado el bug del nivel 3. Las dos
            // fórmulas "recorren un círculo", y leyendo el código no se ve la
            // diferencia: hay que medirla.
            Assert.IsFalse(RingOrientation.EsAntihorario(Anillo(false)),
                "(sin, cos) recorre el mismo círculo AL REVÉS: es sentido horario.");
        }

        [Test]
        public void ElAreaCambiaDeSignoAlInvertirElRecorrido()
        {
            var bueno = Anillo(true);
            var alReves = new List<Vector3>(bueno);
            alReves.Reverse();
            Assert.AreEqual(-RingOrientation.AreaConSigno(bueno),
                             RingOrientation.AreaConSigno(alReves), 0.01f);
        }

        [Test]
        public void ElAreaAbsolutaEsLaDelCirculo()
        {
            // Un octógono inscrito en r=20 tiene 2·√2·r² ≈ 1131 m².
            float area = Mathf.Abs(RingOrientation.AreaConSigno(Anillo(true, 20f)));
            Assert.AreEqual(2f * Mathf.Sqrt(2f) * 400f, area, 1f);
        }

        [Test]
        public void UnAnilloDegenerado_NoPasaPorAntihorario()
        {
            Assert.IsFalse(RingOrientation.EsAntihorario(null));
            Assert.IsFalse(RingOrientation.EsAntihorario(new List<Vector3>()));
            Assert.IsFalse(RingOrientation.EsAntihorario(new List<Vector3>
            {
                Vector3.zero, new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, 10f), // alineados
            }), "Tres puntos en línea no son un anillo: mejor que salte el invariante.");
        }

        [Test]
        public void LaAlturaNoAfectaAlSentido()
        {
            // Los nodos de la cima están a la altura del cerro, no en y=0.
            var enAlto = Anillo(true);
            for (int i = 0; i < enAlto.Count; i++)
                enAlto[i] = new Vector3(enAlto[i].x, 47.3f, enAlto[i].z);
            Assert.IsTrue(RingOrientation.EsAntihorario(enAlto));
        }
    }
}
