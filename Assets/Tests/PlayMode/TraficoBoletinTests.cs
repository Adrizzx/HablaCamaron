using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HablaCamaron.AI;
using HablaCamaron.World;
using HablaCamaron.Missions;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// EL INSTRUMENTO DE MEDICIÓN del tráfico (plan
    /// docs/superpowers/plans/2026-07-29-ia-npc-comportamiento.md, Paso 0).
    ///
    /// Toda la historia de fracasos de este subsistema es de gente "mejorando"
    /// la IA a ojo y validando con UNA corrida. Este test no arregla nada:
    /// MIDE, en la escena real y con los NPC reales, las 8 métricas del
    /// boletín, y escupe UNA línea comparable entre corridas:
    ///
    ///   [BOLETIN] escena=... semilla=... npcs=N | superpuestos=0 | flotando=0 |
    ///             contravia=0 | vereda=0 | spawnsVisibles=0 | bloqueados&gt;10s=1 |
    ///             rojosPorMin=0.80 | girosU=0
    ///
    /// DOS POBLACIONES a la vez, porque miden cosas distintas:
    ///  · REPARTIDA — NPC sembrados por stride sobre TODO el grafo (mismo
    ///    patrón que NpcCarrilDiagTests). El anillo de spawn del
    ///    [TrafficManager] ambiental (45-110 m del jugador) deja medio mapa sin
    ///    cubrir, así que sin esto no se mide la ciudad, solo su esquina.
    ///  · AMBIENTAL — el [TrafficManager] DE VERDAD, encendido a densidad 12.
    ///    Es la única población que permite medir `spawnsVisibles`: los NPC
    ///    sembrados a mano no pasan por su lógica de aparición.
    /// `superpuestos` se mide sobre las DOS (un choque es un choque).
    ///
    /// TRAMPAS DE MEDICIÓN respetadas (§4.3 del plan, todas verificadas):
    ///  · El suelo bajo un NPC es el impacto MÁS ALTO del RaycastAll, no "¿hay
    ///    calzada debajo?": en las esquinas la vereda se apoya ENCIMA de la
    ///    pieza de calle, así que preguntar por calzada da "va bien" con el
    ///    auto subido al bordillo.
    ///  · ...pero descartando OTROS VEHÍCULOS del rayo: un auto de Toon City
    ///    mide ~1.5 m y su techo sería "la superficie más alta" (mismo bug que
    ///    ya se corrigió dentro de NpcDriver.TryGroundHeight).
    ///  · Collider.ClosestPoint NO se usa en ningún lado: con MeshCollider no
    ///    convexo (todos los de Toon City) devuelve el propio punto de consulta
    ///    y da 0 m para todo.
    ///  · El rumbo de cada NPC sale del DESPLAZAMIENTO real, no de
    ///    transform.forward: NpcTurnLimit deja la carrocería adelantada
    ///    respecto a hacia dónde se mueve de verdad en una curva cerrada.
    ///
    /// Semilla fija e impresa en el boletín: sin ella dos corridas no se pueden
    /// comparar. OJO — no da determinismo perfecto: Physics.SphereCast/
    /// RaycastAll no es bit-exacto entre corridas (documentado en
    /// SemaforosNpcDiagTests), por eso el protocolo exige ≥4 corridas antes de
    /// creerse cualquier resultado.
    /// </summary>
    public class TraficoBoletinTests
    {
        // ---------------- Configuración de la medición ----------------

        private const int Semilla = 20260729;
        private const float SegundosSim = 60f;
        private const float IntervaloCarril = 0.5f;   // muestreo de contravía (igual que NpcCarrilDiagTests)
        private const float IntervaloSuelo = 1f;      // muestreo de vereda/flotando (raycasts: caro)
        private const float IntervaloRumbo = 0.25f;   // muestreo de rumbo (giros en U)
        private const float IntervaloSpawn = 0.1f;    // detección de NPC recién nacidos
        private const int NpcRepartidos = 30;         // sembrados por todo el grafo
        private const int NpcAmbientales = 12;        // densidad de "Hora pico", el techo del catálogo

        // ---------------- Umbrales (§6.bis del plan) ----------------

        private const float AlturaFlotando = 1f;      // panza a más de 1 m del suelo
        private const float SolapeFraccion = 0.30f;   // 30% del ancho del auto
        private const float UmbralContravia = 0.10f;  // 10% de las muestras de UN NPC
        private const float UmbralVereda = 0.10f;     // ídem, pisando acera
        private const float SegundosBloqueo = 10f;    // parado sin razón legítima
        private const int MaxBloqueados = 1;          // objetivo: ≤1 de la flota
        private const float GiroUGrados = 130f;       // cambio de rumbo que ya no es una esquina
        private const float VentanaGiroU = 2f;        // ...en menos de esto
        private const float VelocidadMinGiroU = 1.5f; // y rodando de verdad todo el tramo

        private const float AnchoAuto = 1.9f;
        private const float LargoAuto = 4.4f;
        private const float RadioDeteccionJuego = 25f; // el mismo de NpcDriver.Sense

        private string _escenaCargada;

        // ---------------- Ficha por NPC ----------------

        private class Ficha
        {
            public NpcDriver Npc;
            public GameObject Go;
            public string Nombre;          // copia: el Go puede reciclarse antes de loguear
            public bool Ambiental;

            public Vector3 UltimaPos;
            public Vector3 UltimaVel;

            public int Muestras, Contravia;
            public Vector3 PeorContravia;

            public int MuestrasSuelo, MuestrasVereda, MuestrasFlotando;
            public Vector3 PeorVereda;
            public float PeorAltura;

            public float Parado;           // segundos continuos casi quieto sin semáforo
            public float PeorParado;
            public bool ContadoBloqueado;

            public readonly List<Vector3> Rumbos = new List<Vector3>();
            public readonly List<float> Velocidades = new List<float>();
            public int GirosU;
            public float DebounceGiro;

            public readonly HashSet<int> RojosContados = new HashSet<int>();
        }

        // ---------------- Ciclo de vida ----------------

        [UnityTearDown]
        public IEnumerator Limpia()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Core.GameAudioSettings.GameplayMuted = false;
            if (!string.IsNullOrEmpty(_escenaCargada))
            {
                var s = SceneManager.GetSceneByName(_escenaCargada);
                _escenaCargada = null;
                if (s.IsValid() && s.isLoaded)
                {
                    var vacia = SceneManager.CreateScene("PostBoletin");
                    SceneManager.SetActiveScene(vacia);
                    yield return SceneManager.UnloadSceneAsync(s);
                }
            }
        }

        [Timeout(900000)]
        [UnityTest]
        public IEnumerator BoletinDeTraficoEnLaCiudadToon()
        {
            var it = Boletin("N1_CiudadToon");
            while (it.MoveNext()) yield return it.Current;
        }

        [Timeout(900000)]
        [UnityTest]
        public IEnumerator BoletinDeTraficoEnLaZonaSur()
        {
            var it = Boletin("N1_ZonaSur");
            while (it.MoveNext()) yield return it.Current;
        }

        // ---------------- La medición ----------------

        private IEnumerator Boletin(string escena)
        {
            _escenaCargada = escena;
            SceneManager.LoadScene(escena);
            yield return null;
            yield return null; // Awake/Start de [RoadGraph], [TrafficLightController], DriverCamera...
            yield return null;

            // Las escenas de zona traen misión: MissionRunner.Start() congela el
            // juego (timeScale = 0) para el briefing de Don Pancho hasta que
            // alguien pulsa "¡Vamos!" — nadie en batch. Se fuerza (mismo patrón
            // que los otros diagnósticos) y se acelera ×4: fixedDeltaTime NO
            // cambia con timeScale, así que la física y el conteo de pasos
            // siguen siendo exactos, solo más rápidos de medir.
            Time.timeScale = 4f;

            var graph = RoadGraph.Instance;
            Assert.IsNotNull(graph, $"{escena} debe traer un [RoadGraph].");
            var nodes = graph.Data.Nodes;
            Assert.Greater(nodes.Count, 1, $"El grafo de {escena} está vacío.");

            var luces = new List<RoadNode>();
            foreach (var n in nodes) if (n.TrafficLightGroup >= 0) luces.Add(n);

            Random.InitState(Semilla);

            // El tráfico AMBIENTAL de verdad, a densidad de hora pico: es la
            // única población cuyos nacimientos pasan por TrafficManager y por
            // tanto la única con la que se puede medir `spawnsVisibles`.
            var ambiental = TrafficManager.Instance;
            if (ambiental != null) ambiental.MaxNpcs = NpcAmbientales;

            // Población REPARTIDA por todo el grafo.
            var perfiles = new[] { DriverProfile.Particular(), DriverProfile.Taxista(), DriverProfile.Buseta() };
            var fichas = new List<Ficha>();
            var conocidos = new HashSet<int>();

            int stride = Mathf.Max(1, nodes.Count / NpcRepartidos);
            int idx = 0;
            for (int i = 0; i < nodes.Count && idx < NpcRepartidos; i += stride)
            {
                var nodo = nodes[i];
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Boletin_{idx}_n{nodo.Id}";
                go.transform.position = nodo.Position + Vector3.up * 0.75f;
                go.transform.localScale = new Vector3(AnchoAuto, 1.4f, LargoAuto);
                go.AddComponent<Rigidbody>().isKinematic = true;
                var npc = go.AddComponent<NpcDriver>();
                npc.Init(graph.Data, perfiles[idx % perfiles.Length]);
                fichas.Add(NuevaFicha(npc, false));
                idx++;
            }

            // Los ya existentes (el ambiental pudo nacer alguno en estos frames)
            // NO cuentan como "aparición": se marcan como conocidos sin medir.
            foreach (var npc in Object.FindObjectsByType<NpcDriver>(FindObjectsSortMode.None))
            {
                if (npc == null) continue;
                conocidos.Add(npc.GetInstanceID());
                bool sembrado = false;
                foreach (var f in fichas) if (f.Npc == npc) { sembrado = true; break; }
                if (!sembrado) fichas.Add(NuevaFicha(npc, true));
            }

            var cam = Camera.main;
            var lights = TrafficLightController.Instance;
            Debug.Log($"[Boletin] {escena}: {nodes.Count} nodos, {luces.Count} semaforizados, " +
                      $"stride {stride}. Sembrados {idx} repartidos + ambiental a {NpcAmbientales}. " +
                      $"Cámara del jugador: {(cam != null ? cam.name : "NINGUNA (spawnsVisibles no medible)")}.");

            int spawnsVisibles = 0, spawnsEnFrustum = 0, spawnsTotales = 0;
            // PARES ÚNICOS, no eventos de muestreo: dos autos encajados 30 s
            // seguidos son UN problema, no 60. Contar por muestra daría números
            // enormes que además no se pueden comparar con el umbral de §6.bis
            // ni con NpcRedondelDiagTests (que mide una sola vez, al final).
            var paresSuperpuestos = new HashSet<long>();
            int eventosSolape = 0;
            float peorSolapeX = 0f, peorSolapeZ = 0f;
            int rojos = 0;

            float dt = Time.fixedDeltaTime;
            int pasos = Mathf.RoundToInt(SegundosSim / dt);
            float tCarril = 0f, tSuelo = 0f, tRumbo = 0f, tSpawn = 0f, tSolape = 0f;

            for (int paso = 0; paso < pasos; paso++)
            {
                yield return new WaitForFixedUpdate();

                // --- cada paso: velocidad real y acumulador de bloqueo ---
                foreach (var f in fichas)
                {
                    if (f.Go == null) continue;
                    Vector3 pos = f.Go.transform.position;
                    f.UltimaVel = (pos - f.UltimaPos) / dt;
                    f.UltimaPos = pos;

                    // Esperar un semáforo NO es estar bloqueado (EsperandoSemaforo
                    // se propaga hacia atrás por toda la cola, ver NpcQueueWait).
                    bool legitimo = f.Npc != null && f.Npc.EsperandoSemaforo;
                    float v = f.Npc != null ? f.Npc.Speed : f.UltimaVel.magnitude;
                    f.Parado = (v < 0.3f && !legitimo) ? f.Parado + dt : 0f;
                    if (f.Parado > f.PeorParado) f.PeorParado = f.Parado;
                    if (f.Parado > SegundosBloqueo) f.ContadoBloqueado = true;

                    if (f.DebounceGiro > 0f) f.DebounceGiro -= dt;
                }

                tCarril += dt; tSuelo += dt; tRumbo += dt; tSpawn += dt; tSolape += dt;

                // --- contravía (mismo juez que usa el juego con el jugador) ---
                if (tCarril >= IntervaloCarril)
                {
                    tCarril = 0f;
                    foreach (var f in fichas)
                    {
                        if (f.Go == null) continue;
                        // El adelantamiento invade el carril contrario A PROPÓSITO
                        // (NpcDriver.OvertakeOffset): esas muestras no se miden,
                        // igual que en NpcCarrilDiagTests.
                        if (f.Npc != null && f.Npc.CurrentState == NpcState.Overtaking) continue;

                        var veredicto = LaneJudge.Judge(graph.Data, f.UltimaPos, f.UltimaVel);
                        f.Muestras++;
                        if (veredicto == LaneVerdict.WrongWay)
                        {
                            f.Contravia++;
                            f.PeorContravia = f.UltimaPos;
                        }
                    }

                    // --- cruces en rojo (mismo criterio que el juez del jugador) ---
                    if (lights != null)
                    {
                        foreach (var f in fichas)
                        {
                            if (f.Go == null) continue;
                            foreach (var luz in luces)
                            {
                                if (f.RojosContados.Contains(luz.Id)) continue;
                                if (lights.GetGroupState(luz.TrafficLightGroup) != LightState.Red) continue;
                                if (Vector3.Distance(f.UltimaPos, luz.Position) > RadioDeteccionJuego) continue;
                                if (!RedLightJudge.CrossedNode(f.UltimaPos, f.Go.transform.forward, luz.Position)) continue;

                                f.RojosContados.Add(luz.Id);
                                rojos++;
                                Debug.LogWarning($"[Boletin] ROJO: {f.Nombre} atravesó el semáforo del nodo " +
                                                 $"#{luz.Id} {luz.Position} en rojo.");
                            }
                        }
                    }
                }

                // --- vereda y flotando (raycasts: se muestrea más espaciado) ---
                if (tSuelo >= IntervaloSuelo)
                {
                    tSuelo = 0f;
                    foreach (var f in fichas)
                    {
                        if (f.Go == null) continue;
                        if (!SueloBajo(f.Go, out float sueloY, out bool esVereda)) continue;

                        f.MuestrasSuelo++;
                        if (esVereda)
                        {
                            f.MuestrasVereda++;
                            f.PeorVereda = f.UltimaPos;
                        }

                        float panza = PanzaDe(f.Go);
                        float alto = panza - sueloY;
                        if (alto > AlturaFlotando)
                        {
                            f.MuestrasFlotando++;
                            if (alto > f.PeorAltura) f.PeorAltura = alto;
                        }
                    }
                }

                // --- rumbo (giros en U) ---
                if (tRumbo >= IntervaloRumbo)
                {
                    tRumbo = 0f;
                    int ventana = Mathf.RoundToInt(VentanaGiroU / IntervaloRumbo);
                    foreach (var f in fichas)
                    {
                        if (f.Go == null) continue;
                        Vector3 r = f.UltimaVel; r.y = 0f;
                        float vel = r.magnitude;
                        f.Rumbos.Add(vel > 0.01f ? r.normalized : Vector3.zero);
                        f.Velocidades.Add(vel);
                        if (f.Rumbos.Count > ventana + 1)
                        {
                            f.Rumbos.RemoveAt(0);
                            f.Velocidades.RemoveAt(0);
                        }
                        if (f.Rumbos.Count <= ventana || f.DebounceGiro > 0f) continue;

                        // Solo cuenta si venía RODANDO todo el tramo: un auto casi
                        // parado tiene rumbo puro ruido y daría falsos giros en U.
                        bool rodando = true;
                        foreach (float v in f.Velocidades) if (v < VelocidadMinGiroU) { rodando = false; break; }
                        if (!rodando) continue;

                        float ang = Vector3.Angle(f.Rumbos[0], f.Rumbos[f.Rumbos.Count - 1]);
                        if (ang <= GiroUGrados) continue;

                        f.GirosU++;
                        f.DebounceGiro = 3f; // no recontar el mismo giro paso a paso
                        Debug.LogWarning($"[Boletin] GIRO EN U: {f.Nombre} cambió {ang:0} ° de rumbo en " +
                                         $"{VentanaGiroU:0.0} s rodando, en {f.UltimaPos}.");
                    }
                }

                // --- nacimientos: ¿aparece dentro de lo que el jugador ve? ---
                if (tSpawn >= IntervaloSpawn && cam != null)
                {
                    tSpawn = 0f;
                    var planos = GeometryUtility.CalculateFrustumPlanes(cam);
                    foreach (var npc in Object.FindObjectsByType<NpcDriver>(FindObjectsSortMode.None))
                    {
                        if (npc == null || !conocidos.Add(npc.GetInstanceID())) continue;

                        spawnsTotales++;
                        fichas.Add(NuevaFicha(npc, true));
                        var b = BoundsDe(npc.gameObject);
                        if (!GeometryUtility.TestPlanesAABB(planos, b)) continue;

                        // Dentro del cono. Pero "aparecer de la nada" es que el
                        // jugador lo VEA aparecer: tras un edificio no ve nada.
                        // Se reportan las DOS cifras para que el antes/después
                        // no dependa de haber cambiado la definición a mitad.
                        spawnsEnFrustum++;
                        if (TapadoDesde(cam.transform.position, npc.gameObject)) continue;

                        spawnsVisibles++;
                        Debug.LogWarning($"[Boletin] SPAWN VISIBLE: {npc.name} nació a la vista del jugador, " +
                                         $"en {npc.transform.position} " +
                                         $"(a {Vector3.Distance(npc.transform.position, cam.transform.position):0} m).");
                    }
                }

                // --- superpuestos (dos autos ocupando el mismo sitio) ---
                if (tSolape >= IntervaloCarril)
                {
                    tSolape = 0f;
                    float umbral = AnchoAuto * SolapeFraccion;
                    for (int i = 0; i < fichas.Count; i++)
                    {
                        if (fichas[i].Go == null) continue;
                        for (int j = i + 1; j < fichas.Count; j++)
                        {
                            if (fichas[j].Go == null) continue;
                            Vector3 a = fichas[i].Go.transform.position;
                            Vector3 b = fichas[j].Go.transform.position;
                            if (Mathf.Abs(a.y - b.y) > 2f) continue; // pisos distintos: no es un choque real

                            float sx = AnchoAuto - Mathf.Abs(a.x - b.x);
                            float sz = LargoAuto - Mathf.Abs(a.z - b.z);
                            if (sx <= umbral || sz <= umbral) continue;

                            eventosSolape++;
                            peorSolapeX = Mathf.Max(peorSolapeX, sx);
                            peorSolapeZ = Mathf.Max(peorSolapeZ, sz);
                            long par = (long)fichas[i].Npc.GetInstanceID() * 100003L + fichas[j].Npc.GetInstanceID();
                            if (!paresSuperpuestos.Add(par)) continue; // ya reportado: no repetir por muestra
                            Debug.LogWarning($"[Boletin] SUPERPUESTOS: {fichas[i].Nombre} y {fichas[j].Nombre} " +
                                             $"— solape X={sx:0.00} m, Z={sz:0.00} m, en {a}.");
                        }
                    }
                }
            }

            // ---------------- Recuento ----------------

            int vivos = 0, contravia = 0, vereda = 0, flotando = 0, bloqueados = 0, girosU = 0;
            foreach (var f in fichas)
            {
                if (f.Go != null) vivos++;
                girosU += f.GirosU;
                if (f.ContadoBloqueado)
                {
                    bloqueados++;
                    Debug.LogWarning($"[Boletin] BLOQUEADO: {f.Nombre} estuvo {f.PeorParado:0.0} s seguidos " +
                                     $"casi quieto sin semáforo que lo justifique, en {f.UltimaPos}.");
                }
                if (f.Muestras > 0 && (float)f.Contravia / f.Muestras > UmbralContravia)
                {
                    contravia++;
                    Debug.LogWarning($"[Boletin] CONTRAVÍA: {f.Nombre} — {f.Contravia}/{f.Muestras} muestras " +
                                     $"({(float)f.Contravia / f.Muestras:P0}); última en {f.PeorContravia}.");
                }
                if (f.MuestrasSuelo > 0 && (float)f.MuestrasVereda / f.MuestrasSuelo > UmbralVereda)
                {
                    vereda++;
                    Debug.LogWarning($"[Boletin] VEREDA: {f.Nombre} — {f.MuestrasVereda}/{f.MuestrasSuelo} muestras " +
                                     $"pisando acera ({(float)f.MuestrasVereda / f.MuestrasSuelo:P0}); última en {f.PeorVereda}.");
                }
                if (f.MuestrasFlotando > 0)
                {
                    flotando++;
                    Debug.LogWarning($"[Boletin] FLOTA: {f.Nombre} — {f.MuestrasFlotando} muestra(s) con la panza " +
                                     $"a más de {AlturaFlotando:0.0} m del suelo (peor {f.PeorAltura:0.00} m).");
                }
            }
            float rojosPorMin = rojos / (SegundosSim / 60f);
            int superpuestos = paresSuperpuestos.Count;

            Debug.Log($"[BOLETIN] escena={escena} semilla={Semilla} segundos={SegundosSim:0} " +
                      $"npcs={vivos}/{fichas.Count} | superpuestos={superpuestos} | flotando={flotando} | " +
                      $"contravia={contravia} | vereda={vereda} | " +
                      $"spawnsVisibles={spawnsVisibles}/{spawnsTotales} (enFrustum={spawnsEnFrustum}) | " +
                      $"bloqueados>{SegundosBloqueo:0}s={bloqueados} | rojosPorMin={rojosPorMin:0.00} | girosU={girosU}");
            if (superpuestos > 0)
                Debug.Log($"[BOLETIN] peor solape: X={peorSolapeX:0.00} m, Z={peorSolapeZ:0.00} m " +
                          $"({eventosSolape} evento(s) de muestreo sobre {superpuestos} par(es) distinto(s)).");

            // ---------------- Umbrales (§6.bis del plan) ----------------

            var fallos = new List<string>();
            if (superpuestos > 0) fallos.Add($"superpuestos={superpuestos} (exigido 0)");
            if (flotando > 0) fallos.Add($"flotando={flotando} (exigido 0)");
            if (contravia > 0) fallos.Add($"contravia={contravia} (exigido 0)");
            if (vereda > 0) fallos.Add($"vereda={vereda} (exigido 0)");
            if (spawnsVisibles > 0) fallos.Add($"spawnsVisibles={spawnsVisibles} de {spawnsTotales} (exigido 0)");
            if (bloqueados > MaxBloqueados) fallos.Add($"bloqueados>{SegundosBloqueo:0}s={bloqueados} (objetivo ≤{MaxBloqueados})");
            if (girosU > 0) fallos.Add($"girosU={girosU} (objetivo 0)");
            // `rojosPorMin` va SIN aserción A PROPÓSITO. El umbral de <2/min del
            // plan está calibrado sobre SemaforosNpcDiagTests, que mide OTRA
            // cosa: siembra NPC con margen real de frenado ante 2 accesos
            // vigilados y cuenta los cruces de ESE acercamiento. Aquí los NPC
            // nacen repartidos por todo el grafo —varios encima de un nodo
            // semaforizado, ya dentro de la banda de CrossedNode— y se vigilan
            // los 11 semáforos a la vez, así que la línea base salió en 12-15/min
            // sin que eso signifique una regresión. Comparar los dos números
            // sería un error de método: el guardarraíl de semáforos sigue siendo
            // SemaforosNpcDiagTests, y aquí la cifra solo sirve para vigilar que
            // no se dispare respecto a la línea base de ESTE test.

            Assert.IsEmpty(fallos,
                $"Boletín de {escena} fuera de umbral: {string.Join(" · ", fallos)} — ver el detalle de cada " +
                "infractor en el log ([Boletin] ...).");
        }

        // ---------------- Utilidades de medición ----------------

        private static Ficha NuevaFicha(NpcDriver npc, bool ambiental) => new Ficha
        {
            Npc = npc,
            Go = npc.gameObject,
            Nombre = npc.gameObject.name,
            Ambiental = ambiental,
            UltimaPos = npc.transform.position,
        };

        /// <summary>
        /// Superficie que el auto PISA y si es acera. Es el impacto MÁS ALTO
        /// del RaycastAll (§4.3: en las esquinas la vereda se apoya ENCIMA de
        /// la pieza de calle, así que "¿hay calzada debajo?" diría que va bien
        /// con el auto subido al bordillo), descartando el propio auto y
        /// CUALQUIER otro vehículo (un techo de Toon City está a ~1.5 m y sería
        /// "la superficie más alta").
        /// </summary>
        private static bool SueloBajo(GameObject go, out float sueloY, out bool esVereda)
        {
            sueloY = 0f;
            esVereda = false;
            float mejor = float.NegativeInfinity;
            Transform ganador = null;

            foreach (var h in Physics.RaycastAll(go.transform.position + Vector3.up * 3f, Vector3.down, 60f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.root == go.transform.root) continue;
                if (EsVehiculo(h.collider)) continue;
                if (h.point.y <= mejor) continue;
                mejor = h.point.y;
                ganador = h.collider.transform;
            }

            if (ganador == null) return false;
            sueloY = mejor;
            esVereda = EsAcera(ganador);
            return true;
        }

        /// <summary>El nombre de la acera puede estar en la pieza o en cualquier
        /// padre (los builders agrupan; Toon City usa el prefab Pavement_*).</summary>
        private static bool EsAcera(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                string n = p.name;
                if (n.IndexOf("Pavement", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Vereda", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool EsVehiculo(Collider col)
        {
            if (col == null) return false;
            var root = col.transform.root;
            return root.GetComponentInChildren<NpcDriver>() != null ||
                   root.GetComponentInChildren<Vehicle.VehicleController>() != null;
        }

        private static Bounds BoundsDe(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one * 2f);
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Altura de la panza (lo más bajo de la carrocería): medir el
        /// flotar con el PIVOTE mentiría distinto para un cubo de diagnóstico
        /// que para un prefab de Toon City.</summary>
        private static float PanzaDe(GameObject go) => BoundsDe(go).min.y;

        /// <summary>
        /// ¿Hay algo sólido entre el ojo del jugador y este auto? Los demás
        /// VEHÍCULOS no cuentan como pared: esconderse detrás de otro auto no
        /// es esconderse (el de delante se aparta y deja al de atrás a la
        /// vista). Mismo criterio que usa TrafficManager al decidir el spawn,
        /// para que el test mida lo que el juego promete.
        /// </summary>
        private static bool TapadoDesde(Vector3 ojo, GameObject go)
        {
            // La SILUETA, no un punto, y con las MISMAS alturas que usa el
            // juego (NpcSpawnRules.AlturasDeSilueta): con un solo rayo a 0.9 m,
            // regla y medición discrepaban de forma sistemática — un muro bajo
            // tapaba la línea al nivel del capó y el techo quedaba a la vista.
            Vector3 pie = go.transform.position;
            var b = BoundsDe(go);
            pie.y = b.min.y;
            foreach (float alto in NpcSpawnRules.AlturasDeSilueta)
                if (!TapadoHasta(ojo, pie + Vector3.up * alto, go)) return false;
            return true;
        }

        private static bool TapadoHasta(Vector3 ojo, Vector3 destino, GameObject go)
        {
            Vector3 hacia = destino - ojo;
            float largo = hacia.magnitude - 1.5f;
            if (largo <= 0.1f) return false;

            foreach (var h in Physics.RaycastAll(ojo, hacia.normalized, largo,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.root == go.transform.root) continue;
                if (EsVehiculo(h.collider)) continue;
                return true;
            }
            return false;
        }
    }
}
