using UnityEngine;
using UnityEngine.UI;
using HablaCamaron.Core;
using HablaCamaron.UI;
using HablaCamaron.Vehicle;
using HablaCamaron.World;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// Dirige UNA misión dentro de su escena de zona: pausa y muestra el
    /// briefing de Don Pancho, marca la META con una baliza dorada, corre el
    /// temporizador (HUD propio: tiempo + distancia), cuenta infracciones
    /// reales (PlayerInfractions) y al terminar calcula la evaluación del GDD
    /// y la registra en el progreso. El examen final usa las pantallas de
    /// cierre narrativo en lugar de la evaluación normal.
    /// Lo crea MissionSystemBootstrap al cargar una escena con misión.
    /// </summary>
    public class MissionRunner : MonoBehaviour
    {
        /// <summary>Distancia a la que hay que alejarse para armar la meta "volver".</summary>
        public const float ReturnArmDistance = 60f;

        /// <summary>Alto del haz de la baliza (m): tiene que sobresalir del
        /// skyline para verse desde el otro extremo de la ciudad.</summary>
        public const float BeaconHeight = 60f;

        public MissionDef Def;

        /// <summary>La meta actual (la leen el minimapa y la guía de ruta).</summary>
        public Transform GoalTransform => _goal;
        /// <summary>false mientras la meta de "volver" no se ha armado.</summary>
        public bool GoalArmed => _goalArmed;

        /// <summary>
        /// El checkpoint que toca cruzar AHORA, o null si la misión no usa
        /// checkpoints o ya se pasaron todos. Lo consultan la guía de ruta,
        /// las flechas del asfalto y el minimapa: con la meta apagada tienen
        /// que llevar hasta AQUÍ, no a la meta (que todavía no cuenta).
        /// </summary>
        public Transform NextCheckpoint =>
            _checkpoints != null && _cpPasados >= 0 && _cpPasados < _checkpoints.Count
                ? _checkpoints[_cpPasados].transform
                : null;
        /// <summary>true solo mientras se maneja (ni briefing ni veredicto).</summary>
        public bool Running => _running;
        /// <summary>Por qué reprobó al instante (muerte súbita); vacío en el resto de casos.
        /// La pantalla de evaluación la antepone a la frase de Don Pancho.</summary>
        public string FailReason { get; private set; }

        private VehicleController _player;
        private PlayerInfractions _infractions;
        private RoadDiscipline _discipline;
        // Muerte súbita (misiones con StrictRules, id≥2): rojo o contravía
        // sostenida reprueban al instante en vez de solo descontar puntos.
        private StrictRuleJudge _strict;
        private Transform _goal;
        private GameObject _beacon;
        private float _timeLeft;
        private bool _running, _finished;
        private bool _goalArmed = true; // ReturnToStart la desarma hasta alejarse
        private Text _timerText;
        private RectTransform _timerPill;

        /// <summary>Ancho de la píldora del timer (px de referencia).</summary>
        private const float TimerPillWidth = 560f;
        // Reprobación DURA (playtest): fuera de la vía o volcado = misión perdida.
        private readonly DrivingFailJudge _failJudge = new DrivingFailJudge();
        private float _failCheckIn;
        // Vueltas completadas y el contador que las mide RODEANDO la meta
        // (null en las misiones de una sola llegada).
        private int _lapsDone;
        private LapCounter _laps;

        /// <summary>Punto alrededor del cual se cuentan las vueltas. NO es la
        /// meta: en la ciudad el ancla `Meta_Redondel` cae 14 m descentrada del
        /// redondel real (medido), y como LapCounter acumula el ángulo
        /// alrededor de este punto, el anillo que se conduce no lo encerraba y
        /// la segunda vuelta no llegaba nunca (playtest: "no se puede regresar
        /// para la segunda vuelta").</summary>
        private Vector3 _lapCenter;
        // Checkpoints (Def.Checkpoints): la meta nace apagada y estos la arman
        // al pasarlos TODOS en orden (playtest: la meta se tocaba de arranque).
        private System.Collections.Generic.List<MissionCheckpointMarker> _checkpoints;
        private int _cpPasados;
        private int _cpTotal;

        private void Start()
        {
            // Varias misiones comparten zona: manda hc_current_mission si aplica.
            Def ??= MissionCatalog.ForScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                PlayerPrefs.GetInt(MissionCatalog.KEY_CURRENT, -1));
            if (Def == null) { Destroy(gameObject); return; }

            _player = FindFirstObjectByType<VehicleController>();
            if (_player == null) { Destroy(gameObject); return; }

            _infractions = _player.gameObject.GetComponent<PlayerInfractions>() ??
                           _player.gameObject.AddComponent<PlayerInfractions>();

            // La disciplina vial (contravía / fuera de la calle) acompaña
            // siempre al inspector de infracciones durante la misión.
            _discipline = _player.gameObject.GetComponent<RoadDiscipline>() ??
                          _player.gameObject.AddComponent<RoadDiscipline>();

            // Muerte súbita (StrictRules): desde la misión 3 en adelante, un
            // rojo o una contravía sostenida reprueban al instante. Los
            // tutoriales (id 0 y 1) no la traen: el juez ni se instancia.
            if (Def.StrictRules)
            {
                _strict = new StrictRuleJudge();
                _infractions.OnRedLightCrossed += HandleRedLightCrossed;
            }

            // Las misiones de varias vueltas se ganan RODEANDO la meta.
            if (Def.Laps > 1) _laps = new LapCounter();

            _timeLeft = Def.TimeLimit;
            // La ciudad grande se pone de noche si la misión lo pide (así una
            // sola escena enorme sirve de día y de noche).
            if (Def.NightMood) World.CityMood.ApplyNight();
            FindGoal();
            ResolverCentroDeVueltas();
            BuildBeacon();
            // Checkpoints (nivel 3): se siembran DESPUÉS de la baliza (ya
            // apagada por FindGoal/BuildBeacon si Def.Checkpoints).
            if (Def.Checkpoints) BuildCheckpoints();
            BuildTimerUI();

            // El copiloto visual: "sigue adelante / gira a la..." bajo el timer.
            gameObject.AddComponent<RouteGuide>().Init(this, _player.transform);

            // Y el camino PINTADO en el asfalto: flechas doradas sobre la ruta,
            // que se ven de un vistazo sin tener que leer la píldora.
            gameObject.AddComponent<RoutePathMarkers>().Init(this, _player.transform);

            // Corredor de ruta (nivel 4 en adelante): la misión lleva por SU
            // camino. Se traza después de FindGoal porque necesita la meta.
            if (Def.RouteLocked && _goal != null)
            {
                var corridor = gameObject.AddComponent<RouteCorridor>();
                corridor.OnLost += HandleRouteLost;
                corridor.Init(_player.transform, _goal.position);

                // Y se CIERRAN físicamente las bocacalles que no son del camino:
                // avisar está bien, pero es mejor que no pueda equivocarse.
                gameObject.AddComponent<RouteBarriers>().Build(corridor.Route);
            }

            var traffic = AI.TrafficManager.Instance;
            if (traffic != null) traffic.MaxNpcs = Def.TrafficDensity;

            // Briefing con el juego congelado; arranca al pulsar "¡Vamos!".
            // BLINDAJE (playtest: "el juego se congela al inicio"): si construir
            // la pantalla de briefing revienta con una excepción, el timeScale
            // ya quedó en 0 con nada que lo revierta — el juego se congelaba
            // sin salida. Si Show() falla, se deshace el congelamiento y la
            // misión arranca de una vez, sin briefing, en vez de quedar muerta.
            Time.timeScale = 0f;
            try
            {
                MissionBriefingController.Show(Def.Briefing, () =>
                {
                    Time.timeScale = 1f;
                    _running = true;
                    DonPanchoDialogue.Instance?.Trigger(DialogueEvent.MissionStart);
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Habla Camarón] El briefing de '{Def.Title}' falló " +
                                $"({ex.Message}); arranca la misión sin briefing para no " +
                                "dejar el juego congelado.");
                Time.timeScale = 1f;
                _running = true;
            }
        }

        // ---- La meta según el GoalMode de la misión (varias comparten zona) ----
        private void FindGoal()
        {
            var graphData = RoadGraph.Instance != null ? RoadGraph.Instance.Data : null;
            Vector3 spawn = _player.transform.position;

            switch (Def.Goal)
            {
                case GoalMode.ReturnToStart:
                    // Volver a casa: la baliza se arma recién al alejarse.
                    _goalArmed = false;
                    _goal = NewGoalAt(spawn);
                    return;

                case GoalMode.NamedAnchor:
                    // Una misma ciudad grande aloja varias misiones: cada una
                    // tiene su ancla de meta con nombre, puesta por el builder.
                    var ancla = GameObject.Find(Def.GoalAnchor);
                    if (ancla != null) { _goal = ancla.transform; return; }
                    // Sin esto el cambio de ruta/meta pasa desapercibido (la
                    // misión igual "funciona" con el nodo más lejano, pero ya
                    // no es la ruta que el builder diseñó) — un aviso barato
                    // para no repetir el playtest que lo descubrió jugando.
                    Debug.LogWarning($"[Habla Camarón] Ancla de meta '{Def.GoalAnchor}' " +
                                      $"no encontrada en la escena para '{Def.Title}': " +
                                      "cae al nodo alcanzable más lejano.");
                    break; // sin ancla: cae al modo por defecto (nodo más lejano)

                case GoalMode.ReturnAside:
                    // Volver, pero A UN LADO del inicio (nunca encima del
                    // spawn): un nodo de la misma calle, calle abajo.
                    _goalArmed = false;
                    var aside = MissionGoals.AsideFromStart(graphData, spawn, 18f, 55f);
                    _goal = NewGoalAt(aside != null ? aside.Position : spawn);
                    return;

                case GoalMode.RoundaboutEntry:
                    var light = MissionGoals.NearestTrafficLight(graphData, spawn);
                    if (light != null) { _goal = NewGoalAt(light.Position); return; }
                    break; // sin semáforos: cae al modo por defecto
            }

            // MetaOrFarthest: el ancla [MetaExamen] manda si la escena lo trae.
            var meta = GameObject.Find("[MetaExamen]");
            if (meta != null)
            {
                _goal = meta.transform;
                // Checkpoints (nivel 3): la meta nace apagada hasta pasarlos
                // todos en orden — playtest: se podía tocar desde el arranque.
                if (Def.Checkpoints) _goalArmed = false;
                return;
            }

            var far = MissionGoals.Farthest(graphData, spawn);
            _goal = far != null ? NewGoalAt(far.Position) : transform;
            if (Def.Checkpoints) _goalArmed = false;
        }

        private Transform NewGoalAt(Vector3 position)
        {
            var go = new GameObject("[MetaMision]");
            go.transform.position = position;
            return go.transform;
        }

        /// <summary>
        /// El centro REAL del redondel que hay que rodear. Se busca la pieza
        /// de redondel más cercana a la meta; si no hay ninguna cerca, se
        /// cuenta alrededor de la meta (que es lo que se hacía siempre).
        /// Sin esto, en la ciudad se giraba alrededor de un punto que quedaba
        /// FUERA del anillo conducido y no se acumulaban los 360°.
        /// </summary>
        private void ResolverCentroDeVueltas()
        {
            Vector3 referencia = _goal != null ? _goal.position : transform.position;
            _lapCenter = referencia;
            if (_laps == null) return;

            // OJO: se mide SIEMPRE contra `referencia` (la meta), no contra
            // `_lapCenter`, que va cambiando — si no, tras el primer candidato
            // las distancias se compararían contra el centro recién elegido.
            const float BuscarHasta = 60f; // más allá ya no es "este" redondel
            float mejor = BuscarHasta;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Roundabout")) continue;
                float d = Vector3.Distance(t.position, referencia);
                if (d < mejor) { mejor = d; _lapCenter = t.position; }
            }

            if (_lapCenter != referencia)
                Debug.Log($"[Habla Camarón] Vueltas alrededor del redondel real " +
                          $"({Vector3.Distance(_lapCenter, referencia):0} m de la meta).");
        }

        private void BuildBeacon()
        {
            _beacon = new GameObject("Baliza_Meta_Raiz");
            _beacon.transform.SetParent(_goal, false);
            // La meta "volver al inicio" esconde la baliza hasta armarse
            // (una columna dorada encima del auto confundiría al jugador).
            _beacon.SetActive(_goalArmed);

            // Checkpoints (nivel 3): si el ancla de meta trae su propio
            // Collider/Renderer (p. ej. [MetaExamen] de otro builder), también
            // se apagan hasta armar — si no, se podría "tocar" la meta física
            // aunque la baliza esté invisible.
            if (Def.Checkpoints && !_goalArmed)
            {
                var col0 = _goal.GetComponent<Collider>();
                if (col0 != null) col0.enabled = false;
                var rend0 = _goal.GetComponent<Renderer>();
                if (rend0 != null) rend0.enabled = false;
            }

            // Columna ALTA: medido en el nivel 4, la meta queda a 370 m y los
            // edificios la tapan por completo — el jugador arrancaba sin ver
            // adónde iba. Con 60 m de haz sobresale del skyline y se ve desde
            // el otro lado de la ciudad, como la luz de una feria.
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Baliza_Meta";
            beacon.transform.SetParent(_beacon.transform, false);
            beacon.transform.localPosition = Vector3.up * BeaconHeight * 0.5f;
            beacon.transform.localScale = new Vector3(5f, BeaconHeight * 0.5f, 5f);
            Object.Destroy(beacon.GetComponent<Collider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(1f, 0.75f, 0.3f, 0.45f);
            mat.SetFloat("_Surface", 1f); // transparente
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            beacon.GetComponent<Renderer>().material = mat;

            var lightGO = new GameObject("Luz_Meta");
            lightGO.transform.SetParent(_beacon.transform, false);
            lightGO.transform.localPosition = Vector3.up * 3f;
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.4f);
            light.range = 18f;
            light.intensity = 3f;
        }

        /// <summary>
        /// Siembra los checkpoints invisibles sobre la ruta A* spawn→meta
        /// (playtest: la meta del nivel se podía tocar desde el arranque).
        /// Si no hay grafo o no hay camino, cae a la recta spawn-meta —mejor
        /// eso que dejar la misión sin meta jamás armable.
        /// </summary>
        private void BuildCheckpoints()
        {
            var puntos = new System.Collections.Generic.List<Vector3>();
            var graph = RoadGraph.Instance != null ? RoadGraph.Instance.Data : null;
            if (graph != null && _goal != null)
            {
                var a = graph.NearestNode(_player.transform.position);
                var b = graph.NearestNode(_goal.position);
                if (a != null && b != null)
                {
                    var path = AI.AStarPlanner.FindPath(graph, a.Id, b.Id);
                    if (path != null)
                        foreach (var n in path) puntos.Add(n.Position);
                }
            }
            if (puntos.Count < 2 && _goal != null)
            {
                puntos.Clear();
                puntos.Add(_player.transform.position);
                puntos.Add(_goal.position);
            }

            var posiciones = MissionCheckpoints.PickPositions(puntos);
            _cpTotal = posiciones.Count;
            _checkpoints = new System.Collections.Generic.List<MissionCheckpointMarker>(_cpTotal);
            for (int i = 0; i < posiciones.Count; i++)
                _checkpoints.Add(MissionCheckpointMarker.Crear(transform, posiciones[i], i,
                    _player.transform, HandleCheckpointPassed));

            // Sin checkpoints que sembrar (ruta vacía): no dejar la meta
            // inalcanzable — se arma de una vez.
            if (_cpTotal == 0) ArmGoal();
            else RefrescarMarcasDeCheckpoint();
        }

        /// <summary>Solo se ve el checkpoint que toca: encenderlos todos
        /// convierte la ruta en una feria de columnas y se pierde la lectura
        /// de "ahora ve AHÍ".</summary>
        private void RefrescarMarcasDeCheckpoint()
        {
            if (_checkpoints == null) return;
            for (int i = 0; i < _checkpoints.Count; i++)
                if (_checkpoints[i] != null)
                    _checkpoints[i].MostrarVisual(i == _cpPasados);
        }

        /// <summary>Solo avanza si el checkpoint tocado es el que sigue en
        /// orden (MissionCheckpoints.Advance); al completar todos, arma la
        /// meta (baliza + collider/renderer del ancla, si tenía).</summary>
        private void HandleCheckpointPassed(int indice)
        {
            _cpPasados = MissionCheckpoints.Advance(_cpPasados, indice);
            RefrescarMarcasDeCheckpoint(); // enciende el siguiente, apaga el tocado
            if (MissionCheckpoints.GoalArmed(_cpPasados, _cpTotal) && !_goalArmed)
            {
                ArmGoal();
                // GoalUnlocked, no LapDone: esto es una misión de
                // checkpoints (una sola pasada), no de vueltas — la frase
                // de LapDone ("otra vuelta más") no encajaba acá.
                DonPanchoDialogue.Instance?.Trigger(DialogueEvent.GoalUnlocked);
            }
        }

        /// <summary>Enciende la baliza y reactiva el collider/renderer propios
        /// del ancla de meta, si los tenía apagados por Def.Checkpoints.</summary>
        private void ArmGoal()
        {
            if (_goalArmed) return;
            _goalArmed = true;
            if (_beacon != null) _beacon.SetActive(true);
            if (_goal == null) return;
            var col = _goal.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            var rend = _goal.GetComponent<Renderer>();
            if (rend != null) rend.enabled = true;
        }

        private void BuildTimerUI()
        {
            var canvasGO = new GameObject("MissionCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var pill = UIFactory.Panel("TimerPill", canvas.transform,
                UITheme.A(UITheme.PanelDark, 0.9f), Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, UITheme.RadiusMd);
            // 560 de ancho, no 320: con las misiones de vueltas la línea creció
            // ("TIEMPO 7:11 · VUELTA 1/2 · VE AL REDONDEL — 442 m") y el texto
            // se salía de la píldora por los dos lados (playtest, con foto).
            _timerPill = pill;
            UIFactory.SetRect(pill.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, UI.HUDLayout.TimerY), new Vector2(TimerPillWidth, 40),
                new Vector2(0.5f, 1f));
            UIFactory.AddGlowEdge(pill.gameObject, UITheme.A(UITheme.Gold, 0.4f), 1.5f);

            _timerText = UIFactory.Label("TimerText", pill, "", 20,
                UITheme.TextCream, TextAnchor.MiddleCenter, FontStyle.Bold);
            var rt = _timerText.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (!_running || _finished) return;

            // Muerte súbita: contravía sostenida (el semáforo llega por el
            // evento OnRedLightCrossed, no por acá). Si reprueba, no seguir
            // con el resto del frame (la meta/timer ya no tienen sentido).
            if (_strict != null)
            {
                var fallaStrict = _strict.Evaluate(false, _discipline.CurrentVerdict, Time.deltaTime);
                if (fallaStrict == StrictFail.WrongWay) FailStrict(StrictFail.WrongWay);
                if (_finished) return;
            }

            _timeLeft -= Time.deltaTime;
            float dist = Vector3.Distance(_player.transform.position, _goal.position);

            // Meta "volver al inicio": se arma al alejarse y ahí aparece la
            // baliza. Las misiones con checkpoints usan SU PROPIA lógica de
            // armado (HandleCheckpointPassed, al completarlos en orden): acá
            // "dist" es distancia A LA META, no al punto de partida, así que
            // esta regla de "alejarse" armaría la meta de una al arrancar.
            if (!_goalArmed && !Def.Checkpoints)
            {
                _goalArmed = MissionGoals.UpdateReturnArmed(false, dist, ReturnArmDistance);
                if (_goalArmed && _beacon != null) _beacon.SetActive(true);
            }

            if (_timerText != null)
            {
                int m = Mathf.FloorToInt(Mathf.Max(_timeLeft, 0f) / 60f);
                int s = Mathf.FloorToInt(Mathf.Max(_timeLeft, 0f) % 60f);
                string metaLabel;
                if (!_goalArmed && Def.Checkpoints)
                {
                    // Con la distancia AL SIGUIENTE: "CHECKPOINT 0/4" a secas
                    // no decía ni cuál toca ni hacia dónde (playtest: "las
                    // indicaciones no están claras"). Se numera el que hay que
                    // buscar (1-based), no los ya pasados.
                    var cp = NextCheckpoint;
                    metaLabel = cp != null
                        ? $"CHECKPOINT {_cpPasados + 1}/{_cpTotal} — " +
                          $"{Vector3.Distance(_player.transform.position, cp.position):0} m"
                        : $"CHECKPOINT {_cpPasados}/{_cpTotal}";
                }
                else
                    metaLabel = _goalArmed ? $"META {dist:0} m" : "ALÉJATE DEL BARRIO";
                if (_laps != null)
                {
                    // En las de vueltas se dice lo que hay que HACER: rodear el
                    // redondel, y cuánto llevas de la vuelta en curso. Textos
                    // CORTOS: la línea entera tiene que caber en la píldora.
                    // La distancia va al CENTRO DEL REDONDEL (no a la meta):
                    // es el mismo punto que se rodea, así que "AL REDONDEL" y
                    // "RODÉALO" hablan de lo mismo y el cambio de un texto al
                    // otro coincide con entrar al anillo.
                    float alRedondel = Vector3.Distance(_player.transform.position, _lapCenter);
                    metaLabel = alRedondel > LapCounter.RingRadius
                        ? $"AL REDONDEL {alRedondel:0} m"
                        : $"RODÉALO {_laps.Progress * 100f:0}%";
                    metaLabel = MissionGoals.LapLabel(_lapsDone, Def.Laps) + " · " + metaLabel;
                }
                else if (Def.Laps > 1)
                    metaLabel = MissionGoals.LapLabel(_lapsDone, Def.Laps) + " · " + metaLabel;
                _timerText.text = $"{m}:{s:00}   ·   {metaLabel}";
                _timerText.color = _timeLeft < 30f ? UITheme.Danger : UITheme.TextCream;
            }

            // Salirse de la calle o volcar reprueba AL INSTANTE (con el colchón
            // del juez para perdonar brincos). Chequeo barato cada cuarto de
            // segundo: recorrer ~100 aristas 4 veces por segundo no pesa.
            _failCheckIn -= Time.deltaTime;
            if (_failCheckIn <= 0f)
            {
                const float paso = 0.25f;
                _failCheckIn = paso;
                var graphData = RoadGraph.Instance != null ? RoadGraph.Instance.Data : null;
                float distVia = graphData != null
                    ? graphData.DistanceToNearestEdge(_player.transform.position) : 0f;
                var falta = _failJudge.Tick(distVia,
                    Vector3.Dot(_player.transform.up, Vector3.up), paso);
                // Calificado completo: "FailReason" a secas ahora es también
                // la propiedad string de esta clase (la razón de la muerte
                // súbita) — sin el prefijo el compilador la prefiere a ella
                // sobre el enum de DrivingFailJudge y no compila.
                if (falta != global::HablaCamaron.Missions.FailReason.None)
                {
                    DonPanchoDialogue.Instance?.Trigger(
                        falta == global::HablaCamaron.Missions.FailReason.Rollover
                            ? DialogueEvent.Rollover : DialogueEvent.OutOfBounds);
                    Finish(reached: false);
                    return;
                }
            }

            // MISIONES DE VUELTAS (el redondel): se cuentan RODEANDO la meta,
            // no yendo y viniendo. Antes había que tocar la baliza, alejarse
            // 60 m y volver — en un redondel eso no significaba nada y el
            // playtest lo dijo: "te hace dar la vuelta en la calle".
            if (_laps != null)
            {
                Vector3 d3 = _player.transform.position - _lapCenter;
                if (_laps.Tick(new Vector2(d3.x, d3.z)))
                {
                    _lapsDone = _laps.Laps;
                    if (_lapsDone >= Mathf.Max(Def.Laps, 1)) { Finish(reached: true); return; }
                    DonPanchoDialogue.Instance?.Trigger(DialogueEvent.LapDone);
                }
                if (_timeLeft <= 0f) Finish(reached: false);
                return;
            }

            if (_goalArmed && dist < 8f) Finish(reached: true);
            else if (_timeLeft <= 0f) Finish(reached: false);
        }

        private void Finish(bool reached)
        {
            _finished = true;
            _running = false;
            Time.timeScale = 0f; // congela mientras se muestra el veredicto

            // Silencio de gameplay: el motor y el ambiente CALLAN en el veredicto
            // (era muy molesto que siguieran rugiendo). OJO: AudioListener.pause
            // NO apaga el audio procedural (OnAudioFilterRead), por eso se usa la
            // mordaza del mixer, que multiplica las muestras por cero; la pausa
            // del listener apaga los clips (bocinas NPC a medio sonar). La música
            // y la voz de Don Pancho ignoran ambas y sí suenan.
            // MissionSystemBootstrap restaura todo al cargar cualquier escena.
            GameAudioSettings.GameplayMuted = true;
            AudioListener.pause = true;

            var stats = _infractions.ToStats(reached, Def.TimeLimit - _timeLeft, Def.TimeLimit);
            var result = MissionScoring.Compute(stats, Def.Id);
            // La muerte súbita antepone su razón concreta a la frase genérica
            // del veredicto (la pantalla de evaluación muestra donPanchoMessage).
            if (!string.IsNullOrEmpty(FailReason))
                result.donPanchoMessage = FailReason + ". " + result.donPanchoMessage;
            bool passed = result.totalScore >= 70;

            GameManager.Instance?.Data.RecordMissionResult(Def.Id, result.totalScore);

            // Chat de Mishel pendiente para el regreso al mapa (motor emocional).
            if (passed && Def.ChatAfter != null)
            {
                PlayerPrefs.SetInt(MissionCatalog.KEY_PENDING_CHAT, Def.Id);
                PlayerPrefs.Save();
            }

            DonPanchoDialogue.Instance?.Trigger(
                passed ? DialogueEvent.MissionPass : DialogueEvent.MissionFail);

            if (Def.IsFinal)
                // El cierre narrativo del GDD (2 finales); la muerte súbita
                // (examen/bonus id 6-7, ambos StrictRules) pasa su razón para
                // que se anteponga en el final malo (EndingScreen la ignora si viene null).
                EndingScreen.Show(passed, FailReason);
            else
                EvaluationScreen.Show(result);
        }

        // El semáforo llega por evento (PlayerInfractions ya sabe justo cuándo
        // se cruzó el nodo); la contravía se sondea cada frame en Update.
        private void HandleRedLightCrossed() => FailStrict(StrictFail.RedLight);

        /// <summary>Se fue del camino de la misión y no volvió: se acabó.</summary>
        private void HandleRouteLost()
        {
            if (_finished) return;
            FailReason = "Te saliste del camino de la misión";
            DonPanchoDialogue.Instance?.Trigger(DialogueEvent.OutOfBounds);
            Finish(reached: false);
        }

        /// <summary>Muerte súbita: reprobado inmediato con la razón visible.</summary>
        private void FailStrict(StrictFail causa)
        {
            if (_finished) return;
            FailReason = causa == StrictFail.RedLight
                ? "Te pasaste el semáforo en ROJO"
                : "Manejaste en CONTRAVÍA";
            // RedLight: PlayerInfractions YA disparó DialogueEvent.RedLightRun
            // en el mismo cruce (dispara el trigger y RECIÉN AHÍ invoca
            // OnRedLightCrossed, que termina llamando acá) — repetirlo es
            // puro ruido, aunque el cooldown de Don Pancho se lo trague.
            // WrongWay sí necesita el disparo: llega directo del veredicto
            // de RoadDiscipline por Update, sin un Trigger previo garantizado.
            if (causa == StrictFail.WrongWay)
                DonPanchoDialogue.Instance?.Trigger(DialogueEvent.WrongWay);
            Finish(reached: false);
        }

        private void OnDestroy()
        {
            // Regla del repo: toda suscripción a event Action se desprende
            // (el objeto del jugador puede sobrevivir al runner entre escenas).
            if (_infractions != null) _infractions.OnRedLightCrossed -= HandleRedLightCrossed;
        }
    }
}
