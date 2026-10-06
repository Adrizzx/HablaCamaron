using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.AI;
using HablaCamaron.UI;
using HablaCamaron.World;

namespace HablaCamaron.Missions
{
    /// <summary>Qué le toca hacer al jugador AHORA para seguir la ruta.</summary>
    public enum GuideStep { Adelante, GiraIzquierda, GiraDerecha, MediaVuelta }

    /// <summary>
    /// La matemática de la guía, PURA (testeada en EditMode): a partir de hacia
    /// dónde mira el auto y hacia dónde queda el siguiente punto de la ruta,
    /// decide la instrucción; y elige el waypoint de la ruta A* que está lo
    /// bastante adelante como para ser útil (no el nodo bajo las ruedas).
    /// </summary>
    public static class RouteGuideMath
    {
        /// <summary>Umbral: hasta aquí es "sigue adelante" (grados).</summary>
        public const float StraightDeg = 30f;
        /// <summary>Desde aquí ya no es giro sino media vuelta (grados).</summary>
        public const float UTurnDeg = 130f;

        /// <summary>Hasta dónde se busca el próximo giro de la ruta (metros).
        /// Antes eran 14 y por eso el GPS avisaba encima del cruce.</summary>
        public const float LookAheadMeters = 60f;
        /// <summary>Desde aquí el giro ya se anuncia con metros.</summary>
        public const float AnnounceMeters = 50f;

        public static GuideStep StepFor(Vector3 carForward, Vector3 toTarget)
        {
            carForward.y = 0f;
            toTarget.y = 0f;
            if (carForward.sqrMagnitude < 1e-4f || toTarget.sqrMagnitude < 1e-4f)
                return GuideStep.Adelante;

            float angle = Vector3.SignedAngle(carForward, toTarget, Vector3.up);
            if (Mathf.Abs(angle) <= StraightDeg) return GuideStep.Adelante;
            if (Mathf.Abs(angle) >= UTurnDeg) return GuideStep.MediaVuelta;
            return angle < 0f ? GuideStep.GiraIzquierda : GuideStep.GiraDerecha;
        }

        /// <summary>A menos de esto, el giro ya no se anticipa: se ordena.</summary>
        public const float OrderNowMeters = 12f;

        /// <summary>
        /// La frase GUIADA (playtest: "más claro y llevado de la mano"): los
        /// giros se anticipan con metros redondeados de 5 en 5 ("EN 40 m GIRA
        /// A LA DERECHA") y encima del giro queda solo la orden.
        /// </summary>
        public static string PhraseFor(GuideStep step, float metersToTurn)
        {
            if (step == GuideStep.Adelante) return "SIGUE ADELANTE";
            if (step == GuideStep.MediaVuelta) return "DA LA VUELTA";

            string orden = step == GuideStep.GiraIzquierda
                ? "GIRA A LA IZQUIERDA" : "GIRA A LA DERECHA";
            if (metersToTurn < OrderNowMeters) return orden + " AHORA";
            int metros = Mathf.RoundToInt(metersToTurn / 5f) * 5;
            return $"EN {metros} m {orden}";
        }

        /// <summary>La flecha grande que acompaña cada instrucción.</summary>
        public static string ArrowFor(GuideStep step)
        {
            switch (step)
            {
                case GuideStep.GiraIzquierda: return "←";
                case GuideStep.GiraDerecha: return "→";
                case GuideStep.MediaVuelta: return "⟲";
                default: return "↑";
            }
        }

        /// <summary>Color por instrucción: crema = sigue, dorado = giro,
        /// rojo = media vuelta (te estás alejando).</summary>
        public static Color ColorFor(GuideStep step) =>
            step == GuideStep.Adelante ? UITheme.TextCream :
            step == GuideStep.MediaVuelta ? UITheme.Danger : UITheme.GoldLight;

        /// <summary>Primer nodo de la ruta que queda al menos minAhead metros
        /// por delante (el nodo bajo las ruedas no orienta a nadie); si la ruta
        /// es más corta, el último. Null si no hay ruta.</summary>
        /// <summary>Cuánto puede separarse la ruta de la recta al waypoint antes
        /// de considerar que ahí ya hay una curva (m). Del ancho de un carril:
        /// más que esto y la recta se sale del asfalto.</summary>
        public const float CorredorMetros = 6f;

