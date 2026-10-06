using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Las técnicas defensivas del cerebro NPC: frenado por tiempo-a-colisión
    /// (TTC) y burbuja alrededor del jugador. Son las que evitan que el
    /// tránsito embista a quien está aprendiendo.
    /// </summary>
    public class NpcBrainDefensiveTests
    {
        private static DriverProfile Perfil()
        {
            var p = ScriptableObject.CreateInstance<DriverProfile>();
            p.CruiseSpeed = 8f;
            p.MinGap = 6f;
            p.LightBrakeDistance = 18f;
            return p;
        }

        [Test]
        public void CerrandoRapido_FrenaAunqueLaDistanciaParezcaComoda()
        {
            // 20 m se ve lejos (MinGap*2.5 = 15), pero cerrando a 16 m/s el
            // TTC es 1.25 s < 1.4 → emergencia. Esto es frenar por física.
            var s = new NpcSensors { AheadDistance = 20f, AheadClosingSpeed = 16f };
            var d = NpcBrain.Decide(s, Perfil(), true);
            Assert.AreEqual(NpcState.Braking, d.State);
            Assert.AreEqual(0f, d.TargetSpeed);
        }

        [Test]
        public void MismaDistancia_SinCierre_SigueTranquilo()
        {
            // El de adelante va a mi ritmo (cierre 0): 20 m es vía libre.
            var s = new NpcSensors { AheadDistance = 20f, AheadClosingSpeed = 0f };
            var d = NpcBrain.Decide(s, Perfil(), true);
            Assert.AreEqual(NpcState.Cruising, d.State);
        }

        [Test]
        public void CierreModerado_LoSigueSinCerrarLaBrecha()
        {
            // TTC 2.5 s (20 m / 8 m/s): zona de confort → Following.
            var s = new NpcSensors { AheadDistance = 20f, AheadClosingSpeed = 8f };
            var d = NpcBrain.Decide(s, Perfil(), true);
            Assert.AreEqual(NpcState.Following, d.State);
        }

        [Test]
        public void JugadorEnLaBurbuja_SeDetieneDelTodoAunqueLaViaEsteLibre()
        {
            // La regla cambió tras el playtest: nada de gatear hacia quien
            // aprende — el NPC se DETIENE (y pita) hasta que se quite.
            var s = new NpcSensors { AheadDistance = float.MaxValue, PlayerNear = true };
            var d = NpcBrain.Decide(s, Perfil(), true);
            Assert.AreEqual(NpcState.Braking, d.State);
            Assert.AreEqual(0f, d.TargetSpeed);
        }

        [Test]
        public void LaEmergencia_Manda_SobreLaBurbujaDelJugador()
        {
            // Algo pegadísimo adelante + jugador cerca: frena en seco (0), no gatea.
            var s = new NpcSensors { AheadDistance = 2f, PlayerNear = true };
            var d = NpcBrain.Decide(s, Perfil(), true);
            Assert.AreEqual(NpcState.Braking, d.State);
            Assert.AreEqual(0f, d.TargetSpeed);
        }

        // ---- El empate con el jugador (playtest: "se quedan bloqueados") ----

        [Test]
        public void SiElJugadorNoSeAparta_ElNpcLoRodeaAlPaso()
        {
            // Un jugador calado en medio de la calle tenía a los NPCs clavados
            // para siempre (y el reciclado por atasco no actúa con el jugador
            // cerca): la calle quedaba tapada y el nivel, injugable.
            var s = new NpcSensors
            {
                AheadDistance = float.MaxValue,
                PlayerNear = true,
                PlayerStalemate = true,
            };
            var d = NpcBrain.Decide(s, Perfil(), true);
            Assert.AreNotEqual(0f, d.TargetSpeed, "tras el empate tiene que volver a moverse");
            Assert.Less(d.TargetSpeed, Perfil().CruiseSpeed * 0.5f,
                "pero AL PASO: sigue siendo alguien aprendiendo a manejar");
            // Y NO en Overtaking: ese estado activa el desvío lateral de ~3.2 m
            // de NpcDriver.Move(), que dentro del redondel estrecho de la Zona
            // Sur deja dos autos superpuestos y en el carril contrario (medido
            // por NpcRedondelDiagTests / NpcCarrilDiagTests al intentarlo).
            // Aquí se reanuda al paso y DE FRENTE; del clavado de verdad se
            // encarga el anti-bloqueo.
            Assert.AreNotEqual(NpcState.Overtaking, d.State,
                "no debe desviarse: el desvío lateral superpone autos en el redondel");
        }

        [Test]
        public void ElEmpate_NoAtropella_LaEmergenciaSigueMandando()
        {
            // Aunque se haya agotado la paciencia, si lo tiene encima FRENA:
            // la regla de choque inminente va antes que ninguna otra.
            var s = new NpcSensors
            {
                AheadDistance = 2f,
                PlayerNear = true,
                PlayerStalemate = true,
            };
            var d = NpcBrain.Decide(s, Perfil(), true);
            Assert.AreEqual(NpcState.Braking, d.State);
            Assert.AreEqual(0f, d.TargetSpeed);
        }
    }
}
