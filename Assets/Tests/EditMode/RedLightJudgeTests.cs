using NUnit.Framework;
using UnityEngine;
using HablaCamaron.Missions;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// El juez del semáforo en rojo: castigar solo al CRUZAR el nodo, nunca
    /// por llegar frenando cerca de la línea (el falso positivo que hacía
    /// injusto el −4 de señales para principiantes).
    /// </summary>
    public class RedLightJudgeTests
    {
        // El nodo del semáforo en el origen; el auto avanza hacia +Z.

        [Test]
        public void LlegarFrenandoConElNodoAdelante_NoEsCruce()
        {
            // A 4 m de la línea, todavía de frente: puede detenerse a tiempo.
            Assert.IsFalse(RedLightJudge.CrossedNode(
                new Vector3(0f, 0f, -4f), Vector3.forward, Vector3.zero));
        }

        [Test]
        public void DetenidoJustoEnLaLinea_NoEsCruce()
        {
            // Parado con el nodo exactamente al lado de adelante (along = 0).
            Assert.IsFalse(RedLightJudge.CrossedNode(
                new Vector3(0f, 0f, 0f), Vector3.forward, new Vector3(0f, 0f, 0.0f)));
        }

        [Test]
        public void DejarElNodoAtrasPorSuCarril_EsCruce()
        {
            // Ya pasó 2 m más allá de la línea por el mismo carril: se pasó el rojo.
            Assert.IsTrue(RedLightJudge.CrossedNode(
                new Vector3(0f, 0f, 2f), Vector3.forward, Vector3.zero));
        }

        [Test]
        public void PasarDeCostadoPorElAnillo_NoEsCruce()
        {
            // Circulando el redondel a 4.5 m laterales de la entrada de OTRO
            // brazo: el nodo queda atrás pero nunca pasó por encima de él.
            Assert.IsFalse(RedLightJudge.CrossedNode(
                new Vector3(4.5f, 0f, 2f), Vector3.forward, Vector3.zero));
        }

        [Test]
        public void CruceLevementeDesalineado_SigueSiendoCruce()
        {
            // Cruzó por su carril aunque no exactamente sobre el nodo (1.5 m).
            Assert.IsTrue(RedLightJudge.CrossedNode(
                new Vector3(1.5f, 0f, 3f), Vector3.forward, Vector3.zero));
        }

        [Test]
        public void LaAlturaNoInfluye()
        {
            // El centro del auto va más alto que el nodo del piso: se aplana Y.
            Assert.IsTrue(RedLightJudge.CrossedNode(
                new Vector3(0f, 1.4f, 2f), Vector3.forward, Vector3.zero));
        }

        [Test]
        public void SinRumboDefinido_NoJuzga()
        {
            Assert.IsFalse(RedLightJudge.CrossedNode(
                Vector3.zero, Vector3.zero, new Vector3(0f, 0f, -1f)));
        }
    }
}
