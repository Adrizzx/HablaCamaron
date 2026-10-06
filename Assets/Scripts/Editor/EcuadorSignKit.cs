using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// FASE 4 — Construye señales ecuatorianas POR CÓDIGO a partir del
    /// EcuadorSignCatalog: poste + placa (malla poligonal con la forma del
    /// reglamento: octágono, triángulo, círculo, rombo, rectángulo) + borde +
    /// texto 3D (TextMesh, nítido sin texturas) + banda de prohibición.
    /// Las funcionales llevan RoadSign con sus nodos del grafo.
    /// El FRENTE de la señal es -Z local: con yaw 0 la lee quien AVANZA hacia
    /// +Z (carril de ida); con yaw 180, quien viene de regreso.
    /// </summary>
    public static class EcuadorSignKit
    {
        // OJO (2026-07-12): al agrandar señales (PlateY 3.1/PlateR 0.75/char
        // 0.15) el level del player salió ilegible ("level7 corrupted",
        // reproducible y determinista) y con los valores originales carga.
        // Causa exacta sin aislar — NO tocar estos números sin correr el
        // smoke test del .exe después.
        private const float PlateY = 2.05f;   // altura del centro de la placa
        private const float PlateR = 0.42f;   // "radio" de la placa

        // Legibilidad SIN agrandar la placa (arreglo de playtest 2026-07-27):
        // el borde se hace más grueso y la plancha del texto va MÁS CHICA
        // que la placa — ninguno de los dos toca PlateR.
        private const float BorderScale = 1.20f;  // antes 1.12: borde más ancho
        private const float BackingScale = 0.58f; // plancha bajo el texto

        /// <summary>Coloca una señal del catálogo. affectedNodes agrega el
        /// componente RoadSign (señal funcional para IA y puntaje).</summary>
        public static GameObject Place(string key, Transform parent,
            Vector3 localPos, float yawDeg, int[] affectedNodes = null)
        {
            var spec = EcuadorSignCatalog.Get(key);
            if (spec == null)
            {
                Debug.LogWarning($"[Habla Camarón] Señal desconocida: {key}");
                return null;
            }

            var root = new GameObject("Senal_" + spec.Key);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            // Escala EN LA RAÍZ (mallas y textos intactos): agrandar cambiando
            // PlateY/PlateR/characterSize corrompía el level del player.
            root.transform.localScale = Vector3.one * 1.45f;

            BuildPole(root.transform);
            BuildPlate(root.transform, spec);

            if (spec.Efecto != SignType.None || affectedNodes != null)
            {
                var data = root.AddComponent<RoadSign>();
                data.Type = spec.Efecto;
                data.AffectedNodeIds = affectedNodes ?? new int[0];
            }
            return root;
        }

        private static void BuildPole(Transform root)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Poste";
            pole.transform.SetParent(root, false);
            pole.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            pole.transform.localScale = new Vector3(0.07f, 1.1f, 0.07f);
            pole.GetComponent<Renderer>().sharedMaterial =
                SolidMaterial(new Color(0.45f, 0.47f, 0.50f)); // galvanizado
        }

        private static void BuildPlate(Transform root, SignSpec spec)
        {
            // Playtest (2026-07-27): "las señales se leen mal" — la solución
            // es CONTRASTE, nunca tamaño (agrandar la placa corrompe el level
            // del player, gotcha "level7 corrupted"). Tres retoques, todos de
            // COLOR/GROSOR de mallas ya existentes, ninguno toca PlateR/PlateY
            // ni characterSize:
            //  1) borde más grueso (más filo entre placa y poste/fondo real),
            //  2) el fondo se satura/limpia (blancos más blancos, colores más
            //     vivos) sin tocar los tonos del reglamento en el catálogo,
            //  3) una plancha fina del tono OPUESTO al texto, justo detrás de
            //     las letras, para que nunca se pierdan contra el fondo.
            var border = MeshObject("Borde", root, spec.Shape, PlateR * BorderScale, spec.Borde);
            border.transform.localPosition = new Vector3(0f, PlateY, 0.012f);
            var plate = MeshObject("Placa", root, spec.Shape, PlateR, BoostContrast(spec.Fondo));
            plate.transform.localPosition = new Vector3(0f, PlateY, 0f);

            // z = -0.003: delante de la Placa (0) pero DETRÁS del Tachado
            // (-0.006) y del Texto (-0.015) — la banda de prohibición sigue
            // encima, nunca tapada por la plancha.
            var backing = MeshObject("PlanchaTexto", root, spec.Shape, PlateR * BackingScale,
                BackingFor(spec.ColorTexto));
            backing.transform.localPosition = new Vector3(0f, PlateY, -0.003f);

            // Texto 3D nítido (sin texturas), mirando a -Z como la placa.
            var textGO = new GameObject("Texto");
            textGO.transform.SetParent(root, false);
            textGO.transform.localPosition = new Vector3(0f, PlateY, -0.015f);
            // Sin rotación: el TextMesh por defecto se lee desde -Z (el frente).
            var tm = textGO.AddComponent<TextMesh>();
            tm.text = spec.Texto;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // OJO: usar el material builtin de la fuente TAL CUAL. Un material
            // propio que referencie la textura del atlas dinámico corrompe el
            // level serializado del player (crash "level7 is corrupted").
            // Defecto cosmético asumido: los textos se ven a través de paredes.
            tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.fontSize = 64;
            tm.color = spec.ColorTexto;
            // Playtest 2026-07-14: la heurística "larga/corta" dejaba letras
            // FUERA del cartel ("ZONA ESCOLAR", "REDONDEL"...). Ahora el texto
            // se MIDE con la fuente real y el sastre puro (SignTextFit) da el
            // tamaño exacto que cabe en la forma. Los tamaños de siempre son
            // el TECHO (solo se encoge): no se agranda nada, que los levels
            // del player son frágiles (gotcha "level7 corrupted").
            tm.characterSize = FittedCharacterSize(tm, spec);

            // Banda diagonal de prohibición.
            if (spec.Tachada)
            {
                var bar = MeshObject("Tachado", root, SignShape.Rectangulo, PlateR,
                    new Color(0.78f, 0.12f, 0.10f));
                bar.transform.localPosition = new Vector3(0f, PlateY, -0.006f);
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                bar.transform.localScale = new Vector3(1.7f, 0.16f, 1f);
            }
        }

        /// <summary>
        /// Mide el texto REAL con la fuente (en píxeles) y pide al sastre puro
        /// el characterSize que cabe en la forma de la placa. TextMesh dibuja
        /// ≈0.1 unidades de mundo por píxel de fuente con characterSize = 1.
        /// </summary>
        private static float FittedCharacterSize(TextMesh tm, SignSpec spec)
        {
            const float UnitsPerPixel = 0.1f;
            tm.font.RequestCharactersInTexture(tm.text, tm.fontSize, tm.fontStyle);

            float widestPx = 0f, linePx = 0f;
            int lines = 1;
            foreach (char ch in tm.text)
            {
                if (ch == '\n') { widestPx = Mathf.Max(widestPx, linePx); linePx = 0f; lines++; continue; }
                if (tm.font.GetCharacterInfo(ch, out var info, tm.fontSize, tm.fontStyle))
                    linePx += info.advance;
            }
            widestPx = Mathf.Max(widestPx, linePx);

            // El techo probado de siempre — vive como constante nombrada en
            // SignTextFit (SenalTamanoTechoTests lo clava: nunca sube).
            float baseSize = lines > 1
                ? SignTextFit.CharacterSizeMaxMultiLine
                : SignTextFit.CharacterSizeMaxSingleLine;
            return SignTextFit.Fit(baseSize,
                widestPx * UnitsPerPixel,
                lines * tm.fontSize * 1.05f * UnitsPerPixel,
                SignTextFit.UsableWidth(spec.Shape, PlateR),
                SignTextFit.UsableHeight(spec.Shape, PlateR));
        }

        // ---------------- Mallas de placa ----------------

        private static GameObject MeshObject(string name, Transform root,
            SignShape shape, float radius, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = ShapeMesh(shape, radius);
            go.AddComponent<MeshRenderer>().sharedMaterial = SolidMaterial(color);
            return go;
        }

        /// <summary>Polígono de la forma pedida en el plano XY, visible por
        /// ambas caras (triángulos duplicados e invertidos).</summary>
        private static Mesh ShapeMesh(SignShape shape, float r)
        {
            Vector2[] pts;
            switch (shape)
            {
                case SignShape.Octagono: pts = RegularPolygon(8, r, 22.5f); break;
                case SignShape.Triangulo: // punta hacia abajo (el ceda del reglamento)
                    pts = new[] { new Vector2(0, -r), new Vector2(r * 0.95f, r * 0.6f),
                                  new Vector2(-r * 0.95f, r * 0.6f) };
                    break;
                case SignShape.Circulo: pts = RegularPolygon(24, r, 0f); break;
                case SignShape.Rombo: pts = new[] { new Vector2(0, r * 1.15f), new Vector2(r * 1.15f, 0),
                                                    new Vector2(0, -r * 1.15f), new Vector2(-r * 1.15f, 0) }; break;
                default: pts = new[] { new Vector2(-r * 1.1f, r * 0.65f), new Vector2(r * 1.1f, r * 0.65f),
                                       new Vector2(r * 1.1f, -r * 0.65f), new Vector2(-r * 1.1f, -r * 0.65f) }; break;
            }

            var verts = new Vector3[pts.Length + 1];
            verts[0] = Vector3.zero;
            for (int i = 0; i < pts.Length; i++) verts[i + 1] = pts[i];

            // Abanico desde el centro, por las dos caras.
            var tris = new int[pts.Length * 6];
            for (int i = 0; i < pts.Length; i++)
            {
                int next = (i + 1) % pts.Length;
                int t = i * 6;
                tris[t] = 0; tris[t + 1] = i + 1; tris[t + 2] = next + 1;     // cara +Z
                tris[t + 3] = 0; tris[t + 4] = next + 1; tris[t + 5] = i + 1; // cara -Z
            }

            var mesh = new Mesh { name = "Placa_" + shape };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2[] RegularPolygon(int sides, float r, float offsetDeg)
        {
            var pts = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (offsetDeg + i * 360f / sides) * Mathf.Deg2Rad;
                pts[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
            }
            return pts;
        }

        /// <summary>
        /// Sube el contraste de un color de FONDO sin cambiar su identidad
        /// (el reglamento manda el tono): los casi-blancos se limpian hacia
        /// blanco puro, los casi-negros se quedan (ya son el contraste
        /// máximo) y el resto se satura/aclara un poco — un rojo, azul o
        /// amarillo "más vivos" se leen mejor a distancia que el tono plano
        /// del catálogo. Nunca toca forma ni tamaño.
        /// </summary>
        private static Color BoostContrast(Color c)
        {
            float min = Mathf.Min(c.r, c.g, c.b);
            float max = Mathf.Max(c.r, c.g, c.b);
            if (min > 0.85f) return Color.Lerp(c, Color.white, 0.6f); // casi blanco → blanco limpio
            if (max < 0.2f) return c; // casi negro: ya es el contraste máximo

            Color.RGBToHSV(c, out float h, out float s, out float v);
            s = Mathf.Min(1f, s * 1.25f);
            v = Mathf.Min(1f, v * 1.1f);
            return Color.HSVToRGB(h, s, v);
        }

        /// <summary>
        /// Color de la plancha fina detrás del texto: el tono OPUESTO al de
        /// las letras (por luminancia), para que el texto resalte pase lo
        /// que pase con el fondo real. Texto claro (blanco, como PARE) →
        /// plancha casi negra; texto oscuro (negro, como los límites o
        /// CEDA) → plancha casi blanca. A propósito NO es siempre oscura
        /// (aunque el pedido original decía "plancha oscura"): una plancha
        /// oscura detrás de letras NEGRAS las taparía en vez de resaltarlas
        /// — el objetivo (que el texto nunca se pierda) manda sobre la
        /// palabra literal.
        /// </summary>
        private static Color BackingFor(Color textColor)
        {
            float luminancia = textColor.r * 0.299f + textColor.g * 0.587f + textColor.b * 0.114f;
            return luminancia > 0.5f
                ? new Color(0.05f, 0.05f, 0.06f)
                : new Color(0.97f, 0.97f, 0.95f);
        }

        private static Material SolidMaterial(Color c)
        {
            // UNLIT: los colores del reglamento se ven SIEMPRE (con Lit, la
            // cara de la placa quedaba en sombra según el sol y se veía negra).
            // Además garantiza que URP/Unlit viaje en el build (lo usa el fix
            // runtime de los textos y los focos de los semáforos).
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = c;
            return mat;
        }
    }
}
