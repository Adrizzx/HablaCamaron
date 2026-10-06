using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Captura PNGs de la Zona Sur EN PLAY REAL (Logs/snaps/runtime_*.png):
    /// los focos de semáforo y el arreglo de materiales de texto solo existen
    /// en runtime, así que las capturas de editor no los ven. Este test es la
    /// verificación visual de esa capa (y siempre pasa: su valor es el PNG).
    /// </summary>
    public class RuntimeSnapshotTests
    {
        /// <summary>
        /// Devolver el mundo LIMPIO al siguiente test: la Zona Sur cargada
        /// auto-crea su MissionRunner, cuyo briefing deja timeScale = 0 — y
        /// con el tiempo congelado, los tests de vehículo que avanzan con
        /// WaitForFixedUpdate esperan PARA SIEMPRE (colgaba toda la suite
        /// en batch). Se restaura el tiempo y se descarga la ciudad.
        /// </summary>
        [UnityTearDown]
        public IEnumerator DevolverElMundoLimpio()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Core.GameAudioSettings.GameplayMuted = false;

            var ciudad = SceneManager.GetSceneByName("N1_ZonaSur");
            if (ciudad.IsValid() && ciudad.isLoaded)
            {
                var limpia = SceneManager.CreateScene("PostSnapshot_Limpia");
                SceneManager.SetActiveScene(limpia);
                yield return SceneManager.UnloadSceneAsync(ciudad);
            }
        }

        [UnityTest]
        public IEnumerator CapturaRuntime_SemaforoYTextos_ZonaSur()
        {
            SceneManager.LoadScene("N1_ZonaSur");
            yield return null;                       // que cargue
            for (int i = 0; i < 150; i++) yield return null; // ~2.5 s de runtime

            Directory.CreateDirectory("Logs/snaps");
            var go = new GameObject("SnapCamRuntime");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;

            // 1) Un semáforo del redondel, de cerca (¿se ve el foco?).
            var lights = TrafficLightController.Instance;
            if (lights != null && lights.Lamps.Count > 0)
            {
                var lamp = lights.Lamps[0];
                var target = lamp.RedLight != null ? lamp.RedLight.transform
                           : lamp.GreenLight != null ? lamp.GreenLight.transform : null;
                if (target != null)
                {
                    Vector3 eye = target.position + new Vector3(6f, 0.5f, 6f);
                    cam.transform.SetPositionAndRotation(eye,
                        Quaternion.LookRotation(target.position - eye));
                    Shot(cam, "runtime_semaforo.png");
                }
            }

            // 2) La vista del conductor (¿los textos ya no atraviesan paredes?).
            var aveo = GameObject.Find("Aveo_Camaron");
            if (aveo != null)
            {
                var driver = aveo.GetComponent<Vehicle.DriverCamera>();
                Vector3 eye = driver != null
                    ? aveo.transform.TransformPoint(driver.EyeLocalPos)
                    : aveo.transform.position + Vector3.up * 1.5f;
                cam.transform.SetPositionAndRotation(eye, aveo.transform.rotation);
                Shot(cam, "runtime_conductor.png");
            }

            Object.Destroy(go);
            Assert.Pass("Capturas runtime en Logs/snaps/");
        }

        private static void Shot(Camera cam, string file)
        {
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine("Logs/snaps", file), tex.EncodeToPNG());
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