        /// <summary>
        /// El waypoint que se le enseña al jugador. Se busca el nodo más lejano
        /// (hasta `minAhead`) al que se puede apuntar EN RECTA sin salirse de la
        /// ruta: se avanza mientras los nodos intermedios sigan pegados a la
        /// línea jugador→candidato, y se corta en cuanto la ruta se dobla.
        ///
        /// Antes se devolvía el primer nodo a ≥ minAhead y, si NINGUNO llegaba
        /// (toda la ruta cabe en el look-ahead: una plaza, un redondel chico),
        /// el ÚLTIMO de la ruta. Eso apuntaba al otro lado del anillo y la
        /// flecha mandaba POR ENCIMA DE LA ISLA: medido en la cima del nivel 3,
        /// decía "SIGUE ADELANTE" desde 40 m antes de la plaza con la recta
        /// cruzando el árbol del centro (playtest: "indica dirección
        /// incorrecta"). En una recta el comportamiento no cambia: los nodos
        /// intermedios están sobre la línea, así que se sigue llegando al de
        /// 60 m y el aviso se anticipa igual.
        /// </summary>
        public static Vector3? PickWaypoint(List<RoadNode> path, Vector3 playerPos, float minAhead)
        {
            if (path == null || path.Count == 0) return null;
            float minSq = minAhead * minAhead;

            Vector3 mejor = path[0].Position;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 cand = path[i].Position;
                if (!RectaSigueLaRuta(path, i, playerPos, cand)) break;
                mejor = cand;
                if ((cand - playerPos).sqrMagnitude >= minSq) return cand;
            }
            return mejor;
        }

        /// <summary>¿Los nodos anteriores al candidato caen sobre la recta
        /// jugador→candidato? Si alguno se aparta, entre medio hay una curva.</summary>
        private static bool RectaSigueLaRuta(List<RoadNode> path, int hasta,
            Vector3 desde, Vector3 candidato)
        {
            Vector3 dir = candidato - desde; dir.y = 0f;
            float largo = dir.magnitude;
            if (largo < 1e-3f) return true;
            dir /= largo;

            for (int j = 0; j < hasta; j++)
            {
                Vector3 v = path[j].Position - desde; v.y = 0f;
                float sobre = Vector3.Dot(v, dir);
                if (sobre <= 0f || sobre >= largo) continue; // queda fuera del tramo
                if ((v - dir * sobre).magnitude > CorredorMetros) return false;
            }
            return true;
        }

