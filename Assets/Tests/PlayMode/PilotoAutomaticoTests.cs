using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HablaCamaron.AI;
using HablaCamaron.Missions;
using HablaCamaron.Vehicle;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// EL PILOTO AUTOMÁTICO (playtest 2026-07-25: "cada nivel ejecuta tú mismo
    /// hasta llegar al final"). Cada misión del catálogo se JUEGA de verdad:
    /// se carga su escena, se pasa el briefing y un conductor virtual lleva el
    /// auto por la ruta A* de la misión hasta tocar la meta.
    ///
    /// Qué demuestra que ningún test anterior demostraba: que el nivel se puede
    /// TERMINAR. Los tests de grafo prueban que existe una ruta; este prueba
    /// que un auto la recorre — con su tráfico, sus semáforos, sus vallas y sus
    /// jueces encendidos — y que al llegar el runner da la misión por cumplida.
    ///
    /// El piloto no imita a un humano (no usa embrague ni marchas): mueve el
    /// Rigidbody por la ruta a velocidad de ciudad. Lo que se valida es el
    /// NIVEL —geometría, meta alcanzable, nada que bloquee el paso, jueces que
    /// no reprueban a quien conduce por su carril— no la física del coche, que
    /// ya cubren VehicleControllerTests y CuestaEmpinadaTests.
    /// </summary>
    public class PilotoAutomaticoTests
    {
        /// <summary>Velocidad de crucero del piloto (m/s ≈ 40 km/h).</summary>
        private const float PilotSpeed = 11f;

        /// <summary>Tope de tiempo simulado por misión (s). Si el piloto no
        /// llega en este rato, el nivel tiene un problema.</summary>
        private const float MaxSimSeconds = 240f;

        /// <summary>Paso de simulación: se avanza el juego a saltos grandes
        /// para que un nivel de 1 km no tarde una eternidad en el runner.</summary>
        private const float SimStep = 0.05f;

        [UnityTearDown]
        public IEnumerator Limpiar()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Core.GameAudioSettings.GameplayMuted = false;
            PlayerPrefs.DeleteKey(MissionCatalog.KEY_CURRENT);
            yield return null;
        }

        // Una entrada por misión: el runner las ejecuta como casos separados,
        // así se ve EXACTAMENTE qué nivel falla.
        [UnityTest]
        public IEnumerator Nivel1_SacarElAveo() { yield return Jugar(0); }

        [UnityTest]
        public IEnumerator Nivel2_LaVueltaALaManzana() { yield return Jugar(1); }

        [UnityTest]
        public IEnumerator Nivel3_LaCuesta() { yield return Jugar(2); }

        [UnityTest]
        public IEnumerator Nivel4_ElRedondel() { yield return Jugar(3); }

        [UnityTest]
        public IEnumerator Nivel5_LaSimonDeNoche() { yield return Jugar(4); }

        [UnityTest]
        public IEnumerator Nivel6_HoraPico() { yield return Jugar(5); }

        [UnityTest]
        public IEnumerator Nivel7_ElExamen() { yield return Jugar(6); }

        [UnityTest]
        public IEnumerator Bonus_QuitoEntero() { yield return Jugar(7); }

        // ---------------------------------------------------------------

        /// <summary>Carga la misión, pasa el briefing y conduce hasta la meta.</summary>
        private IEnumerator Jugar(int missionId)
        {
            var def = MissionCatalog.Get(missionId);
            Assert.IsNotNull(def, $"no existe la misión {missionId}");

            PlayerPrefs.SetInt(MissionCatalog.KEY_CURRENT, missionId);
            SceneManager.LoadScene(def.SceneName);
            yield return null;
            for (int i = 0; i < 30; i++) yield return null; // que arranque todo

            var runner = Object.FindFirstObjectByType<MissionRunner>();
            Assert.IsNotNull(runner, $"[{def.Title}] sin MissionRunner");
            var car = Object.FindFirstObjectByType<VehicleController>();
            Assert.IsNotNull(car, $"[{def.Title}] sin auto del jugador");
            Assert.IsNotNull(runner.GoalTransform, $"[{def.Title}] la meta no se resolvió");

            // Pasar el briefing como lo haría el jugador.
            var briefing = GameObject.Find("MissionBriefing");
            Assert.IsNotNull(briefing, $"[{def.Title}] sin pantalla de briefing");
            briefing.GetComponentsInChildren<UnityEngine.UI.Button>()[0].onClick.Invoke();
            yield return null;
            Assert.IsTrue(runner.Running, $"[{def.Title}] no arrancó tras el briefing");

            // El piloto toma el mando: se apaga la física del coche para
            // moverlo por la ruta (lo que se prueba es el NIVEL, no el motor).
            var rb = car.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            car.enabled = false;

            float simulado = 0f;
            int vueltas = 0;
            var ruta = TrazarRuta(car.transform.position, runner.GoalTransform.position);
            int wp = 0;

            Debug.Log($"[PilotoDiag] '{def.Title}' escena={def.SceneName} " +
                      $"auto={car.transform.position} meta={runner.GoalTransform.position} " +
                      $"dist={Vector3.Distance(car.transform.position, runner.GoalTransform.position):0} m " +
                      $"nodosRuta={(ruta != null ? ruta.Count : -1)} " +
                      $"escenasCargadas={SceneManager.sceneCount}");

            // Las misiones de "volver" (y cada vuelta de las de 2 laps) NO dan
            // la meta por buena hasta que el jugador se ALEJA del inicio: la
            // baliza se arma a los 60 m. El piloto tiene que jugar esa mecánica
            // igual que un humano — primero alejarse, después volver.
            bool armadaAntes = runner.GoalArmed;
            Vector3 puntoLejano = PuntoParaAlejarse(runner.GoalTransform.position);

            while (simulado < MaxSimSeconds && runner.Running)
            {
                Vector3 meta = runner.GoalTransform.position;

                // ¿Cambió el estado de la baliza (se armó, o empezó otra
                // vuelta)? Entonces hay otro objetivo: retrazar.
                if (runner.GoalArmed != armadaAntes)
                {
                    armadaAntes = runner.GoalArmed;
                    ruta = TrazarRuta(car.transform.position,
                                      runner.GoalArmed ? meta : puntoLejano);
                    wp = 0;
                    vueltas++;
                }

                // Mientras la baliza no esté armada, el objetivo es ALEJARSE.
                Vector3 objetivoFinal = runner.GoalArmed ? meta : puntoLejano;

                // Misiones de VUELTAS (el redondel): una vez llegado, hay que
                // RODEAR la meta. El piloto da vueltas alrededor de ella.
                if (def.Laps > 1 &&
                    Vector3.Distance(car.transform.position, meta) < LapCounter.RingRadius * 0.6f)
                {
                    Vector3 radial = car.transform.position - meta;
                    radial.y = 0f;
                    if (radial.sqrMagnitude < 1f) radial = Vector3.forward * 18f;
                    // Punto siguiente sobre la circunferencia (giro antihorario).
                    Vector3 tangente = Vector3.Cross(Vector3.up, radial.normalized);
                    objetivoFinal = meta + (radial.normalized * 18f + tangente * 8f);
                    ruta = null;
                }

                // Objetivo inmediato: el waypoint de la ruta, o el destino final.
                Vector3 destino = (ruta != null && wp < ruta.Count) ? ruta[wp] : objetivoFinal;
                Vector3 hacia = destino - car.transform.position;
                hacia.y = 0f;

                if (hacia.magnitude < 4f)
                {
                    if (ruta != null && wp < ruta.Count) wp++;
                    else
                    {
                        // Llegó a la meta pero el runner aún no lo ve: seguir
                        // acercándose (el radio de meta es de 8 m).
                    }
                }

                if (hacia.sqrMagnitude > 1e-4f)
                {
                    Vector3 dir = hacia.normalized;
                    Vector3 pos = car.transform.position + dir * (PilotSpeed * SimStep);

                    // La ALTURA sale de la propia ruta: los nodos del grafo van
                    // sobre la calzada, así que seguirlos sube la cuesta sola.
                    // Con un raycast al suelo no basta — en la cima de la Zona
                    // Sur hay dos superficies bajo el mismo punto (la rampa y el
                    // terreno de abajo) y el piloto acababa 8 m POR DEBAJO de la
                    // meta; y si se toma la más alta, se sube a la copa del
                    // árbol de la isla, 11 m POR ENCIMA (ambas cosas medidas).
                    pos.y = Mathf.MoveTowards(car.transform.position.y, destino.y + 0.6f,
                                              PilotSpeed * SimStep);
                    car.transform.SetPositionAndRotation(pos,
                        Quaternion.LookRotation(dir, Vector3.up));
                    rb.position = pos;
                }

                // Si la ruta se acabó y el objetivo sigue lejos, retrazar.
                if (ruta != null && wp >= ruta.Count &&
                    Vector3.Distance(car.transform.position, objetivoFinal) > 10f)
                {
                    ruta = TrazarRuta(car.transform.position, objetivoFinal);
                    wp = 0;
                }

                simulado += SimStep;
                yield return null;
            }

            Vector3 fin = car.transform.position, metaFin = runner.GoalTransform.position;
            Vector3 delta = metaFin - fin;
            Assert.IsFalse(runner.Running,
                $"[{def.Title}] el piloto NO llegó a la meta en {MaxSimSeconds:0} s simulados " +
                $"(quedó a {delta.magnitude:0} m: {new Vector2(delta.x, delta.z).magnitude:0} m en " +
                $"planta y {delta.y:0.0} m de altura; auto={fin} meta={metaFin}). " +
                "El nivel no se puede terminar: revisar meta, ruta o bloqueos.");

            Assert.IsTrue(string.IsNullOrEmpty(runner.FailReason),
                $"[{def.Title}] la misión terminó REPROBADA conduciendo por su propia ruta: " +
                $"'{runner.FailReason}'. Los jueces están castigando al que va bien.");

            Debug.Log($"[Piloto] '{def.Title}' completado en {simulado:0} s simulados.");

            // Descargar la escena para no arrastrarla al siguiente caso.
            var abierta = SceneManager.GetSceneByName(def.SceneName);
            if (abierta.IsValid() && abierta.isLoaded)
            {
                var limpia = SceneManager.CreateScene("PostPiloto_" + def.Id);
                SceneManager.SetActiveScene(limpia);
                yield return SceneManager.UnloadSceneAsync(abierta);
            }
        }

        /// <summary>Un nodo bien lejos de la meta: adonde va el piloto mientras
        /// la baliza de "volver" no se arma (hay que alejarse 60 m del inicio).</summary>
        private static Vector3 PuntoParaAlejarse(Vector3 meta)
        {
            var graph = RoadGraph.Instance != null ? RoadGraph.Instance.Data : null;
            var lejos = graph != null ? MissionGoals.Farthest(graph, meta) : null;
            return lejos != null ? lejos.Position : meta;
        }

        /// <summary>Altura a la que apoyar el coche en ese punto, ignorando el
        /// propio vehículo. null si ahí no hay suelo.</summary>
        private static float? AlturaDeVia(Vector3 at, Transform propio, float alturaActual)
        {
            var hits = Physics.RaycastAll(at + Vector3.up * 8f, Vector3.down, 40f,
                                          Physics.DefaultRaycastLayers,
                                          QueryTriggerInteraction.Ignore);
            var superficies = new List<float>(hits.Length);
            foreach (var h in hits)
            {
                if (h.collider.transform.root == propio) continue;
                superficies.Add(h.point.y);
            }

            // La más alta PISABLE, no la más alta a secas: en la cima de la
            // Zona Sur hay un árbol en la isla y quedarse con el techo del
            // rayo subía el coche a la COPA — 11.3 m por encima de la meta,
            // medido (el gotcha ya estaba escrito en CLAUDE.md). Se reutiliza
            // la misma regla que usan los NPCs, que para esto está.
            return NpcGroundMath.TryPick(superficies, alturaActual, out float y)
                ? y + 0.6f : (float?)null;
        }

        /// <summary>La ruta por las calles, como la del jugador.</summary>
        private static List<Vector3> TrazarRuta(Vector3 desde, Vector3 hasta)
        {
            var puntos = new List<Vector3>();
            var graph = RoadGraph.Instance != null ? RoadGraph.Instance.Data : null;
            if (graph == null) { puntos.Add(hasta); return puntos; }

            var a = graph.NearestNode(desde);
            var b = graph.NearestNode(hasta);
            if (a == null || b == null) { puntos.Add(hasta); return puntos; }

            var path = AStarPlanner.FindPath(graph, a.Id, b.Id);
            if (path == null) { puntos.Add(hasta); return puntos; }

            foreach (var n in path) puntos.Add(n.Position);
            puntos.Add(hasta); // el último tramo hasta la baliza
            return puntos;
        }
    }
}
