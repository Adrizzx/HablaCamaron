using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>Un puerto de conexión de una pieza de vía: el punto del borde
    /// donde el asfalto continúa hacia la pieza vecina.</summary>
    public struct RoadPort
    {
        public Vector3 Position;  // centro del borde conectable (mundo)
        public Vector3 Outward;   // dirección saliente (unitaria, XZ)
        public int PieceId;       // qué pieza es dueña del puerto
        public float LaneOffset;  // separación del carril respecto del eje
    }

    /// <summary>
    /// El corazón del escáner de la ciudad YA CONSTRUIDA (Demo_Scene_1 de
    /// Toon City): PURO y testeado en EditMode. Dos puertos ENFRENTADOS se
    /// cosen; cada puerto aporta un carril de entrada y uno de salida (mano
    /// derecha, como Ecuador); cada pieza conecta sus entradas con sus
    /// salidas (recta, T o cruz dan lo correcto sin conocer su interior); y
    /// los extremos sueltos ganan retorno para que nadie quede atrapado.
    /// RoadScanKit (editor) detecta los puertos con raycasts y llama aquí.
    /// </summary>
    public static class RoadPortGraph
    {
        /// <summary>
        /// Empareja puertos enfrentados: a menos de tolerance metros y
        /// mirándose (outward casi opuestos). Cada puerto se cose UNA vez,
        /// prefiriendo el más cercano. Devuelve pares de índices.
        /// </summary>
        public static List<(int a, int b)> Match(IReadOnlyList<RoadPort> ports, float tolerance)
        {
            var pairs = new List<(int, int)>();
            var used = new HashSet<int>();
            float tolSq = tolerance * tolerance;

            for (int i = 0; i < ports.Count; i++)
            {
                if (used.Contains(i)) continue;
                int best = -1;
                float bestSq = tolSq;
                for (int j = i + 1; j < ports.Count; j++)
                {
                    if (used.Contains(j)) continue;
                    if (ports[i].PieceId == ports[j].PieceId) continue; // no auto-costura
                    if (Vector3.Dot(ports[i].Outward, ports[j].Outward) > -0.7f) continue;
                    // Un puente NO se cose con la calle que pasa por debajo:
                    // la altura debe coincidir aunque el XZ sea el mismo.
                    if (Mathf.Abs(ports[i].Position.y - ports[j].Position.y) > 1.2f) continue;
                    float sq = (ports[i].Position - ports[j].Position).sqrMagnitude;
                    if (sq <= bestSq) { bestSq = sq; best = j; }
                }
                if (best >= 0)
                {
                    used.Add(i);
                    used.Add(best);
                    pairs.Add((i, best));
                }
            }
            return pairs;
        }

        /// <summary>
        /// COSTURA DE RESCATE: une las islas que deja Match.
        ///
        /// Medido en la ciudad Toon (2026-07-25): Match dejaba 314 piezas en
        /// **35 componentes** (la mayor de 58 piezas) porque muchas uniones de
        /// la demo no cumplen "puertos enfrentados a menos de 4.5 m" — hay
        /// piezas irregulares (rampas de autopista) donde el detector saca 0 o
        /// 1 puerto. Resultado: solo el 14.6 % de los destinos tenían ruta A*,
        /// los NPCs se quedaban parados replanificando y la meta "el nodo más
        /// alto ALCANZABLE" del nivel 3 caía en la isla del spawn (cuesta de
        /// desnivel 0).
        ///
        /// Aquí se cierra el grafo con un árbol de expansión mínima entre
        /// componentes (Kruskal): de todos los pares de puertos de componentes
        /// DISTINTAS que estén a tiro, se van tomando los más cercanos y solo
        /// se acepta el que une dos islas. Un puerto ya cosido puede recibir
        /// otra costura (es una arista más, no rompe la geometría del carril).
        /// PURA y testeada: devuelve SOLO las costuras añadidas.
        /// </summary>
        /// <param name="maxDistance">Alcance del rescate (m). Por encima del
        /// separador de la demo pero por debajo de una cuadra.</param>
        /// <summary>Pendiente máxima que puede tener una costura de rescate.
        /// Sin este tope, dos puertos a 1.2 m de altura y 0.9 m de distancia
        /// producían una arista de 53° — una PARED. El grafo quedaba "conectado"
        /// pero por rampas que ningún auto sube, y las rutas A* pasaban por
        /// ellas (medido en la ciudad: pendMax 53.9°).</summary>
        public const float MaxStitchSlopeDeg = 15f;

        /// <param name="maxHeightDelta">Diferencia de altura tolerada (m): así
        /// una autopista elevada no se cose con la calle que pasa debajo.</param>
        public static List<(int a, int b)> StitchComponents(IReadOnlyList<RoadPort> ports,
            IReadOnlyList<(int a, int b)> pairs, float maxDistance, float maxHeightDelta)
        {
            var extra = new List<(int, int)>();
            if (ports.Count == 0) return extra;

            // Union-Find sobre PIEZAS: el estado inicial es lo que cosió Match.
            int maxPiece = 0;
            for (int i = 0; i < ports.Count; i++)
                if (ports[i].PieceId > maxPiece) maxPiece = ports[i].PieceId;
            var parent = new int[maxPiece + 1];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }
            bool Union(int x, int y)
            {
                int rx = Find(x), ry = Find(y);
                if (rx == ry) return false;
                parent[rx] = ry;
                return true;
            }

            foreach (var (a, b) in pairs) Union(ports[a].PieceId, ports[b].PieceId);

            // Candidatos: pares de puertos a tiro y a la misma altura. Se
            // ordenan por distancia y Kruskal se queda con los que unen islas.
            var cand = new List<(float d, int i, int j)>();
            float maxSq = maxDistance * maxDistance;
            for (int i = 0; i < ports.Count; i++)
                for (int j = i + 1; j < ports.Count; j++)
                {
                    if (ports[i].PieceId == ports[j].PieceId) continue;
                    float dy = System.Math.Abs(ports[i].Position.y - ports[j].Position.y);
                    if (dy > maxHeightDelta) continue;
                    float sq = (ports[i].Position - ports[j].Position).sqrMagnitude;
                    if (sq > maxSq) continue;

                    // Y que la costura sea una RAMPA, no una pared: si el
                    // desnivel es grande respecto de lo que separa a los dos
                    // puertos, unirlos crearía una vía imposible de subir.
                    Vector3 plano = ports[i].Position - ports[j].Position;
                    plano.y = 0f;
                    float horiz = plano.magnitude;
                    if (horiz < 0.01f) continue;
                    if (Mathf.Atan2(dy, horiz) * Mathf.Rad2Deg > MaxStitchSlopeDeg) continue;

                    cand.Add((sq, i, j));
                }
            cand.Sort((x, y) => x.d.CompareTo(y.d));

            foreach (var (_, i, j) in cand)
                if (Union(ports[i].PieceId, ports[j].PieceId)) extra.Add((i, j));

            return extra;
        }

        /// <summary>Pendiente máxima (grados) de una arista INTERNA de pieza.
        /// Por encima de esto no es una calle: es el salto de un nivel a otro.
        /// Generosa respecto de los 15° de las costuras — dentro de una misma
        /// pieza sí puede haber una rampa de verdad.</summary>
        public const float MaxInnerSlopeDeg = 25f;

        /// <summary>¿Se puede subir/bajar este tramo conduciendo?</summary>
        public static bool TramoConducible(Vector3 a, Vector3 b)
        {
            float dy = Mathf.Abs(a.y - b.y);
            float horiz = new Vector2(a.x - b.x, a.z - b.z).magnitude;
            if (horiz < 0.01f) return dy < 0.5f; // encimados: solo si están al ras
            return Mathf.Atan2(dy, horiz) * Mathf.Rad2Deg <= MaxInnerSlopeDeg;
        }

        /// <summary>
        /// Construye el grafo dirigido de carriles sobre el RoadGraphData:
        /// por puerto un nodo de SALIDA (deja la pieza, carril derecho) y uno
        /// de ENTRADA; internas entrada→salidas de la misma pieza; costuras
        /// salida↔entrada entre piezas; retorno (U) en puertos sueltos.
        /// </summary>
        /// <summary>Cuánto más "caro" es cruzar una costura de RESCATE que una
        /// calle normal. No la cierra (a veces es el único paso): solo hace que
        /// A* la evite si existe cualquier alternativa por calzada de verdad.</summary>
        public const float PenalizacionRescate = 12f;

        public static void Build(RoadGraphData g, IReadOnlyList<RoadPort> ports,
            List<(int a, int b)> pairs,
            IReadOnlyList<(int a, int b)> rescate = null)
        {
            // Las costuras de rescate son asfalto tendido sobre lo que no era
            // calle: transitables, pero último recurso para el trazado.
            var caras = new HashSet<(int, int)>();
            if (rescate != null)
                foreach (var par in rescate) { caras.Add(par); caras.Add((par.b, par.a)); }

            var entry = new RoadNode[ports.Count];
            var exit = new RoadNode[ports.Count];
            for (int i = 0; i < ports.Count; i++)
            {
                var p = ports[i];
                Vector3 right = Vector3.Cross(Vector3.up, p.Outward).normalized;
                exit[i] = g.AddNode(p.Position + right * p.LaneOffset);
                entry[i] = g.AddNode(p.Position - right * p.LaneOffset);
            }

            // La pieza reparte: lo que entra por un puerto sale por los demás.
            // Una pieza de UN solo puerto (los bordes del puente elevado la
            // producen) da la vuelta adentro: entrada → su propia salida.
            for (int i = 0; i < ports.Count; i++)
            {
                bool tieneHermanos = false;
                for (int j = 0; j < ports.Count; j++)
                {
                    if (i == j || ports[i].PieceId != ports[j].PieceId) continue;

                    // NO se conectan dos puertos de la misma pieza si el tramo
                    // que los une es impracticable. La demo tiene piezas cuyos
                    // puertos están a alturas MUY distintas (el arranque de la
                    // autopista elevada), y unirlos en línea recta metía en el
                    // grafo una "calle" de 53.9° — medido: 8.15 m de desnivel
                    // en 5.9 m de planta. Esa arista fantasma era el único
                    // acceso "válido" al redondel de la ciudad, así que el
                    // nivel 4 acababa con su meta a 72 m del redondel real.
                    if (!TramoConducible(entry[i].Position, exit[j].Position)) continue;

                    g.Connect(entry[i].Id, exit[j].Id);
                    tieneHermanos = true;
                }
                if (!tieneHermanos) g.Connect(entry[i].Id, exit[i].Id);
            }

            // Costuras entre piezas (en ambos sentidos de circulación).
            var matched = new HashSet<int>();
            foreach (var (a, b) in pairs)
            {
                float peso = caras.Contains((a, b)) ? PenalizacionRescate : 1f;
                g.Connect(exit[a].Id, entry[b].Id, peso);
                g.Connect(exit[b].Id, entry[a].Id, peso);
                matched.Add(a);
                matched.Add(b);
            }

            // Extremo suelto: el que sale da la vuelta y vuelve a entrar.
            for (int i = 0; i < ports.Count; i++)
                if (!matched.Contains(i))
                    g.Connect(exit[i].Id, entry[i].Id);
        }
    }
}