        /// <summary>Rotación de la flecha pintada en el asfalto: acostada
        /// (mirando al cielo) y con el "arriba" de la textura apuntando a
        /// donde toca doblar.</summary>
        public static Quaternion ArrowRotation(Vector3 flatDir)
        {
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude < 1e-6f) flatDir = Vector3.forward;
            return Quaternion.LookRotation(Vector3.up, flatDir.normalized);
        }

        /// <summary>Opacidad de la flecha: invisible más allá del anuncio,
        /// llena encima del giro (aparece de a poco, no de golpe).</summary>
        public static float ArrowAlpha(float metersToTurn)
        {
            if (metersToTurn >= AnnounceMeters) return 0f;
            if (metersToTurn <= OrderNowMeters) return 1f;
            return Mathf.InverseLerp(AnnounceMeters, OrderNowMeters, metersToTurn);
        }
    }

    /// <summary>
    /// El copiloto visual de la misión: recalcula la ruta A* por las calles
    /// hacia la meta cada ratito y muestra la instrucción bajo el temporizador
    /// ("SIGUE ADELANTE", "GIRA A LA DERECHA"...). En la meta de "volver a
    /// casa" sin armar, guía hacia el punto más lejano (aléjate del barrio) y
    /// al armarse la baliza, guía de regreso. Lo crea MissionRunner.
    /// </summary>
    public class RouteGuide : MonoBehaviour
    {
        private const float RefreshEvery = 0.6f; // recalcular ruta (barato, grafos chicos)
        private const float GoalNearby = 35f;    // desde aquí "la meta está adelante"

        private MissionRunner _runner;
        private Transform _player;
        private RoadNode _awayNode; // destino mientras la meta de volver no se arma
        private Text _text;
        private Text _arrow;
        private GameObject _pill;
        private float _refreshIn;
        private Vector3 _target;
        private bool _hasTarget;

        private Transform _flecha;
        private Material _flechaMat;
        private Vector3 _flechaPos; // apoyo en la calzada, cacheado (no cada frame)

        public void Init(MissionRunner runner, Transform player)
        {
            _runner = runner;
            _player = player;
            BuildPill();
            BuildFlecha();
        }

        /// <summary>La flecha pintada en el asfalto (estilo Waze): un quad
        /// acostado con material propio URP. Permitido: el gotcha del build
        /// corrupto es exclusivo de los TextMesh con la textura del atlas de
        /// la fuente; una malla con material propio no lo toca.</summary>
        private void BuildFlecha()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FlechaGuia";
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.localScale = new Vector3(4f, 7f, 1f);

            // Patrón de transparencia URP del repo (TrafficLightController,
            // MissionSystemBootstrap): sin Src/Dst/ZWrite el Unlit puede
            // quedar con blend opaco y el alpha de ArrowAlpha no se vería
            // (la flecha aparecería de golpe en vez de a poco).
            _flechaMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _flechaMat.color = UITheme.GoldLight;
            _flechaMat.SetFloat("_Surface", 1f); // transparente
            _flechaMat.SetOverrideTag("RenderType", "Transparent");
            _flechaMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _flechaMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _flechaMat.SetFloat("_ZWrite", 0f);
            _flechaMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _flechaMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            quad.GetComponent<Renderer>().material = _flechaMat;

            _flecha = quad.transform;
            _flecha.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_flecha != null) Destroy(_flecha.gameObject);
            if (_flechaMat != null) Destroy(_flechaMat);
        }

        private void BuildPill()
        {
            var canvasGO = new GameObject("GuideCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Más grande que antes (playtest: "más claro y guiado"): flecha
            // protagonista a la izquierda + frase con distancia al giro.
            var pill = UIFactory.Panel("GuidePill", canvas.transform,
                UITheme.A(UITheme.PanelDark, 0.85f), Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, UITheme.RadiusMd);
            UIFactory.SetRect(pill.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, UI.HUDLayout.GuideY), new Vector2(430, 42), new Vector2(0.5f, 1f));

            _arrow = UIFactory.Label("GuideArrow", pill, "↑", 30,
                UITheme.GoldLight, TextAnchor.MiddleCenter, FontStyle.Bold);
            var art = _arrow.rectTransform;
            art.anchorMin = Vector2.zero; art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f);
            art.offsetMin = new Vector2(6f, 0f);
            art.offsetMax = new Vector2(52f, 0f);

            _text = UIFactory.Label("GuideText", pill, "", 22,
                UITheme.GoldLight, TextAnchor.MiddleCenter, FontStyle.Bold);
            var rt = _text.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(52f, 0f); rt.offsetMax = new Vector2(-8f, 0f);
            // El hueco es correcto, pero UIFactory.Label deja el texto en
            // `Overflow`: las frases largas de la guía ("EN 40 m GIRA A LA
            // DERECHA") se derramaban fuera de la píldora. Que encoja hasta
            // 14 antes que salirse.
            UIFactory.FitTextInside(_text, 22, 14);

            _pill = pill.gameObject;
            _pill.SetActive(false);
        }

        private void Update()
        {
            if (_runner == null || _player == null || _pill == null) return;

            // Solo mientras se maneja (ni en el briefing ni en el veredicto).
            bool show = _runner.Running;
            if (_pill.activeSelf != show) _pill.SetActive(show);
            if (!show)
            {
                if (_flecha != null) _flecha.gameObject.SetActive(false);
                return;
            }

            _refreshIn -= Time.deltaTime;
            if (_refreshIn <= 0f)
            {
                _refreshIn = RefreshEvery;
                RecomputeTarget();
            }
            if (!_hasTarget)
            {
                if (_flecha != null) _flecha.gameObject.SetActive(false);
                return;
            }

            // Meta armada y cerca: ya no hay que girar, hay que llegar.
            Vector3 goalPos = _runner.GoalTransform != null
                ? _runner.GoalTransform.position : _target;
            if (_runner.GoalArmed &&
                Vector3.Distance(_player.position, goalPos) < GoalNearby)
            {
                _arrow.text = "◈";
                _text.text = "¡LA META BRILLA ADELANTE!";
                _arrow.color = _text.color = UITheme.GoldLight;
                _pill.transform.localScale = Vector3.one;
                if (_flecha != null) _flecha.gameObject.SetActive(false);
                return;
            }

            // La instrucción guiada: flecha grande + frase con metros al giro
            // + color por urgencia + pulso cuando el giro está encima.
            float meters = Vector3.Distance(_player.position, _target);
            var step = RouteGuideMath.StepFor(_player.forward, _target - _player.position);
            _arrow.text = RouteGuideMath.ArrowFor(step);
            _text.text = RouteGuideMath.PhraseFor(step, meters);
            _arrow.color = _text.color = RouteGuideMath.ColorFor(step);

            bool encima = meters < 15f &&
                (step == GuideStep.GiraIzquierda || step == GuideStep.GiraDerecha);
            _pill.transform.localScale = Vector3.one *
                (encima ? 1f + 0.08f * Mathf.PingPong(Time.unscaledTime * 2.5f, 1f) : 1f);

            ActualizarFlecha(step, _target, meters);
        }

        /// <summary>Pinta (o esconde) la flecha del giro sobre el asfalto.</summary>
        private void ActualizarFlecha(GuideStep paso, Vector3 puntoDelGiro, float metros)
        {
            bool esGiro = paso == GuideStep.GiraIzquierda || paso == GuideStep.GiraDerecha;
            float alpha = esGiro ? RouteGuideMath.ArrowAlpha(metros) : 0f;
            if (_flecha == null || alpha <= 0.01f)
            {
                if (_flecha != null) _flecha.gameObject.SetActive(false);
                return;
            }

            // La posición apoyada (_flechaPos) se recalcula solo cada
            // RefreshEvery, en RecomputeTarget: el raycast es lo caro, no
            // hace falta tirarlo cada frame para una flecha casi estática.
            Vector3 salida = puntoDelGiro - _player.position; // hacia dónde se dobla
            _flecha.SetPositionAndRotation(_flechaPos, RouteGuideMath.ArrowRotation(salida));
            var c = UITheme.GoldLight; c.a = alpha;
            _flechaMat.color = c;
            _flecha.gameObject.SetActive(true);
        }

        /// <summary>Apoyo en la calzada para un punto del grafo: usa
        /// RaycastAll (no un Raycast simple) porque en la Ciudad Toon hay
        /// autopistas elevadas por ENCIMA de calles (gotcha documentado) y
        /// el primer impacto puede ser el tablero de arriba, no el asfalto
        /// real; con 12 NPCs de tráfico también puede pegar en el techo de
        /// un auto. Se queda con el impacto cuya Y esté más cerca de la Y
        /// del punto del grafo (esa Y YA es el nivel de la calzada, porque
        /// viene de un nodo real de la vía). Si ninguno califica (>2 m de
        /// diferencia), conserva el fallback de siempre.</summary>
        private static Vector3 ApoyoEnCalzada(Vector3 puntoDelGiro)
        {
            Vector3 fallback = puntoDelGiro + Vector3.up * 0.1f;
            var hits = Physics.RaycastAll(puntoDelGiro + Vector3.up * 3f, Vector3.down, 8f);
            Vector3 mejor = fallback;
            float mejorDelta = 2f; // tolerancia: más allá no es la calzada
            foreach (var hit in hits)
            {
                float delta = Mathf.Abs(hit.point.y - puntoDelGiro.y);
                if (delta < mejorDelta)
                {
                    mejorDelta = delta;
                    mejor = hit.point + Vector3.up * 0.08f;
                }
            }
            return mejor;
        }

        // El destino: la meta armada, o (en "volver a casa" sin armar) el punto
        // más lejano del barrio — la dirección natural para alejarse.
        private void RecomputeTarget()
        {
            _hasTarget = false;
            var graph = RoadGraph.Instance != null ? RoadGraph.Instance.Data : null;
            if (graph == null || _runner.GoalTransform == null) return;

            // Mismo criterio que RoutePathMarkers: con la meta apagada por
            // CHECKPOINTS hay que llevar al siguiente checkpoint, no al nodo
            // más lejano de la meta (que es la regla de "volver al inicio" y
            // en el nivel 3 apuntaba exactamente al lado contrario).
            Vector3 destino;
            var siguienteCp = _runner.NextCheckpoint;
            if (_runner.GoalArmed)
            {
                destino = _runner.GoalTransform.position;
            }
            else if (siguienteCp != null)
            {
                destino = siguienteCp.position;
            }
            else
            {
                _awayNode ??= MissionGoals.Farthest(graph, _runner.GoalTransform.position);
                if (_awayNode == null) return;
                destino = _awayNode.Position;
            }

            // Ruta por las calles (A*, el mismo de los NPCs) y waypoint útil.
            var start = graph.NearestNode(_player.position);
            var end = graph.NearestNode(destino);
            Vector3? wp = null;
            if (start != null && end != null)
                wp = RouteGuideMath.PickWaypoint(
                    AStarPlanner.FindPath(graph, start.Id, end.Id),
                    _player.position, RouteGuideMath.LookAheadMeters);

            _target = wp ?? destino; // sin ruta (fuera del grafo): línea recta
            _flechaPos = ApoyoEnCalzada(_target); // mismo cadencia que _target
            _hasTarget = true;
        }
    }
}
