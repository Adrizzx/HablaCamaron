using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.Vehicle;
using HablaCamaron.World;

namespace HablaCamaron.UI
{
    /// <summary>Mundo (XZ) → coordenadas del minimapa. PURA (testeada):
    /// norte arriba, escala uniforme y siempre dentro del marco.</summary>
    public static class MiniMapMath
    {
        public static Vector2 WorldToMap(Vector3 world, Vector3 center, float worldHalf, float mapHalf)
        {
            if (worldHalf < 0.001f) return Vector2.zero;
            float x = (world.x - center.x) / worldHalf * mapHalf;
            float y = (world.z - center.z) / worldHalf * mapHalf;
            return new Vector2(Mathf.Clamp(x, -mapHalf, mapHalf),
                               Mathf.Clamp(y, -mapHalf, mapHalf));
        }
    }

    /// <summary>
    /// Minimapa REAL (cierre post-Fase 4; antes era decorativo con una ruta
    /// falsa): dibuja las calles del RoadGraph de la zona, el punto del
    /// jugador en vivo y la meta de la misión (respetando la baliza de
    /// "volver", que aparece recién al armarse). En escenas sin grafo se
    /// queda como cuadrícula decorativa, sin romper nada.
    /// Lo crea HUDController dentro de su panel.
    /// </summary>
    public class MiniMapView : MonoBehaviour
    {
        private const float MapHalf = 100f; // área útil: 200×200 dentro del panel

        private RectTransform _content;
        private RectTransform _playerDot, _goalDot;
        private Transform _playerT;
        private Missions.MissionRunner _runner;
        private Vector3 _center;
        private float _worldHalf = -1f; // <0 = grafo aún no dibujado
        private float _retryIn;

        /// <summary>Tope de segmentos de la ruta dibujados (la ciudad da rutas
        /// de cientos de nodos; a esta escala con 120 sobra).</summary>
        private const int MaxRouteSegments = 120;

        private readonly System.Collections.Generic.List<RectTransform> _routeSegs =
            new System.Collections.Generic.List<RectTransform>();
        private Missions.RoutePathMarkers _markers;
        private float _routeIn;

        /// <summary>El HUD entrega el rect donde dibujar (ya con fondo y marco).</summary>
        public void Init(RectTransform content) => _content = content;

        private void LateUpdate()
        {
            if (_content == null) return;

            // El grafo y el runner nacen en la carga de escena: reintentar barato.
            if (_worldHalf < 0f)
            {
                _retryIn -= Time.unscaledDeltaTime;
                if (_retryIn > 0f) return;
                _retryIn = 0.5f;
                TryBuild();
                return;
            }

            if (_playerT == null)
            {
                var car = FindFirstObjectByType<VehicleController>();
                if (car != null) _playerT = car.transform;
            }
            if (_playerT != null)
                _playerDot.anchoredPosition =
                    MiniMapMath.WorldToMap(_playerT.position, _center, _worldHalf, MapHalf);

            if (_runner == null) _runner = FindFirstObjectByType<Missions.MissionRunner>();
            bool goalVisible = _runner != null && _runner.GoalTransform != null && _runner.GoalArmed;
            _goalDot.gameObject.SetActive(goalVisible);
            if (goalVisible)
                _goalDot.anchoredPosition =
                    MiniMapMath.WorldToMap(_runner.GoalTransform.position, _center, _worldHalf, MapHalf);

            DrawRoute();
        }

