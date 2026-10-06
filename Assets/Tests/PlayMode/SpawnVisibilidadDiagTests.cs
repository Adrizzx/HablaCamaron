using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HablaCamaron.AI;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// DIAGNÓSTICO del Paso 1: ¿de verdad NO queda ningún sitio fuera de la
    /// vista donde nacer? Tras exigir "fuera del frustum", la Zona Sur se
    /// quedó con CERO tráfico ambiental (0 spawns en 4+4 corridas), y relajar
    /// el radio mínimo a 25 m no lo movió. Antes de inventar otra regla, se
    /// mide: nodo por nodo, distancia al jugador, si cae en el frustum y si
    /// tiene LÍNEA DE VISTA (un auto que nace tras un edificio no "aparece de
    /// la nada" aunque geométricamente esté dentro del cono).
    /// No afirma nada: imprime la tabla. Es un instrumento, no un guardarraíl.
    /// </summary>
    public class SpawnVisibilidadDiagTests
    {
        private string _escena;

        [UnityTearDown]
        public IEnumerator Limpia()
        {
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(_escena))
            {
                var s = SceneManager.GetSceneByName(_escena);
                _escena = null;
                if (s.IsValid() && s.isLoaded)
                {
                    var vacia = SceneManager.CreateScene("PostDiagSpawn");
                    SceneManager.SetActiveScene(vacia);
                    yield return SceneManager.UnloadSceneAsync(s);
                }
            }
        }

        [UnityTest]
        public IEnumerator DondeSePuedeNacerSinQueElJugadorLoVea()
        {
            foreach (string escena in new[] { "N1_ZonaSur", "N1_CiudadToon" })
            {
                _escena = escena;
                SceneManager.LoadScene(escena);
                yield return null;
                yield return null;
                yield return null;
                Time.timeScale = 1f;
                for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

                var graph = RoadGraph.Instance;
                var cam = Camera.main;
                var mgr = TrafficManager.Instance;
                var player = mgr != null ? mgr.Player : null;
                Assert.IsNotNull(graph, $"{escena} debe traer [RoadGraph].");

                if (cam == null || player == null)
                {
                    Debug.LogWarning($"[DiagSpawn] {escena}: cámara={(cam != null)} jugador={(player != null)} " +
                                     "— sin uno de los dos no se puede medir.");
                    continue;
                }

                var planos = GeometryUtility.CalculateFrustumPlanes(cam);
                int total = 0, enRango = 0, fueraDeVista = 0, tapados = 0, libresYVisibles = 0;

                foreach (var n in graph.Data.Nodes)
                {
                    total++;
                    float d = Vector3.Distance(n.Position, player.position);
                    if (!NpcSpawnRules.DistanciaValida(d, NpcSpawnRules.RadioMinFueraDeVista, mgr.SpawnRadiusMax))
                        continue;
                    enRango++;

                    bool frustum = NpcSpawnRules.EnFrustum(planos, n.Position);
                    if (!frustum) { fueraDeVista++; continue; }

                    // ¿Hay algo sólido entre el ojo del jugador y ese punto?
                    Vector3 ojo = cam.transform.position;
                    Vector3 hacia = (n.Position + Vector3.up * 0.9f) - ojo;
                    bool tapado = Physics.Raycast(ojo, hacia.normalized, out var hit, hacia.magnitude - 1.5f,
                                      Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                                  hit.collider.transform.root != player.root;
                    if (tapado) tapados++; else libresYVisibles++;
                }

                Debug.Log($"[DiagSpawn] {escena}: nodos={total} | en rango ({NpcSpawnRules.RadioMinFueraDeVista:0}-" +
                          $"{mgr.SpawnRadiusMax:0} m)={enRango} | FUERA del frustum={fueraDeVista} | " +
                          $"en frustum pero TAPADOS={tapados} | en frustum y A LA VISTA={libresYVisibles}. " +
                          $"Cámara '{cam.name}' fov={cam.fieldOfView:0} far={cam.farClipPlane:0} en {cam.transform.position}, " +
                          $"jugador en {player.position}.");

                var s2 = SceneManager.GetSceneByName(escena);
                if (s2.IsValid() && s2.isLoaded)
                {
                    var vacia = SceneManager.CreateScene("PostDiagSpawn_" + escena);
                    SceneManager.SetActiveScene(vacia);
                    yield return SceneManager.UnloadSceneAsync(s2);
                }
                _escena = null;
            }
        }
    }
}
