using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HablaCamaron.AI;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// DIAGNÓSTICO (playtest nivel 2: "los NPCs vuelan o se superponen" en el
    /// redondel de N1_ZonaSur). Carga la escena real, CONCENTRA autos NPC en
    /// los dos puntos de giro de la misión — el redondel central y la plaza
    /// de retorno de la cima — y mide, tras ~10 s de circulación:
    ///  A) flotantes: panza a más de 1.0 m de la superficie bajo sus ruedas.
    ///  B) superpuestos: bounds XZ de dos NPCs solapados más del 30% del
    ///     ancho del auto (sin contar autos en pisos distintos).
    /// Imprime CADA infractor con posición y nodo del grafo de origen.
    /// A diferencia de TrafficGroundingDiagTests (que solo reporta), este
    /// EXIGE 0 en ambas mediciones: es la reproducción del bug reportado.
    /// </summary>
    public class NpcRedondelDiagTests
    {
        private const float CarWidth = 1.9f;
        private const float CarLength = 4.4f;
        private const float AlturaMax = 1.0f;
        private const float SegundosSim = 10f;

        [UnityTearDown]
        public IEnumerator Limpia()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Core.GameAudioSettings.GameplayMuted = false;
            var s = SceneManager.GetSceneByName("N1_ZonaSur");
            if (s.IsValid() && s.isLoaded)
            {
                var vacia = SceneManager.CreateScene("PostDiagRedondel");
                SceneManager.SetActiveScene(vacia);
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        [UnityTest]
        public IEnumerator NingunNpcVuelaNiSeSuperponeEnLosRedondeles()
        {
            SceneManager.LoadScene("N1_ZonaSur");
            yield return null;
            yield return null; // Awake/Start del [RoadGraph] y demás controladores

            // N1_ZonaSur trae misión: MissionRunner.Start() congela el juego
            // (Time.timeScale = 0f) para el briefing de Don Pancho y solo lo
            // descongela cuando alguien pulsa "¡Vamos!" — nadie en batch. Con
            // el tiempo congelado, WaitForFixedUpdate no vuelve a llamar
            // NUNCA (mismo colgado documentado en RuntimeSnapshotTests): se
            // fuerza aquí para que los NPCs sí circulen los 10 s simulados.
            Time.timeScale = 1f;

            var graph = RoadGraph.Instance;
            Assert.IsNotNull(graph, "La escena N1_ZonaSur debe traer un [RoadGraph].");
            var nodes = graph.Data.Nodes;
            Assert.Greater(nodes.Count, 0, "El grafo de N1_ZonaSur está vacío.");

            // Redondel central: ZonaSurBuilder recentra el redondel al origen
            // del mundo (rb.center → 0). Plaza de la cima: el brazo norte
            // sube, así que sus nodos son los de mayor Y del mapa.
            float maxY = float.NegativeInfinity;
            foreach (var n in nodes) if (n.Position.y > maxY) maxY = n.Position.y;

            var central = new List<RoadNode>();
            var cima = new List<RoadNode>();
            foreach (var n in nodes)
            {
                float distOrigenXZ = new Vector2(n.Position.x, n.Position.z).magnitude;
                if (distOrigenXZ < 45f) central.Add(n);
                if (n.Position.y > maxY - 5f) cima.Add(n);
            }
            Debug.Log($"[DiagRedondel] Nodos totales: {nodes.Count}. Redondel central (<45 m " +
                      $"del origen): {central.Count}. Plaza de la cima (Y > {maxY - 5f:0.0}): {cima.Count}.");
            Assert.Greater(central.Count, 0, "No se detectó el redondel central (revisar umbral de distancia).");
            Assert.Greater(cima.Count, 0, "No se detectó la plaza de retorno de la cima (revisar umbral de altura).");

            var profile = DriverProfile.Particular();
            var spawned = new List<(GameObject go, RoadNode nodoOrigen)>();

            void Spawn(List<RoadNode> pool, int cuantos, string tag)
            {
                for (int i = 0; i < cuantos; i++)
                {
                    var nodo = pool[i % pool.Count];
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"DiagNPC_{tag}_{i}";
                    go.transform.position = nodo.Position + Vector3.up * 0.75f;
                    go.transform.localScale = new Vector3(CarWidth, 1.4f, CarLength);
                    var rb = go.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    var npc = go.AddComponent<NpcDriver>();
                    npc.Init(graph.Data, profile);
                    spawned.Add((go, nodo));
                }
            }

            // Concentrar tráfico exactamente donde el playtest reporta el
            // problema: varios autos por punto de giro (cola dando la vuelta).
            Spawn(central, 6, "Central");
            Spawn(cima, 8, "Cima"); // el anillo completo de 8 de la plaza

            int pasos = Mathf.RoundToInt(SegundosSim / Time.fixedDeltaTime);
            for (int i = 0; i < pasos; i++) yield return new WaitForFixedUpdate();

            var vivos = new List<(GameObject go, RoadNode nodoOrigen)>();
            foreach (var s in spawned) if (s.go != null) vivos.Add(s);

            // A) Flotantes: superficie bajo el auto (ignorándolo a él), como TrafficGroundingDiagTests.
            int flotando = 0;
            float peorAltura = 0f;
            foreach (var (go, nodoOrigen) in vivos)
            {
                Vector3 p = go.transform.position;
                float suelo = float.NegativeInfinity;
                foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 60f))
                    if (h.collider.transform.root != go.transform.root && h.point.y > suelo)
                        suelo = h.point.y;
                if (float.IsNegativeInfinity(suelo)) continue; // sin piso detectado bajo él

                float alto = p.y - suelo;
                if (alto > AlturaMax)
                {
                    flotando++;
                    peorAltura = Mathf.Max(peorAltura, alto);
                    Debug.LogWarning($"[DiagRedondel] FLOTA: {go.name} en {p} (nodo origen #{nodoOrigen.Id} " +
                                      $"{nodoOrigen.Position}) — altura sobre el piso: {alto:0.00} m.");
                }
            }

            // B) Superpuestos: AABB en XZ, umbral = 30% del ancho del auto; ignora pisos distintos.
            int superpuestos = 0;
            float umbral = CarWidth * 0.30f;
            for (int i = 0; i < vivos.Count; i++)
            for (int j = i + 1; j < vivos.Count; j++)
            {
                Vector3 a = vivos[i].go.transform.position;
                Vector3 b = vivos[j].go.transform.position;
                if (Mathf.Abs(a.y - b.y) > 2f) continue; // pisos distintos: no es un choque real

                float solapeX = CarWidth - Mathf.Abs(a.x - b.x);
                float solapeZ = CarLength - Mathf.Abs(a.z - b.z);
                if (solapeX > umbral && solapeZ > umbral)
                {
                    superpuestos++;
                    Debug.LogWarning($"[DiagRedondel] SUPERPUESTOS: {vivos[i].go.name} (nodo #{vivos[i].nodoOrigen.Id} " +
                                      $"{vivos[i].nodoOrigen.Position}) y {vivos[j].go.name} (nodo #{vivos[j].nodoOrigen.Id} " +
                                      $"{vivos[j].nodoOrigen.Position}) — solape X={solapeX:0.00} m, Z={solapeZ:0.00} m.");
                }
            }

            Debug.Log($"[DiagRedondel] NPCs vivos: {vivos.Count}/{spawned.Count}. Flotando (>1.0 m): " +
                      $"{flotando} (peor {peorAltura:0.00} m). Superpuestos: {superpuestos} par(es).");

            Assert.AreEqual(0, flotando, $"{flotando} NPC(s) flotando (>1.0 m) en el redondel — ver log.");
            Assert.AreEqual(0, superpuestos, $"{superpuestos} par(es) de NPCs superpuestos en el redondel — ver log.");
        }
    }
}
