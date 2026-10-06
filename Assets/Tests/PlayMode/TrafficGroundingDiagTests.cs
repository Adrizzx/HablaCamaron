using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// DIAGNÓSTICO (playtest: "los autos vuelan"): carga una zona con tráfico,
    /// deja que los NPCs circulen unos segundos y REPORTA cuántos quedan
    /// flotando sobre el piso (altura del pivote menos la superficie bajo él).
    /// Renderiza también un cenital para verlo. Siempre pasa: su valor es la
    /// evidencia en el log (Logs/tests-pm.log) y el PNG.
    /// </summary>
    public class TrafficGroundingDiagTests
    {
        [UnityTearDown]
        public IEnumerator Limpia()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Core.GameAudioSettings.GameplayMuted = false;
            var s = SceneManager.GetSceneByName("N1_CiudadToon");
            if (s.IsValid() && s.isLoaded)
            {
                var vacia = SceneManager.CreateScene("PostDiag");
                SceneManager.SetActiveScene(vacia);
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        [UnityTest]
        public IEnumerator CuantosNpcsFlotan_EnLaCiudad()
        {
            SceneManager.LoadScene("N1_CiudadToon");
            yield return null;
            for (int i = 0; i < 400; i++) yield return null; // ~6.5 s de circulación

            var npcs = Object.FindObjectsByType<NpcDriver>(FindObjectsSortMode.None);
            int flotando = 0;
            float peor = 0f;
            foreach (var npc in npcs)
            {
                Vector3 p = npc.transform.position;
                // Superficie bajo el auto, ignorándolo a él.
                float suelo = float.NegativeInfinity;
                foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 60f))
                    if (h.collider.transform.root != npc.transform.root && h.point.y > suelo)
                        suelo = h.point.y;
                if (float.IsNegativeInfinity(suelo)) continue;
                float alto = p.y - suelo;
                if (alto > 2.5f) { flotando++; peor = Mathf.Max(peor, alto); }
            }

            Debug.Log($"[DiagVuelo] NPCs vivos: {npcs.Length}. Flotando (>2.5 m): " +
                      $"{flotando}. Peor altura: {peor:0.0} m.");

            Directory.CreateDirectory("Logs/snaps");
            var camGO = new GameObject("DiagCam");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 90f;
            var centro = npcs.Length > 0 ? npcs[0].transform.position : Vector3.zero;
            cam.transform.position = centro + Vector3.up * 160f;
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.farClipPlane = 400f;
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes("Logs/snaps/diag_vuelo_ciudad.png", tex.EncodeToPNG());
            Object.Destroy(rt); Object.Destroy(tex); Object.Destroy(camGO);

            Assert.Pass($"Diagnóstico: {flotando} NPCs flotando de {npcs.Length}.");
        }

        /// <summary>
        /// REGRESIÓN (playtest 2026-07-25: "hay NPC carros que se suben a otros
        /// carros"). El apoyo al piso tomaba cualquier superficie a menos de
        /// MaxStepUp (1.6 m) por encima, y un auto de Toon City mide ~1.5 m: el
        /// TECHO del de adelante entraba como suelo pisable. Ahora NpcDriver
        /// descarta del raycast todo lo que sea un vehículo.
        /// </summary>
        [UnityTest]
        public IEnumerator NingunNpcSeSubeEncimaDeOtro()
        {
            SceneManager.LoadScene("N1_CiudadToon");
            yield return null;
            for (int i = 0; i < 500; i++) yield return null; // ~8 s de circulación

            var npcs = Object.FindObjectsByType<NpcDriver>(FindObjectsSortMode.None);
            var encimados = new System.Collections.Generic.List<string>();

            foreach (var a in npcs)
            {
                foreach (var b in npcs)
                {
                    if (a == b) continue;
                    Vector3 pa = a.transform.position, pb = b.transform.position;
                    // Casi en la misma vertical y uno claramente ARRIBA del otro
                    // = está montado encima (los autos miden ~1.5 m).
                    Vector2 plano = new Vector2(pa.x - pb.x, pa.z - pb.z);
                    if (plano.magnitude > 2.5f) continue;
                    float alturaSobre = pa.y - pb.y;
                    if (alturaSobre > 1.0f)
                        encimados.Add($"{a.name} está {alturaSobre:0.0} m sobre {b.name} " +
                                      $"(separación en planta {plano.magnitude:0.0} m)");
                }
            }

            Debug.Log($"[DiagEncimados] NPCs vivos: {npcs.Length}. " +
                      $"Montados sobre otro: {encimados.Count}.");
            Assert.IsEmpty(encimados,
                "Hay NPCs subidos encima de otros autos:\n" + string.Join("\n", encimados));
        }
    }
}
