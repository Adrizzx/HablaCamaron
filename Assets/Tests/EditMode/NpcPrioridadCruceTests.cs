using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Prioridad en el punto de conflicto y desempate determinista (Paso 3 del
    /// plan de IA). Es la regla que convierte el caos en negociación: cede
    /// quien llega DESPUÉS al cruce, y si llegan a la vez manda el id menor.
    /// </summary>
    public class NpcPrioridadCruceTests
    {
        // ---------------- Tiempos al cruce ----------------

        [Test]
        public void DosTrayectoriasQueSeCruzan_DanElTiempoDeCadaUno()
        {
            // A viene del oeste a 10 m/s desde 40 m; B del sur a 10 m/s desde
            // 20 m. Se cruzan en el origen: B llega en 2 s, A en 4 s.
            bool hay = NpcConflictMath.TiemposAlCruce(
                new Vector2(-40f, 0f), new Vector2(10f, 0f),
                new Vector2(0f, -20f), new Vector2(0f, 10f),
                out float tA, out float tB);

            Assert.IsTrue(hay);
            Assert.AreEqual(4f, tA, 0.05f);
            Assert.AreEqual(2f, tB, 0.05f);
        }

        [Test]
        public void TrayectoriasParalelas_NoSeCruzan()
        {
            bool hay = NpcConflictMath.TiemposAlCruce(
                new Vector2(0f, 0f), new Vector2(10f, 0f),
                new Vector2(0f, 8f), new Vector2(10f, 0f),
                out _, out _);
            Assert.IsFalse(hay, "Dos carriles paralelos no tienen punto de cruce que negociar.");
        }

        [Test]
        public void ElCruceQueYaQuedoAtras_NoCuenta()
        {
            // A ya pasó el origen y se aleja; B viene hacia él.
            bool hay = NpcConflictMath.TiemposAlCruce(
                new Vector2(30f, 0f), new Vector2(10f, 0f),
                new Vector2(0f, -20f), new Vector2(0f, 10f),
                out _, out _);
            Assert.IsFalse(hay, "Si el cruce quedó atrás no hay nada que ceder.");
        }

        [Test]
        public void UnAutoParado_NoTienePuntoDeCruce()
        {
            // Velocidad nula: el producto cruzado se anula y no hay recta.
            bool hay = NpcConflictMath.TiemposAlCruce(
                new Vector2(-40f, 0f), Vector2.zero,
                new Vector2(0f, -20f), new Vector2(0f, 10f),
                out _, out _);
            Assert.IsFalse(hay);
        }

        // ---------------- Quién cede ----------------

        [Test]
        public void CedeElQueLlegaDespues()
        {
            Assert.IsTrue(NpcConflictMath.DeboCeder(4f, 2f, miId: 1, suId: 2),
                "Llego 2 s después: me toca ceder.");
            Assert.IsFalse(NpcConflictMath.DeboCeder(2f, 4f, miId: 1, suId: 2),
                "Llego antes: paso yo.");
        }

        [Test]
        public void ElQueYaCirculaEnElRedondel_TienePrioridadSobreElQueEntra()
        {
            // El del anillo está a 6 m del punto de fusión a 8 m/s (0.75 s);
            // el que entra, a 14 m a 8 m/s (1.75 s). Cede el que entra.
            // Este caso es EL MOTIVO de no usar la regla de la derecha: en un
            // redondel antihorario el que circula llega por la IZQUIERDA del
            // que entra, así que "cede ante quien viene por tu derecha" haría
            // ceder justo al que tiene la prioridad.
            Assert.IsTrue(NpcConflictMath.DeboCeder(1.75f, 0.75f, miId: 7, suId: 3));
            Assert.IsFalse(NpcConflictMath.DeboCeder(0.75f, 1.75f, miId: 3, suId: 7));
        }

        [Test]
        public void SiLleganALaVez_DesempataElIdMenor()
        {
            Assert.IsTrue(NpcConflictMath.DeboCeder(2f, 2f, miId: 9, suId: 4),
                "Empate y mi id es mayor: cedo.");
            Assert.IsFalse(NpcConflictMath.DeboCeder(2f, 2f, miId: 4, suId: 9),
                "Empate y mi id es menor: paso.");
        }

        [Test]
        public void ElDesempateEsANTISIMETRICO_NuncaCedenLosDosNiNinguno()
        {
            // La propiedad que de verdad importa: pase lo que pase, de dos
            // autos en conflicto cede EXACTAMENTE UNO. Si cedieran los dos se
            // quedarían clavados para siempre; si no cediera ninguno, se
            // atraviesan. Se barre una rejilla de tiempos e ids.
            for (int i = 0; i < 40; i++)
            {
                float ta = 0.1f * i;
                for (int j = 0; j < 40; j++)
                {
                    float tb = 0.1f * j;
                    bool aCede = NpcConflictMath.DeboCeder(ta, tb, 100, 200);
                    bool bCede = NpcConflictMath.DeboCeder(tb, ta, 200, 100);
                    Assert.AreNotEqual(aCede, bCede,
                        $"Con tiempos {ta:0.0}/{tb:0.0} ceden los dos o ninguno: eso es el bloqueo eterno " +
                        "(o el traspaso) que esta regla existe para evitar.");
                }
            }
        }

        // ---------------- La regla en el cerebro: REVERTIDA ----------------

        [Test]
        public void LaPrioridadNO_MandaHoyEnLaFsm()
        {
            // PROBADO Y REVERTIDO (2026-07-30). La regla "cede el que llega
            // después al cruce" se conectó a NpcBrain y hubo que quitarla: en
            // la Zona Sur la contravía pasó de 5/3/5/9 a 14/10/10/7 y los
            // bloqueados de 5/6/1/3 a 15/10/8/4, sin mejorar los superpuestos
            // (medido con TraficoBoletinTests, 4 corridas por versión). Un
            // arreglo dirigido —eximir el ceda del anti-atasco— tampoco bastó.
            // Causa: el que cede se detiene DENTRO del cruce y la escalera de
            // desatasco acaba sacándolo de su carril.
            // Este test fija el estado ACTUAL para que nadie reconecte la regla
            // sin medir de nuevo; la matemática de arriba se conserva probada
            // para cuando exista una línea de parada real sobre el grafo.
            var p = DriverProfile.Particular();
            var s = new NpcSensors
            {
                AheadDistance = float.MaxValue,
                NeighborTimeToCollision = 1.5f,
                NeighborHasPriority = true,
            };
            var d = NpcBrain.Decide(s, p, true);
            Assert.AreEqual(NpcState.Cruising, d.State,
                "Si esto pasa a Yielding es que se reconectó la regla revertida: medir el boletín ANTES de darla por buena.");
            Object.DestroyImmediate(p);
        }
    }
}
