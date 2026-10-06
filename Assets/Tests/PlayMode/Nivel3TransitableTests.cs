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
    /// ¿SE PUEDE CONDUCIR el nivel 3 hasta la cima? (playtest 2026-07-25: "no
    /// me deja completar el nivel 3"). El piloto automático NO sirve para esto:
    /// mueve el coche por encima de la geometría, así que atravesaba tan feliz
    /// la esfera invisible que tenía la calzada de la plaza — un cilindro
    /// aplastado conserva un CapsuleCollider ESFÉRICO de 16 m de radio.
    ///
    /// Aquí se barre la ruta con la CAJA del coche (BoxCast tramo a tramo) y se
    /// mide el suelo bajo cada metro: cualquier obstáculo sólido o escalón que
    /// el Aveo no pueda subir sale a la luz.
    /// </summary>
    public class Nivel3TransitableTests
    {
        /// <summary>Media caja del Aveo con margen (ancho, alto, largo).</summary>
        private static readonly Vector3 MediaCaja = new Vector3(0.95f, 0.7f, 1.9f);

        /// <summary>Escalón que el coche NO puede subir rodando (m).</summary>
        private const float EscalonMaximo = 0.9f;

        [UnityTearDown]
        public IEnumerator Limpia()
        {
            Time.timeScale = 1f;
            PlayerPrefs.DeleteKey(MissionCatalog.KEY_CURRENT);
            var s = SceneManager.GetSceneByName("N1_ZonaSur");
            if (s.IsValid() && s.isLoaded)
            {
                var v = SceneManager.CreateScene("PostN3");
                SceneManager.SetActiveScene(v);
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        [UnityTest]
        public IEnumerator LaRutaALaCima_EstaLibreYSinEscalones()
        {
            PlayerPrefs.SetInt(MissionCatalog.KEY_CURRENT, 2);
            SceneManager.LoadScene("N1_ZonaSur");
            yield return null;
            for (int i = 0; i < 40; i++) yield return null;

            var runner = Object.FindFirstObjectByType<MissionRunner>();
            var car = Object.FindFirstObjectByType<VehicleController>();
            var graph = RoadGraph.Instance;
            Assert.IsNotNull(runner); Assert.IsNotNull(car); Assert.IsNotNull(graph);

            var meta = runner.GoalTransform.position;
            var a = graph.Data.NearestNode(car.transform.position);
            var b = graph.Data.NearestNode(meta);
            var ruta = AStarPlanner.FindPath(graph.Data, a.Id, b.Id);
            Assert.IsNotNull(ruta, "el nivel 3 tiene que tener ruta a la cima");

            Physics.SyncTransforms();

            // 1) ESCALONES: el suelo bajo la ruta, metro a metro.
            var escalones = new List<string>();
            float anterior = float.NaN;
            for (int i = 1; i < ruta.Count; i++)
            {
                Vector3 p0 = ruta[i - 1].Position, p1 = ruta[i].Position;
                int pasos = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p0, p1)));
                for (int k = 0; k <= pasos; k++)
                {
                    Vector3 p = Vector3.Lerp(p0, p1, k / (float)pasos);
                    float suelo = SueloBajo(p, p.y);
                    if (float.IsNaN(suelo)) { anterior = float.NaN; continue; }
                    if (!float.IsNaN(anterior) && Mathf.Abs(suelo - anterior) > EscalonMaximo)
                        escalones.Add($"escalón de {Mathf.Abs(suelo - anterior):0.0} m en {p}");
                    anterior = suelo;
                }
            }
            Assert.IsEmpty(escalones,
                "La ruta a la cima tiene escalones que el coche no puede subir:\n" +
                string.Join("\n", escalones));

            // 2) OBSTÁCULOS: barrido con la caja del coche por el eje de la vía.
            var choques = new List<string>();
            for (int i = 1; i < ruta.Count; i++)
            {
                Vector3 p0 = ruta[i - 1].Position + Vector3.up * 0.75f;
                Vector3 p1 = ruta[i].Position + Vector3.up * 0.75f;
                Vector3 dir = p1 - p0;
                float largo = dir.magnitude;
                if (largo < 0.05f) continue;
                dir /= largo;

                foreach (var h in Physics.BoxCastAll(p0, MediaCaja, dir,
                             Quaternion.LookRotation(dir, Vector3.up), largo,
                             Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    var t = h.collider.transform;
                    // El propio coche, el suelo y la decoración blanda no cuentan.
                    if (t.root == car.transform.root) continue;
                    string n = h.collider.name;
                    // Lo que ES suelo no estorba: asfalto, relleno y las
                    // "RampaSuave_*" (colliders invisibles que RoadStripKit
                    // tiende sobre las pendientes para que el coche no brinque
                    // entre módulos — son la propia calzada, no un muro).
                    if (n.StartsWith("Road_") || n.StartsWith("Highway_") || n == "Piso" ||
                        n == "Calzada" || n == "Terreno" || n.StartsWith("Meseta") ||
                        n.StartsWith("Cuna") || n.StartsWith("Relleno") ||
                        n.StartsWith("RampaSuave")) continue;
                    if (h.collider.GetComponentInParent<NpcDriver>() != null) continue;
                    choques.Add($"'{t.root.name}/{n}' en {h.point}");
                }
            }

            Debug.Log($"[N3] ruta de {ruta.Count} nodos hasta la cima " +
                      $"| escalones={escalones.Count} | obstáculos={choques.Count}");
            Assert.IsEmpty(choques,
                "Hay obstáculos sólidos sobre la ruta del nivel 3:\n" +
                string.Join("\n", choques));
        }

        /// <summary>
        /// Altura de la CALZADA bajo un punto: la superficie más cercana a la
        /// altura que marca el grafo, no la más alta. Quedarse con "la más
        /// alta" mide el techo del garaje, las casas o la copa de un árbol —
        /// el error de método que ya escondió dos veces el bug de la cima.
        /// </summary>
        private static float SueloBajo(Vector3 p, float alturaEsperada)
        {
            float mejor = float.NaN, mejorDist = float.MaxValue;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 120f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                string n = h.collider.name;
                if (n.StartsWith("Tree") || n.StartsWith("Bush") || n.Contains("Flor")) continue;
                float d = Mathf.Abs(h.point.y - alturaEsperada);
                if (d < mejorDist) { mejorDist = d; mejor = h.point.y; }
            }
            return mejor;
        }
    }
}
