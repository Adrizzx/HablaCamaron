using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HablaCamaron.AI;
using HablaCamaron.Missions;
using HablaCamaron.Vehicle;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// INFORME DE NIVELES: arranca cada misión de verdad y mide lo que el
    /// jugador tiene delante en el segundo cero — dónde queda la meta, si hay
    /// guía visible (flechas en el suelo y ruta en el minimapa), cuántas
    /// vallas cierran los desvíos y cuánto tráfico circula. Deja un retrato
    /// desde el volante en `Logs/snaps/niveles/`.
    ///
    /// No juzga (el que dice si un nivel se puede terminar es
    /// PilotoAutomaticoTests): esto es la foto fija de la SALIDA de cada nivel,
    /// que es donde el jugador decide si entiende qué hacer o se pierde.
    /// </summary>
    public class InformeDeNivelesTests
    {
        private const string Dir = "Logs/snaps/niveles";

        [UnityTearDown]
        public IEnumerator Limpia()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Core.GameAudioSettings.GameplayMuted = false;
            PlayerPrefs.DeleteKey(MissionCatalog.KEY_CURRENT);
            yield return null;
        }

        [UnityTest] public IEnumerator Nivel1() { yield return Informe(0); }
        [UnityTest] public IEnumerator Nivel2() { yield return Informe(1); }
        [UnityTest] public IEnumerator Nivel3() { yield return Informe(2); }
        [UnityTest] public IEnumerator Nivel4() { yield return Informe(3); }
        [UnityTest] public IEnumerator Nivel5() { yield return Informe(4); }
        [UnityTest] public IEnumerator Nivel6() { yield return Informe(5); }
        [UnityTest] public IEnumerator Nivel7() { yield return Informe(6); }
        [UnityTest] public IEnumerator Bonus() { yield return Informe(7); }

        private IEnumerator Informe(int missionId)
        {
            Directory.CreateDirectory(Dir);
            var def = MissionCatalog.Get(missionId);
            PlayerPrefs.SetInt(MissionCatalog.KEY_CURRENT, missionId);
            SceneManager.LoadScene(def.SceneName);
            yield return null;
            for (int i = 0; i < 40; i++) yield return null;

            var runner = Object.FindFirstObjectByType<MissionRunner>();
            var car = Object.FindFirstObjectByType<VehicleController>();
            Assert.IsNotNull(runner, $"[{def.Title}] sin runner");
            Assert.IsNotNull(car, $"[{def.Title}] sin auto");

            var brief = GameObject.Find("MissionBriefing");
            if (brief != null)
                brief.GetComponentsInChildren<UnityEngine.UI.Button>()[0].onClick.Invoke();
            yield return null;
            for (int i = 0; i < 90; i++) yield return null; // que nazcan tráfico y guía

            Vector3 pos = car.transform.position;
            Vector3 meta = runner.GoalTransform != null ? runner.GoalTransform.position : pos;
            Vector3 hacia = meta - pos; hacia.y = 0f;

            int flechas = 0, vallas = 0, rutaMapa = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.gameObject.activeInHierarchy) continue;
                if (t.name.StartsWith("Flecha_")) flechas++;
                else if (t.name == "Valla") vallas++;
            }
            int callesMapa = 0;
            foreach (var r in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            {
                if (r.name == "Ruta" && r.gameObject.activeInHierarchy) rutaMapa++;
                else if (r.name == "Calle" && r.gameObject.activeInHierarchy) callesMapa++;
            }
            var mm = Object.FindFirstObjectByType<UI.MiniMapView>();
            var marks = Object.FindFirstObjectByType<RoutePathMarkers>();
            Debug.Log($"[MAPA {missionId + 1}] minimapa={(mm != null)} calles={callesMapa} " +
                      $"ruta={rutaMapa} | markers={(marks != null)} " +
                      $"nodosRuta={(marks != null && marks.Route != null ? marks.Route.Count : -1)}");

            int npcs = Object.FindObjectsByType<NpcDriver>(FindObjectsSortMode.None).Length;
            int semaforos = Object.FindObjectsByType<World.TrafficLightController>(
                FindObjectsSortMode.None).Length;
            int cebras = Object.FindObjectsByType<World.Crosswalk>(FindObjectsSortMode.None).Length;

            Debug.Log($"[NIVEL {missionId + 1}] '{def.Title}' ({def.SceneName}) " +
                      $"| meta a {hacia.magnitude:0} m ({Vector3.Angle(car.transform.forward, hacia):0}° " +
                      $"del morro, armada={runner.GoalArmed}) " +
                      $"| GUÍA: {flechas} flechas, {rutaMapa} tramos en el mapa " +
                      $"| vallas={vallas} | NPCs={npcs}/{def.TrafficDensity} " +
                      $"| semáforos={semaforos} cebras={cebras} " +
                      $"| tiempo={def.TimeLimit}s laps={def.Laps} " +
                      $"strict={def.StrictRules} ruta={def.RouteLocked}");

            yield return Retrato(pos + Vector3.up * 1.6f, car.transform.rotation,
                                 $"nivel{missionId + 1}_volante.png");

            var abierta = SceneManager.GetSceneByName(def.SceneName);
            if (abierta.IsValid() && abierta.isLoaded)
            {
                var limpia = SceneManager.CreateScene("PostInforme_" + missionId);
                SceneManager.SetActiveScene(limpia);
                yield return SceneManager.UnloadSceneAsync(abierta);
            }
            Assert.Pass();
        }

        private IEnumerator Retrato(Vector3 at, Quaternion rot, string file)
        {
            var camGO = new GameObject("InformeCam");
            var cam = camGO.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(at, rot);
            cam.fieldOfView = 60f;
            cam.farClipPlane = 900f;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Dir, file), tex.EncodeToPNG());
            Object.Destroy(rt); Object.Destroy(tex); Object.Destroy(camGO);
            yield return null;
        }
    }
}
