using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HablaCamaron.Missions;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// DIAGNÓSTICO/REGRESIÓN (playtest nivel 4 "El redondel", misión id 3,
    /// N1_CiudadToon). Arranca la misión real EN PLAY y verifica los DOS
    /// síntomas reportados que dependían del código y la geometría (no del
    /// tercero, "obstáculos en el camino", que lo cubre RouteClearanceScan
    /// como invariante del builder):
    ///  · "se congela al inicio" — el arranque completo (Start del runner,
    ///    construir el briefing, pulsar "¡Vamos!", correr con tráfico real)
    ///    no debe lanzar ninguna excepción ni dejar timeScale en 0 sin salida.
    ///    Unity hace fallar el test solo si algo loguea un error/excepción
    ///    inesperado durante el Play (no hace falta pedirlo a mano).
    ///  · "la meta aparece en el punto de partida" — Meta_Redondel debe
    ///    quedar lejos del spawn.
    /// </summary>
    public class Nivel4RedondelDiagTests
    {
        private const float DistanciaMinimaMeta = 50f; // la meta nunca "pegada" al spawn

        [UnityTearDown]
        public IEnumerator Limpiar()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Core.GameAudioSettings.GameplayMuted = false;
            var ciudad = SceneManager.GetSceneByName("N1_CiudadToon");
            if (ciudad.IsValid() && ciudad.isLoaded)
            {
                var limpia = SceneManager.CreateScene("PostNivel4Diag_Limpia");
                SceneManager.SetActiveScene(limpia);
                yield return SceneManager.UnloadSceneAsync(ciudad);
            }
        }

        [UnityTest]
        public IEnumerator Mision3ElRedondel_ArrancaSinCongelarse_YLaMetaQuedaLejos()
        {
            PlayerPrefs.SetInt(MissionCatalog.KEY_CURRENT, 3);
            SceneManager.LoadScene("N1_CiudadToon");
            yield return null;
            for (int i = 0; i < 30; i++) yield return null;

            var runner = Object.FindFirstObjectByType<MissionRunner>();
            Assert.IsNotNull(runner, "El MissionRunner de la misión 3 debe existir.");
            Assert.AreEqual(0f, Time.timeScale, "El briefing debe congelar el juego (a propósito).");

            var jugador = Object.FindFirstObjectByType<VehicleController>();
            Assert.IsNotNull(jugador, "El Aveo del jugador debe existir.");
            Assert.IsNotNull(runner.GoalTransform, "La meta (Meta_Redondel) debe resolverse.");
            float distMeta = Vector3.Distance(jugador.transform.position, runner.GoalTransform.position);
            Assert.Greater(distMeta, DistanciaMinimaMeta,
                $"Meta_Redondel quedó a {distMeta:0} m del spawn — playtest: " +
                "'la meta aparece en el punto de partida'.");

            // Simular el clic real de "¡Vamos, camarón!": el juego DEBE arrancar.
            var briefing = GameObject.Find("MissionBriefing");
            Assert.IsNotNull(briefing, "La pantalla de briefing debe construirse.");
            var botones = briefing.GetComponentsInChildren<UnityEngine.UI.Button>();
            Assert.GreaterOrEqual(botones.Length, 1, "El briefing debe tener el botón '¡Vamos!'.");
            botones[0].onClick.Invoke();

            yield return null;
            Assert.AreEqual(1f, Time.timeScale, "Al pulsar Vamos el juego debe descongelarse.");
            Assert.IsTrue(runner.Running, "La misión debe quedar corriendo tras el briefing.");

            // El tráfico real (8 NPC) debe fluir varios segundos sin que nada
            // se congele (playtest: "se congela al inicio").
            for (int i = 0; i < 240; i++) yield return null; // ~4 s de juego real
            Assert.IsTrue(runner.Running, "La misión debe seguir corriendo (nada la congeló).");
        }
    }
}
