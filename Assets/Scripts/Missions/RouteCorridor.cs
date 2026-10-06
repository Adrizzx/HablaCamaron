using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.AI;
using HablaCamaron.UI;
using HablaCamaron.World;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// Mantiene al jugador EN EL CAMINO de la misión (playtest 2026-07-25: "en
    /// el nivel 4 debe dejar ir solo por donde puede, no ser libre"). La ruta
    /// se traza UNA vez con A* al empezar (spawn → meta) y a partir de ahí el
    /// juez puro RouteCorridorJudge decide: dentro, desviándose o perdido.
    ///  · Desviándose → aviso rojo en el HUD con cuenta atrás y reclamo de Don
    ///    Pancho, y una infracción de defensiva (una sola por escapada).
    ///  · Perdido → la misión se da por terminada (repetir nivel).
    /// Lo agrega MissionRunner solo en las misiones con RouteLocked.
    /// </summary>
    public class RouteCorridor : MonoBehaviour
    {
        /// <summary>Se dispara cuando el jugador abandona la ruta del todo.</summary>
        public event System.Action OnLost;

        private readonly RouteCorridorJudge _judge = new RouteCorridorJudge();
        private List<Vector3> _route;
        private Transform _player;
        private PlayerInfractions _infractions;
        private bool _yaCastigado;

        /// <summary>La ruta fijada (la dibuja el minimapa si quiere).</summary>
        public IReadOnlyList<Vector3> Route => _route;

        /// <summary>Traza el camino de la misión y arranca la vigilancia.</summary>
        public void Init(Transform player, Vector3 goal)
        {
            _player = player;
            _infractions = player.GetComponent<PlayerInfractions>();
            _route = TraceRoute(player.position, goal);
            BuildWarnUI();
        }

        /// <summary>Ruta por las calles del spawn a la meta; si no hay grafo o
        /// no hay camino, queda la recta (mejor eso que no vigilar nada).</summary>
        private static List<Vector3> TraceRoute(Vector3 from, Vector3 to)
        {
            var puntos = new List<Vector3>();
            var graph = RoadGraph.Instance != null ? RoadGraph.Instance.Data : null;
            if (graph != null)
            {
                var a = graph.NearestNode(from);
                var b = graph.NearestNode(to);
                if (a != null && b != null)
                {
                    var path = AStarPlanner.FindPath(graph, a.Id, b.Id);
                    if (path != null)
                        foreach (var n in path) puntos.Add(n.Position);
                }
            }
            if (puntos.Count == 0) { puntos.Add(from); puntos.Add(to); }
            return puntos;
        }

        private void Update()
        {
            if (_player == null || _route == null) return;
            if (Time.timeScale == 0f) { Show(null); return; }

            float dist = RouteCorridorJudge.DistanceToRoute(_route, _player.position);
            switch (_judge.Tick(dist, Time.deltaTime))
            {
                case CorridorVerdict.OnRoute:
                    _yaCastigado = false;
                    Show(null);
                    break;

                case CorridorVerdict.Straying:
                    int quedan = Mathf.CeilToInt(
                        RouteCorridorJudge.LostSeconds - _judge.OutFor);
                    Show($"FUERA DE RUTA — VUELVE AL CAMINO ({Mathf.Max(quedan, 0)})");
                    if (!_yaCastigado)
                    {
                        _yaCastigado = true;
                        _infractions?.AddOffRoad();
                        DonPanchoDialogue.Instance?.Trigger(DialogueEvent.OutOfBounds);
                    }
                    break;

                case CorridorVerdict.Lost:
                    Show(null);
                    enabled = false;
                    OnLost?.Invoke();
                    break;
            }
        }

        // ---------------- Aviso en pantalla ----------------
        // Al canal único (UI.AlertBanner): si a la vez hay una contravía, esa
        // manda — es más grave que ir por una calle que no toca.

        private void BuildWarnUI() => AlertBanner.Ensure();

        private void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            AlertBanner.Instance?.Show(message, AlertPriority.Info);
        }
    }
}
