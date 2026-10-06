using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Tiempo-a-colisión entre dos vehículos con rumbos cualesquiera (Paso 2
    /// del plan de IA). Es la pregunta que faltaba: no "¿está en mi cono?",
    /// sino "¿vamos a coincidir en el mismo sitio?".
    /// </summary>
    public class NpcConflictMathTests
    {
        private const float R = NpcConflictMath.RadioConflicto;

        [Test]
        public void DeFrenteYAcercandose_DaElTiempoDelEncuentro()
        {
            // Él 20 m adelante, viniendo hacia mí a 10 m/s de cierre.
            // El contrato es el instante de MÁXIMA APROXIMACIÓN (cuando la
            // distancia deja de bajar), no el del primer contacto: aquí, los
            // 20 m a 10 m/s = 2 s, no (20-radio)/10. Esta prueba se escribió
            // mal la primera vez y la cazó la suite; se corrige la EXPECTATIVA,
            // no la función, porque todo lo demás (y el frenado del cerebro)
            // asume el mismo criterio que ya usa AheadDistance/AheadClosingSpeed.
            float t = NpcConflictMath.TiempoAColision(new Vector2(0f, 20f), new Vector2(0f, -10f), R);
            Assert.AreEqual(2f, t, 0.05f);
        }

        [Test]
        public void EnCruzYCoincidiendo_HayConflicto()
        {
            // Yo voy hacia +X (no aparece aquí: todo es relativo). Él está 20 m
            // a mi derecha y cruza hacia mí; en ~2 s ocupan el mismo punto.
            float t = NpcConflictMath.TiempoAColision(new Vector2(20f, -20f), new Vector2(-10f, 10f), R);
            Assert.Less(t, 3f, "Dos rumbos que convergen en el mismo punto son un conflicto.");
            Assert.Greater(t, 0f);
        }

        [Test]
        public void Alejandose_NoHayConflicto()
        {
            float t = NpcConflictMath.TiempoAColision(new Vector2(0f, 20f), new Vector2(0f, 10f), R);
            Assert.AreEqual(float.MaxValue, t, "Si se aleja, no hay nada que resolver.");
        }

        [Test]
        public void MismaVelocidadYRumbo_NoHayConflicto()
        {
            // El clásico "voy detrás de él a la misma velocidad": la distancia
            // no cambia nunca. Sin este caso, dividir por |v|² sería 0/0.
            float t = NpcConflictMath.TiempoAColision(new Vector2(0f, 12f), Vector2.zero, R);
            Assert.AreEqual(float.MaxValue, t);
        }

        [Test]
        public void SeCruzanDeLejos_NoEsConflicto()
        {
            // Trayectorias que se cruzan pero con 30 m de separación en el
            // momento más cercano: es tráfico normal, no un choque.
            float t = NpcConflictMath.TiempoAColision(new Vector2(0f, 60f), new Vector2(30f, -10f), R);
            Assert.AreEqual(float.MaxValue, t, "Pasar de largo a 30 m no es un conflicto.");
        }

        [Test]
        public void YaEncima_ElConflictoEsInmediato()
        {
            float t = NpcConflictMath.TiempoAColision(new Vector2(1f, 1f), new Vector2(5f, 0f), R);
            Assert.AreEqual(0f, t, "Superpuestos ya: el conflicto es ahora, no dentro de t segundos.");
        }

        [Test]
        public void LaDerechaSeMideContraMiPropioCostado()
        {
            // Mirando hacia +Z, mi derecha es +X.
            Assert.IsTrue(NpcConflictMath.PorLaDerecha(Vector3.right, new Vector3(10f, 0f, 5f)));
            Assert.IsFalse(NpcConflictMath.PorLaDerecha(Vector3.right, new Vector3(-10f, 0f, 5f)));
        }

        [Test]
        public void LaAlturaNoEnsuciaElLadoDelQueViene()
        {
            // Un paso a desnivel no debe cambiar de qué lado viene alguien.
            Assert.IsTrue(NpcConflictMath.PorLaDerecha(Vector3.right, new Vector3(10f, 40f, 5f)));
        }
    }
}
