using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HablaCamaron.Missions;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// INTEGRACIÓN (Tarea 10): la meta del nivel 3 nace apagada y la arman
    /// checkpoints invisibles EN ORDEN (playtest: "la meta del nivel se podía
    /// tocar desde el arranque"). Carga la misión real de N1_ZonaSur (id 2,
    /// "La cuesta de Guamaní") y verifica en PLAY que: (a) se sembraron entre
    /// 3 y 6 marcadores, (b) la baliza nace apagada, (c) teletransportar el
    /// auto DIRECTO a la meta sin pasar checkpoints no termina la misión, y
    /// (d) pasarlos todos EN ORDEN arma la meta.
    /// </summary>
    public class Nivel3CheckpointsTests
    {
        [UnityTearDown]
        public IEnumerator Limpia()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Core.GameAudioSettings.GameplayMuted = false;
            PlayerPrefs.DeleteKey(MissionCatalog.KEY_CURRENT);
            var s = SceneManager.GetSceneByName("N1_ZonaSur");
            if (s.IsValid() && s.isLoaded)
            {
                var v = SceneManager.CreateScene("PostNivel3Checkpoints");
                SceneManager.SetActiveScene(v);
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        /// <summary>Simula el clic real de "¡Vamos, camarón!": el juego debe arrancar.</summary>
        private static IEnumerator ArrancarMision(MissionRunner runner)
        {
            var briefing = GameObject.Find("MissionBriefing");
            Assert.IsNotNull(briefing, "La pantalla de briefing debe construirse.");
            var botones = briefing.GetComponentsInChildren<UnityEngine.UI.Button>();
            Assert.GreaterOrEqual(botones.Length, 1, "El briefing debe tener el botón '¡Vamos!'.");
            botones[0].onClick.Invoke();
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, "Al pulsar Vamos el juego debe descongelarse.");
            Assert.IsTrue(runner.Running, "La misión debe quedar corriendo tras el briefing.");
        }

        /// <summary>Teletransporta el auto (por el Rigidbody, para no pelear con
        /// la física) y deja correr unos pasos fijos para que el trigger del
        /// checkpoint (o la comprobación de distancia a la meta) reaccione.</summary>
        private static IEnumerator Teletransportar(Rigidbody rb, Vector3 pos)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.transform.position = pos;
            Physics.SyncTransforms();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Mision2LaCuesta_LaMetaNaceApagadaYLaArmanLosCheckpointsEnOrden()
        {
            PlayerPrefs.SetInt(MissionCatalog.KEY_CURRENT, 2);
            SceneManager.LoadScene("N1_ZonaSur");
            yield return null;
            for (int i = 0; i < 40; i++) yield return null;

            var runner = Object.FindFirstObjectByType<MissionRunner>();
            Assert.IsNotNull(runner, "El MissionRunner de la misión 2 debe existir.");
            Assert.IsTrue(runner.Def.Checkpoints, "La misión 2 (id 2) debe tener Checkpoints activo.");

            var car = Object.FindFirstObjectByType<VehicleController>();
            Assert.IsNotNull(car, "El Aveo del jugador debe existir.");
            var rb = car.GetComponent<Rigidbody>();
            Assert.IsNotNull(rb, "El auto debe traer Rigidbody (VehicleController lo exige).");

            yield return ArrancarMision(runner);

            // (a) entre 3 y 6 marcadores sembrados sobre la ruta A* spawn→meta.
            var marcadores = runner.GetComponentsInChildren<MissionCheckpointMarker>()
                .OrderBy(m => m.Indice).ToList();
            Assert.GreaterOrEqual(marcadores.Count, MissionCheckpoints.MinCheckpoints,
                "Deben sembrarse al menos MinCheckpoints marcadores.");
            Assert.LessOrEqual(marcadores.Count, MissionCheckpoints.MaxCheckpoints,
                "No deben sembrarse más de MaxCheckpoints marcadores.");

            // (b) la baliza nace apagada: nadie puede tocar la meta de arranque.
            Assert.IsFalse(runner.GoalArmed,
                "La meta debe nacer APAGADA hasta pasar los checkpoints (playtest).");

            // (c) teletransportarse DIRECTO a la meta sin pasar ningún
            // checkpoint no debe terminar la misión: el chequeo de llegada
            // está apagado mientras GoalArmed sea false.
            yield return Teletransportar(rb, runner.GoalTransform.position);
            Assert.IsTrue(runner.Running,
                "Tocar la meta SIN checkpoints no debe terminar la misión (la meta sigue apagada).");
            Assert.IsFalse(runner.GoalArmed,
                "La meta sigue apagada: no se pasó ningún checkpoint todavía.");

            // (d) pasar los marcadores EN ORDEN arma la meta.
            foreach (var m in marcadores)
                yield return Teletransportar(rb, m.transform.position);

            Assert.IsTrue(runner.GoalArmed,
                "Tras pasar TODOS los checkpoints en orden, la meta debe quedar armada.");
        }

        /// <summary>
        /// La guía tiene que llevar AL SIGUIENTE CHECKPOINT, no al lado
        /// contrario (playtest: "las indicaciones no están claras, puedes ir
        /// donde puedas"). El bug: con la meta apagada, RoutePathMarkers y
        /// RouteGuide tomaban la rama de las misiones de "volver al inicio",
        /// que guía al nodo MÁS LEJANO DE LA META — o sea, en el nivel 3,
        /// justo al revés de la cuesta. Y el checkpoint que tocaba cruzar era
        /// invisible, así que no había forma de saberlo.
        /// </summary>
        [UnityTest]
        public IEnumerator Mision2LaCuesta_LaGuiaLlevaAlSiguienteCheckpointYSeVe()
        {
            PlayerPrefs.SetInt(MissionCatalog.KEY_CURRENT, 2);
            SceneManager.LoadScene("N1_ZonaSur");
            yield return null;
            for (int i = 0; i < 40; i++) yield return null;

            var runner = Object.FindFirstObjectByType<MissionRunner>();
            Assert.IsNotNull(runner, "El MissionRunner de la misión 2 debe existir.");
            yield return ArrancarMision(runner);

            var cp = runner.NextCheckpoint;
            Assert.IsNotNull(cp, "Con checkpoints activos siempre hay uno pendiente.");

            // (a) el que toca cruzar SE VE (tiene su marca encendida).
            var marca = cp.Find("Marca");
            Assert.IsNotNull(marca, "El checkpoint pendiente debe traer su marca visible.");
            Assert.IsTrue(marca.gameObject.activeSelf,
                "La marca del checkpoint que toca cruzar tiene que estar encendida.");

            // (b) y los demás NO (si no, la ruta es una feria de columnas).
            var todos = runner.GetComponentsInChildren<MissionCheckpointMarker>();
            foreach (var m in todos)
            {
                if (m.transform == cp) continue;
                var otra = m.transform.Find("Marca");
                if (otra != null)
                    Assert.IsFalse(otra.gameObject.activeSelf,
                        $"Solo debe verse el checkpoint pendiente; {m.name} está encendido.");
            }

            // (c) la ruta guiada TERMINA en ese checkpoint, no huyendo de la meta.
            var markers = Object.FindFirstObjectByType<RoutePathMarkers>();
            Assert.IsNotNull(markers, "La guía de flechas debe existir en la misión.");
            for (int i = 0; i < 150; i++) yield return null; // deja que recalcule

            var ruta = markers.Route;
            Assert.Greater(ruta.Count, 1, "La guía debe trazar una ruta por las calles.");
            float alCheckpoint = Vector3.Distance(ruta[ruta.Count - 1], cp.position);
            Assert.Less(alCheckpoint, MissionCheckpoints.RadioMetros * 3f,
                $"La ruta guiada debe terminar en el checkpoint pendiente y acaba a " +
                $"{alCheckpoint:0} m de él: está llevando al jugador a otro lado.");
        }
    }
}
