using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Diagnóstico visual sin abrir el editor: renderiza capturas PNG de una
    /// zona desde la posición del Aveo (vista de conductor en 4 direcciones +
    /// cenital) a Logs/snaps/. Corre en batch:
    /// -executeMethod HablaCamaron.EditorTools.SnapshotTool.SnapZonaSur
    /// (batch SIN -nographics: con gráficos sí renderiza).
    /// </summary>
    public static class SnapshotTool
    {
        public static void SnapZonaSur() => Snap("Assets/Scenes/N1_ZonaSur.unity");
        public static void SnapTestDrive() => Snap("Assets/Scenes/N0_TestDrive.unity");
        public static void SnapSimonBolivar() => Snap("Assets/Scenes/N1_SimonBolivar.unity");
        public static void SnapCorredorExamen() => Snap("Assets/Scenes/N1_CorredorExamen.unity");

        /// <summary>Las 3 zonas jugables en una sola corrida batch.</summary>
        public static void SnapTodas()
        {
            Snap("Assets/Scenes/N1_ZonaSur.unity");
            Snap("Assets/Scenes/N1_SimonBolivar.unity");
            Snap("Assets/Scenes/N1_CorredorExamen.unity");
        }

        public static void Snap(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);
            Directory.CreateDirectory("Logs/snaps");

            var aveo = GameObject.Find("Aveo_Camaron");
            // El MISMO ojo que DriverCamera en el juego (si existe), no uno inventado.
            var driver = aveo != null ? aveo.GetComponent<HablaCamaron.Vehicle.DriverCamera>() : null;
            Vector3 eye = driver != null
                ? aveo.transform.TransformPoint(driver.EyeLocalPos)
                : (aveo != null ? aveo.transform.position + Vector3.up * 1.35f : new Vector3(0f, 2f, 0f));
            Quaternion heading = aveo != null ? aveo.transform.rotation : Quaternion.identity;

            var go = new GameObject("SnapCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;

            string scene = Path.GetFileNameWithoutExtension(scenePath);
            Shot(cam, eye, heading, $"{scene}_frente.png");
            Shot(cam, eye, heading * Quaternion.Euler(0, 90, 0), $"{scene}_der.png");
            Shot(cam, eye, heading * Quaternion.Euler(0, 180, 0), $"{scene}_atras.png");
            Shot(cam, eye, heading * Quaternion.Euler(0, -90, 0), $"{scene}_izq.png");
            Shot(cam, eye + Vector3.up * 60f, Quaternion.Euler(90, 0, 0), $"{scene}_cenital.png");

            // Tomas forenses del spawn: desde fuera mirando AL auto y bajas.
            Vector3 at = eye - Vector3.up * 0.6f; // centro del auto
            Shot(cam, at + heading * new Vector3(-8f, 1.2f, 0f),
                Quaternion.LookRotation(at - (at + heading * new Vector3(-8f, 1.2f, 0f))), $"{scene}_lado_izq.png");
            Shot(cam, at + heading * new Vector3(8f, 1.2f, 0f),
                Quaternion.LookRotation(at - (at + heading * new Vector3(8f, 1.2f, 0f))), $"{scene}_lado_der.png");
            Shot(cam, at + heading * new Vector3(0f, 1.2f, -10f),
                Quaternion.LookRotation(at - (at + heading * new Vector3(0f, 1.2f, -10f))), $"{scene}_detras.png");
            Shot(cam, eye + Vector3.up * 6f, Quaternion.Euler(90, 0, 0), $"{scene}_cenital_bajo.png");

            Object.DestroyImmediate(go);

            // Números exactos para diagnóstico (las fotos engañan).
            string info = aveo != null
                ? $"Aveo={aveo.transform.position} yaw={aveo.transform.eulerAngles.y:0.0} ojo={eye}"
                : "Aveo NO ENCONTRADO";
            var techo = GameObject.Find("Techo");
            if (techo != null) info += $" | TechoGaraje y={techo.transform.position.y:0.00} pos={techo.transform.position}";
            var garaje = GameObject.Find("Garaje_Papa");
            if (garaje != null) info += $" | Garaje pos={garaje.transform.position}";
            if (aveo != null)
            {
                var rends = aveo.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    info += $" | BoundsAveo min={b.min} max={b.max}";
                }
            }
            Debug.Log($"[Habla Camarón] {scene}: {info}");
        }

        /// <summary>
        /// DIAGNÓSTICO DE LA CIMA DEL NIVEL 3 (playtest: "al finalizar el
        /// redondel se ve en azul"). Mide hasta dónde llega el SUELO más allá
        /// de la plaza de retorno —donde se acaba, el jugador ve el cielo— y
        /// deja retratos desde el ojo del conductor, que es la vista donde se
        /// nota. Una cenital no sirve para esto: el hueco solo se ve a la
        /// altura de los ojos.
        /// -executeMethod HablaCamaron.EditorTools.SnapshotTool.DiagCimaNivel3
        /// </summary>
        public static void DiagCimaNivel3()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/N1_ZonaSur.unity");
            Directory.CreateDirectory("Logs/snaps");
            Physics.SyncTransforms();

            var plaza = GameObject.Find("Cima_RedondelRetorno");
            if (plaza == null) { Debug.LogError("[CimaAzul] No hallé la plaza de la cima."); return; }

            Vector3 c = plaza.transform.position;
            Vector3 fwd = plaza.transform.forward;   // sentido de subida
            Vector3 der = plaza.transform.right;

            // ¿Dónde se acaba el piso? Se avanza desde el centro de la plaza y
            // se anota el primer metro SIN nada debajo (eso es el cielo).
            void Barrer(string nombre, Vector3 dir)
            {
                float primerHueco = -1f;
                string ultimo = "nada";
                for (float d = 0f; d <= 220f; d += 2f)
                {
                    Vector3 p = c + dir * d;
                    if (Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out var h, 200f))
                    { ultimo = h.collider.name; continue; }
                    primerHueco = d; break;
                }
                Debug.Log(primerHueco < 0f
                    ? $"[CimaAzul] {nombre}: suelo continuo hasta 220 m (último: {ultimo})"
                    : $"[CimaAzul] {nombre}: SE ACABA EL SUELO a {primerHueco:0} m del centro " +
                      $"(último apoyo: {ultimo})");
            }

            Barrer("adelante", fwd);
            Barrer("atras", -fwd);
            Barrer("derecha", der);
            Barrer("izquierda", -der);

            var piso = GameObject.Find("Piso");
            if (piso != null)
            {
                var b = piso.GetComponent<Renderer>().bounds;
                Debug.Log($"[CimaAzul] Piso: centro={b.center} tamaño={b.size} " +
                          $"| cima en {c} (dista {Vector3.Distance(new Vector3(c.x,0,c.z), b.center):0} m del centro del piso)");
            }

            // Retratos desde el ojo del conductor, entrando a la plaza.
            var go = new GameObject("SnapCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 800f;
            Vector3 ojo = c - fwd * 26f + Vector3.up * 1.8f;
            Shot(cam, ojo, Quaternion.LookRotation(fwd, Vector3.up), "cima_n3_entrando.png");
            Shot(cam, c + Vector3.up * 1.8f, Quaternion.LookRotation(fwd, Vector3.up), "cima_n3_centro.png");
            Shot(cam, c + Vector3.up * 1.8f, Quaternion.LookRotation(der, Vector3.up), "cima_n3_derecha.png");
            // CENITAL: la única vista donde se comprueba de un golpe si los
            // chevrones pintados y el sentido del grafo giran para el mismo
            // lado. La imagen queda orientada con la subida hacia arriba.
            Shot(cam, c + Vector3.up * 58f, Quaternion.LookRotation(Vector3.down, fwd), "cima_n3_cenital.png");
            Object.DestroyImmediate(go);
            Debug.Log("[CimaAzul] Retratos en Logs/snaps/cima_n3_*.png");

            // ---- ¿Adónde manda la guía al llegar arriba? ----
            // (playtest: "nos indica dirección incorrecta"). Se reproduce lo
            // que hace RouteGuide: meta = nodo alcanzable más lejano, ruta A*
            // desde donde está el jugador, y el waypoint que se le enseña.
            var rg = Object.FindFirstObjectByType<HablaCamaron.World.RoadGraph>();
            if (rg == null || rg.Data == null) { Debug.LogWarning("[CimaGuia] sin grafo"); return; }
            var g = rg.Data;

            // ---- ¿Hacia qué lado gira el anillo EN LA ESCENA? ----
            // (playtest: "las indicaciones están por el lado contrario de como
            // funciona un redondel"). No se ordena por ángulo —eso daría
            // antihorario siempre por construcción y no probaría nada—: se
            // RECORRE el ciclo siguiendo las aristas del grafo, que es por
            // donde el A* manda de verdad al jugador.
            var enAnillo = new List<HablaCamaron.World.RoadNode>();
            Vector2 centroXZ = new Vector2(c.x, c.z);
            foreach (var n in g.Nodes)
            {
                float d = Vector2.Distance(new Vector2(n.Position.x, n.Position.z), centroXZ);
                if (d > 4f && d < 30f && Mathf.Abs(n.Position.y - c.y) < 6f) enAnillo.Add(n);
            }
            if (enAnillo.Count >= 3)
            {
                var idsAnillo = new HashSet<int>();
                foreach (var n in enAnillo) idsAnillo.Add(n.Id);

                // Se prueba CADA nodo como arranque y se queda el recorrido más
                // largo. Empezando por uno cualquiera, si toca la salida del
                // anillo (que enlaza fuera del conjunto) el paseo muere en dos
                // pasos y el área sale 0: eso mide el arranque, no el anillo.
                var recorrido = new List<Vector3>();
                foreach (var inicio in enAnillo)
                {
                    var paseo = new List<Vector3>();
                    var visto = new HashSet<int>();
                    var actual = inicio;
                    while (actual != null && visto.Add(actual.Id))
                    {
                        paseo.Add(actual.Position);
                        HablaCamaron.World.RoadNode siguiente = null;
                        foreach (int vec in g.Neighbors(actual.Id))
                            if (idsAnillo.Contains(vec) && !visto.Contains(vec))
                            { siguiente = g.GetNode(vec); break; }
                        actual = siguiente;
                    }
                    if (paseo.Count > recorrido.Count) recorrido = paseo;
                }

                float area = HablaCamaron.World.RingOrientation.AreaConSigno(recorrido);
                bool ok = HablaCamaron.World.RingOrientation.EsAntihorario(recorrido);
                Debug.Log($"[CimaGiro] Anillo recorrido por sus ARISTAS: {recorrido.Count} nodos, " +
                          $"área con signo {area:0} m² ⇒ {(ok ? "ANTIHORARIO ✓ (tránsito por la derecha)" : "HORARIO ✗ — AL REVÉS")}");
                if (!ok)
                    Debug.LogError("[CimaGiro] El redondel de la cima gira al revés: la guía mandará " +
                                   "a rodearlo por el lado contrario.");
            }

            var spawn = GameObject.Find("Aveo_Camaron");
            Vector3 desdeSpawn = spawn != null ? spawn.transform.position : Vector3.zero;
            var metaNodo = HablaCamaron.Missions.MissionGoals.Farthest(g, desdeSpawn);
            if (metaNodo == null) { Debug.LogWarning("[CimaGuia] sin meta"); return; }
            Debug.Log($"[CimaGuia] META en {metaNodo.Position} (a {Vector3.Distance(metaNodo.Position, c):0} m " +
                      $"del centro de la plaza, altura {metaNodo.Position.y:0.0})");

            // El coche entrando a la plaza, mirando hacia arriba de la cuesta.
            Vector3 pos = c - fwd * 26f;
            var a = g.NearestNode(pos);
            var bNodo = g.NearestNode(metaNodo.Position);
            var ruta = HablaCamaron.AI.AStarPlanner.FindPath(g, a.Id, bNodo.Id);
            if (ruta == null) { Debug.LogError("[CimaGuia] NO HAY RUTA del acceso a la meta"); return; }

            Debug.Log($"[CimaGuia] ruta de {ruta.Count} nodos desde la entrada de la plaza");

            // Lo que REALMENTE diría la guía, punto por punto en la subida y
            // dentro de la plaza: se reproduce PickWaypoint + StepFor + PhraseFor.
            foreach (float atras in new[] { 60f, 40f, 26f, 14f, 6f, 0f, -8f })
            {
                Vector3 p = c - fwd * atras;
                var na = g.NearestNode(p);
                var r = HablaCamaron.AI.AStarPlanner.FindPath(g, na.Id, bNodo.Id);
                if (r == null) { Debug.Log($"[CimaGuia] a {atras:0} m: SIN RUTA"); continue; }

                var wp = HablaCamaron.Missions.RouteGuideMath.PickWaypoint(
                    r, p, HablaCamaron.Missions.RouteGuideMath.LookAheadMeters);
                if (wp == null) { Debug.Log($"[CimaGuia] a {atras:0} m: sin waypoint"); continue; }

                Vector3 haciaWp = wp.Value - p;
                var step = HablaCamaron.Missions.RouteGuideMath.StepFor(fwd, haciaWp);
                string frase = HablaCamaron.Missions.RouteGuideMath.PhraseFor(step, haciaWp.magnitude);
                // ¿La línea recta al waypoint atraviesa la isla del centro?
                Vector3 aIsla = new Vector3(c.x, 0f, c.z) - new Vector3(p.x, 0f, p.z);
                Vector3 dirWp = new Vector3(haciaWp.x, 0f, haciaWp.z).normalized;
                float proy = Vector3.Dot(aIsla, dirWp);
                float distEje = proy > 0f
                    ? (aIsla - dirWp * proy).magnitude : 999f;
                bool cruzaIsla = proy > 0f && proy < haciaWp.magnitude && distEje < 8f;

                Debug.Log($"[CimaGuia] a {atras,3:0} m de la plaza -> \"{frase}\" " +
                          $"(wp a {haciaWp.magnitude:0} m)" +
                          (cruzaIsla ? "  <<< LA RECTA CRUZA LA ISLA" : ""));
            }
        }

        /// <summary>
        /// DIAGNÓSTICO DEL NIVEL 4 (playtest: "no se puede regresar para la
        /// segunda vuelta"). La misión pide DOS vueltas al redondel, pero el
        /// corredor de ruta (RouteLocked) vigila la distancia a la ruta
        /// spawn→meta, que termina EN la meta: rodear el redondel te aleja de
        /// esa línea. Aquí se mide cuánto del anillo queda FUERA del corredor.
        /// -executeMethod HablaCamaron.EditorTools.SnapshotTool.DiagNivel4Vueltas
        /// </summary>
        public static void DiagNivel4Vueltas()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/N1_CiudadToon.unity");
            Physics.SyncTransforms();

            var rg = Object.FindFirstObjectByType<HablaCamaron.World.RoadGraph>();
            var meta = GameObject.Find("Meta_Redondel");
            var coche = Object.FindFirstObjectByType<HablaCamaron.Vehicle.VehicleController>();
            if (rg == null || meta == null || coche == null)
            { Debug.LogError("[N4] falta grafo, Meta_Redondel o coche"); return; }
            var g = rg.Data;

            // La MISMA ruta que traza RouteCorridor al empezar la misión.
            var a = g.NearestNode(coche.transform.position);
            var b = g.NearestNode(meta.transform.position);
            var path = HablaCamaron.AI.AStarPlanner.FindPath(g, a.Id, b.Id);
            if (path == null) { Debug.LogError("[N4] sin ruta spawn->meta"); return; }
            var ruta = path.ConvertAll(n => n.Position);
            Debug.Log($"[N4] ruta del corredor: {ruta.Count} nodos, meta en {meta.transform.position}");

            // El redondel de verdad (la pieza del paquete) y su centro.
            Transform anillo = null;
            float mejor = float.MaxValue;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Roundabout")) continue;
                float d = Vector3.Distance(t.position, meta.transform.position);
                if (d < mejor) { mejor = d; anillo = t; }
            }
            Vector3 centro = anillo != null ? anillo.position : meta.transform.position;
            Debug.Log($"[N4] redondel '{(anillo != null ? anillo.name : "NO HALLADO")}' " +
                      $"en {centro} (la meta está a {mejor:0} m de su centro)");

            // ¿Cuánto del giro alrededor del centro cae fuera del corredor?
            foreach (float radio in new[] { 12f, 18f, 25f, 35f })
            {
                int fuera = 0; float peor = 0f;
                for (int i = 0; i < 72; i++)
                {
                    float ang = i / 72f * Mathf.PI * 2f;
                    Vector3 p = centro + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radio;
                    float d = HablaCamaron.Missions.RouteCorridorJudge.DistanceToRoute(ruta, p);
                    if (d > HablaCamaron.Missions.RouteCorridorJudge.CorridorHalfWidth) fuera++;
                    peor = Mathf.Max(peor, d);
                }
                Debug.Log($"[N4] girando a {radio:0} m del centro: {fuera * 100 / 72}% del anillo " +
                          $"FUERA del corredor (peor distancia {peor:0} m, tope " +
                          $"{HablaCamaron.Missions.RouteCorridorJudge.CorridorHalfWidth:0} m)");
            }

            Debug.Log($"[N4] LapCounter.RingRadius={HablaCamaron.Missions.LapCounter.RingRadius} m, " +
                      $"corredor={HablaCamaron.Missions.RouteCorridorJudge.CorridorHalfWidth} m, " +
                      $"se pierde la misión a los {HablaCamaron.Missions.RouteCorridorJudge.LostSeconds}s fuera");

            // ¿Se completan DOS vueltas dando vueltas por el anillo? Se simula
            // el recorrido midiendo el ángulo alrededor de los dos candidatos:
            // la meta (como se hacía antes) y el centro real del redondel.
            foreach (var (nombre, eje) in new[] { ("la META (antes)", meta.transform.position),
                                                  ("el CENTRO real (ahora)", centro) })
            {
                foreach (float radio in new[] { 12f, 18f, 25f })
                {
                    var lc = new HablaCamaron.Missions.LapCounter();
                    int vueltas = 0;
                    // Dos vueltas completas, en pasos finos.
                    for (int i = 0; i <= 720; i++)
                    {
                        // DOS vueltas: cada 360 pasos es una vuelta entera (2π).
                        float ang = i / 360f * 2f * Mathf.PI;
                        Vector3 p = centro + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radio;
                        Vector3 d = p - eje;
                        if (lc.Tick(new Vector2(d.x, d.z))) vueltas++;
                    }
                    Debug.Log($"[N4] rodeando a {radio:0} m midiendo alrededor de {nombre}: " +
                              $"{vueltas} vuelta(s) de 2 {(vueltas >= 2 ? "OK" : "<<< NO SE COMPLETAN")}");
                }
            }
        }

        /// <summary>
        /// DIAGNÓSTICO DE POSTES EN LA CALZADA DEL NIVEL 4 (playtest: "en medio
        /// nivel hay un poste en toda vía"). Recorre la ruta de la misión y
        /// lista TODO objeto cuya geometría invade el carril, diciendo si aún
        /// tiene collider y si se podría mover. Ojo: apagar el collider no
        /// basta — el poste se SIGUE VIENDO en mitad de la calle.
        /// -executeMethod HablaCamaron.EditorTools.SnapshotTool.DiagNivel4Postes
        /// </summary>
        public static void DiagNivel4Postes()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/N1_CiudadToon.unity");
            Physics.SyncTransforms();

            var rg = Object.FindFirstObjectByType<HablaCamaron.World.RoadGraph>();
            var meta = GameObject.Find("Meta_Redondel");
            var coche = Object.FindFirstObjectByType<HablaCamaron.Vehicle.VehicleController>();
            if (rg == null || meta == null || coche == null)
            { Debug.LogError("[Postes] falta grafo, meta o coche"); return; }

            var g = rg.Data;
            var a = g.NearestNode(coche.transform.position);
            var b = g.NearestNode(meta.transform.position);
            var path = HablaCamaron.AI.AStarPlanner.FindPath(g, a.Id, b.Id);
            if (path == null) { Debug.LogError("[Postes] sin ruta"); return; }
            var ruta = path.ConvertAll(n => n.Position);

            // SE USA EL ESCÁNER DEL PROYECTO (RouteClearanceScan): barre la
            // ruta con la CAJA del coche, que es lo único fiable aquí.
            // Intentos fallidos, anotados para que nadie los repita:
            //  · medir del centro de bounds → la farola en L tiene el brazo
            //    sobre la calle a 8 m y salía como obstáculo con el poste en
            //    la vereda;
            //  · `Collider.ClosestPoint` → con MeshCollider NO convexo (todos
            //    los de Toon City lo son) devuelve el propio punto de consulta,
            //    así que TODO daba 0.0 m: edificios, autos y hasta el Aveo.
            var idsRuta = path.ConvertAll(n => n.Id);
            // Dos anchos: el del invariante (la caja del coche pegado al eje) y
            // el del CARRIL ENTERO. El primero da 0 aunque haya un poste a 3 m
            // —queda fuera del barrido— pero el jugador no conduce clavado al
            // eje: por eso el invariante decía "ninguno" y aun así se chocaba.
            foreach (var (etiqueta, semiancho) in new[] { ("caja del coche", 1.6f),
                                                          ("CARRIL ENTERO", 3.6f) })
            {
                var intrusos = RouteClearanceScan.Scan(g, idsRuta, new Vector3(semiancho, 1.2f, 3.0f));
                var porNombre = new Dictionary<string, int>();
                foreach (var cc in intrusos)
                {
                    porNombre.TryGetValue(cc.name, out int k);
                    porNombre[cc.name] = k + 1;
                }
                Debug.Log($"[Postes] barrido con {etiqueta} ({semiancho:0.0} m): " +
                          $"{intrusos.Count} colliders invaden la ruta");
                foreach (var kv in porNombre)
                    Debug.Log($"[Postes]   {kv.Key} ×{kv.Value}");
            }

            // ¿QUÉ HAY BAJO CADA NODO DE LA RUTA? Calzada, vereda o pelado.
            // OJO: no vale "la superficie más cercana" — el Terrain de la demo
            // está a la MISMA altura que el asfalto. Hay que preguntar si
            // existe calzada A TIRO, no cuál queda más cerca. Y `Pavement_` es
            // la ACERA del paquete: RouteClearanceScan la da por suelo bueno,
            // así que una ruta por la vereda pasa el invariante igual.
            int conCalzada = 0, soloVereda = 0, pelado = 0, soloParche = 0;
            var primerasMalas = new List<string>();
            for (int i = 0; i < ruta.Count; i++)
            {
                // LA SUPERFICIE DE ARRIBA, no "si hay calzada en algún sitio":
                // en las esquinas la vereda se apoya ENCIMA de la pieza de
                // calle, así que preguntar "¿hay Road_ debajo?" decía que sí
                // mientras el coche rueda por la acera. Manda el impacto más
                // ALTO, que es lo que se pisa.
                bool calzada = false, vereda = false, parche = false;
                float masAlto = float.NegativeInfinity;
                foreach (var h in Physics.RaycastAll(ruta[i] + Vector3.up * 6f, Vector3.down, 12f))
                {
                    if (h.point.y <= masAlto) continue;
                    string clase = null;
                    for (var t = h.collider.transform; t != null && clase == null; t = t.parent)
                    {
                        string n = t.name;
                        if (n.StartsWith("Road_") || n.StartsWith("Highway_") ||
                            n.StartsWith("Roundabout")) clase = "calzada";
                        else if (n == "Parche_Costura") clase = "parche";
                        else if (n.StartsWith("Pavement")) clase = "vereda";
                    }
                    if (clase == null) continue;
                    masAlto = h.point.y;
                    calzada = clase == "calzada"; parche = clase == "parche"; vereda = clase == "vereda";
                }
                if (calzada) conCalzada++;
                else if (parche) { soloParche++; if (primerasMalas.Count < 8) primerasMalas.Add($"solo PARCHE @ {ruta[i]}"); }
                else if (vereda) { soloVereda++; if (primerasMalas.Count < 8) primerasMalas.Add($"VEREDA @ {ruta[i]}"); }
                else { pelado++; if (primerasMalas.Count < 8) primerasMalas.Add($"SIN NADA @ {ruta[i]}"); }
            }
            Debug.Log($"[Postes] SUELO BAJO LA RUTA ({ruta.Count} nodos): calzada real={conCalzada}, " +
                      $"solo parche={soloParche}, solo vereda={soloVereda}, pelado={pelado} " +
                      $"→ {(soloVereda + pelado + soloParche) * 100 / ruta.Count}% no es calle de verdad");
            foreach (var s in primerasMalas) Debug.Log($"[Postes]   {s}");

            // PRUEBA VISUAL: la vía tal como la ve el jugador, a lo largo de la
            // ruta. Un poste plantado en la calzada se ve aquí y no en ningún
            // número (y los "ghost" sin collider solo se detectan mirando).
            Directory.CreateDirectory("Logs/snaps/n4");
            var camGO = new GameObject("SnapCam");
            var cam2 = camGO.AddComponent<Camera>();
            cam2.fieldOfView = 70f; cam2.nearClipPlane = 0.05f; cam2.farClipPlane = 600f;
            int tomas = 8;
            for (int k = 0; k < tomas; k++)
            {
                int idx = Mathf.Clamp(ruta.Count * k / tomas, 0, ruta.Count - 2);
                Vector3 p = ruta[idx] + Vector3.up * 1.8f;
                Vector3 mira = ruta[Mathf.Min(idx + 1, ruta.Count - 1)] - ruta[idx];
                mira.y = 0f;
                if (mira.sqrMagnitude < 0.01f) mira = Vector3.forward;
                Shot(cam2, p, Quaternion.LookRotation(mira.normalized, Vector3.up),
                     $"n4/ruta_{k:00}.png");
            }
            Object.DestroyImmediate(camGO);
            Debug.Log($"[Postes] {tomas} retratos de la ruta en Logs/snaps/n4/");

            // OJO AL MÉTODO: hay que medir el PUNTO SÓLIDO más cercano al eje
            // del carril, no el centro de bounds. Una farola en L tiene el
            // brazo colgando sobre la calle a 8 m de altura, así que su centro
            // cae sobre la calzada aunque el poste esté en la vereda — medido
            // así salían "postes en la vía" que no lo eran, y hasta el propio
            // Aveo a 0.0 m. Lo que estorba es geometría por DEBAJO del techo
            // del coche y cerca del eje.
            const float MedioCoche = 2.2f;   // medio ancho del coche + holgura
            const float TechoCoche = 2.2f;

            int estorban = 0;
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!col.enabled || col.isTrigger) continue;

                // Ni el coche del jugador ni los NPC son obstáculos fijos.
                if (col.GetComponentInParent<HablaCamaron.Vehicle.VehicleController>() != null) continue;
                if (col.GetComponentInParent<HablaCamaron.AI.NpcDriver>() != null) continue;

                var bb = col.bounds;
                if (bb.min.y > TechoCoche) continue;              // pasa por encima
                if (bb.size.x > 25f || bb.size.z > 25f) continue;  // calzada / edificios

                bool esVia = false;
                for (var t = col.transform; t != null && !esVia; t = t.parent)
                    if (t.name.StartsWith("Road_") || t.name.StartsWith("Highway_") ||
                        t.name.StartsWith("Roundabout") || t.name.StartsWith("Pavement") ||
                        t.name == "Parche_Costura" || t.name == "Terrain" ||
                        t.name == "Piso" || t.name.StartsWith("Cebra"))
                        esVia = true;
                if (esVia) continue;

                // Punto del EJE más cercano a este collider, y punto SÓLIDO del
                // collider más cercano a ese eje.
                Vector3 eje = ruta[0];
                float mejor = float.MaxValue;
                foreach (var p in ruta)
                {
                    float dd = (new Vector2(p.x - bb.center.x, p.z - bb.center.z)).sqrMagnitude;
                    if (dd < mejor) { mejor = dd; eje = p; }
                }
                Vector3 solido = col.ClosestPoint(eje);
                if (solido.y > TechoCoche) continue; // lo cercano queda en alto

                float d = new Vector2(solido.x - eje.x, solido.z - eje.z).magnitude;
                if (d > MedioCoche) continue;

                estorban++;
                if (estorban <= 20)
                    Debug.Log($"[Postes] ESTORBA {col.name} a {d:0.0} m del eje " +
                              $"| movible={PrefabUtility.IsAnyPrefabInstanceRoot(col.gameObject)} " +
                              $"| pos={solido}");
            }
            Debug.Log($"[Postes] TOTAL que golpean al coche (a menos de {MedioCoche:0.0} m " +
                      $"del eje y bajo el techo): {estorban}");
        }

        private static void Shot(Camera cam, Vector3 pos, Quaternion rot, string file)
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            File.WriteAllBytes(Path.Combine("Logs/snaps", file), tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