        /// <summary>
        /// LA RUTA sobre el minimapa (playtest 2026-07-25: "en el mapa las
        /// indicaciones por dónde debo ir, eso no está"). Se pinta la misma
        /// ruta que ya calcula RoutePathMarkers para el suelo, así que el
        /// mapa y las flechas dicen exactamente lo mismo y el A* se corre una
        /// sola vez. Refresco lento: es una polilínea, no hace falta cada frame.
        /// </summary>
        private void DrawRoute()
        {
            _routeIn -= Time.unscaledDeltaTime;
            if (_routeIn > 0f) return;

            if (_markers == null)
                _markers = FindFirstObjectByType<Missions.RoutePathMarkers>();
            var ruta = _markers != null ? _markers.Route : null;

            // Mientras no haya ruta que pintar se reintenta enseguida; una vez
            // pintada, medio segundo sobra. Con un refresco fijo de 0.5 s el
            // mapa podía tardar en estrenarse justo al empezar la misión, que
            // es cuando el jugador más mira para saber por dónde tirar.
            _routeIn = (ruta != null && ruta.Count >= 2) ? 0.5f : 0.1f;

            int usados = 0, saltados = 0;
            if (ruta != null && ruta.Count >= 2)
            {
                for (int i = 1; i < ruta.Count && usados < MaxRouteSegments; i++)
                {
                    var a = MiniMapMath.WorldToMap(ruta[i - 1], _center, _worldHalf, MapHalf);
                    var b = MiniMapMath.WorldToMap(ruta[i], _center, _worldHalf, MapHalf);
                    if ((a - b).sqrMagnitude < 1f) { saltados++; continue; } // invisible a esta escala

                    var seg = SegmentoDeRuta(usados++);
                    seg.sizeDelta = new Vector2(Vector2.Distance(a, b), 4.5f);
                    seg.anchoredPosition = (a + b) * 0.5f;
                    seg.localRotation = Quaternion.Euler(0, 0,
                        Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                    if (!seg.gameObject.activeSelf) seg.gameObject.SetActive(true);
                }
            }
            for (int i = usados; i < _routeSegs.Count; i++)
                if (_routeSegs[i].gameObject.activeSelf) _routeSegs[i].gameObject.SetActive(false);
            _ = saltados; // (queda a mano para diagnosticar la escala del mapa)
        }

        /// <summary>Segmento de ruta del pool (se crea la primera vez).</summary>
        private RectTransform SegmentoDeRuta(int index)
        {
            while (_routeSegs.Count <= index)
            {
                var seg = UIFactory.Panel("Ruta", _content, UITheme.AccentTop,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, 999f);
                seg.GetComponent<Image>().raycastTarget = false;
                seg.SetAsLastSibling(); // la ruta va SOBRE las calles
                _routeSegs.Add(seg);
            }
            // Siempre por encima de las calles y por debajo de los puntos.
            _routeSegs[index].SetSiblingIndex(_content.childCount - 3);
            return _routeSegs[index];
        }

        // ---------------- Construcción ----------------

        private void TryBuild()
        {
            var graph = RoadGraph.Instance;
            if (graph == null || graph.Data.Nodes.Count == 0) return; // sin calles: decorativo

            // Encuadre: centro y semiancho del grafo (con un respiro del 10%).
            var b = new Bounds(graph.Data.Nodes[0].Position, Vector3.zero);
            foreach (var n in graph.Data.Nodes) b.Encapsulate(n.Position);
            _center = b.center;
            _worldHalf = Mathf.Max(b.extents.x, b.extents.z) * 1.1f + 1f;

            // Calles: una línea por arista (los dos sentidos se superponen, bien).
            foreach (var e in graph.Data.Edges)
            {
                var from = graph.Data.GetNode(e.FromId);
                var to = graph.Data.GetNode(e.ToId);
                if (from == null || to == null) continue;
                DrawStreet(MiniMapMath.WorldToMap(from.Position, _center, _worldHalf, MapHalf),
                           MiniMapMath.WorldToMap(to.Position, _center, _worldHalf, MapHalf));
            }

            // Meta (dorada, oculta hasta que haya misión con meta armada).
            _goalDot = Dot("Meta", UITheme.GoldLight, 14);
            UIFactory.AddGlowEdge(_goalDot.gameObject, UITheme.A(UITheme.Gold, 0.8f), 2f);
            _goalDot.gameObject.SetActive(false);

            // Jugador (terracota con halo, encima de todo).
            var halo = UIFactory.GlowCircle("PHalo", _content, UITheme.A(UITheme.AccentEdge, 0.6f), 30);
            UIFactory.SetRect(halo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(30, 30), new Vector2(0.5f, 0.5f));
            _playerDot = Dot("Jugador", UITheme.AccentTop, 12);
            halo.transform.SetParent(_playerDot, true);
            ((RectTransform)halo.transform).anchoredPosition = Vector2.zero;
        }

        private RectTransform Dot(string name, Color color, float size)
        {
            var img = UIFactory.Circle(name, _content, color, size);
            img.raycastTarget = false;
            UIFactory.SetRect(img.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size, size), new Vector2(0.5f, 0.5f));
            return img.rectTransform;
        }

        private void DrawStreet(Vector2 a, Vector2 b)
        {
            var seg = UIFactory.Panel("Calle", _content, UITheme.A(UITheme.Gold, 0.45f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 999f);
            seg.GetComponent<Image>().raycastTarget = false;
            seg.sizeDelta = new Vector2(Vector2.Distance(a, b), 2.5f);
            seg.anchoredPosition = (a + b) * 0.5f;
            seg.localRotation = Quaternion.Euler(0, 0,
                Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }
    }
}
