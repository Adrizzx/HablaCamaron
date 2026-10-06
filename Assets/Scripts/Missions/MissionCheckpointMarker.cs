using System;
using UnityEngine;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// Checkpoint sembrado sobre la ruta: una burbuja de trigger que avisa al
    /// MissionRunner cuando el jugador la cruza. Quién cuenta y en qué orden lo
    /// decide MissionCheckpoints (puro); esto solo detecta el paso.
    ///
    /// LLEVA MARCA VISIBLE (playtest: "no se ven los checkpoints"). Antes era
    /// un trigger pelado, así que en el nivel 3 el jugador tenía que cruzar
    /// 3-6 burbujas INVISIBLES en orden para que se encendiera la meta, sin
    /// nada en pantalla que dijera dónde estaban. Ahora el que toca cruzar se
    /// ve: un aro dorado en el asfalto y una columna de luz que asoma por
    /// encima de las casas. Solo se muestra el SIGUIENTE (MissionRunner lo
    /// gobierna): enseñarlos todos a la vez convierte la cuesta en una feria.
    /// </summary>
    public class MissionCheckpointMarker : MonoBehaviour
    {
        /// <summary>Alto de la columna (m): tiene que verse sobre los techos.</summary>
        private const float AltoColumna = 26f;

        public int Indice { get; private set; }
        private Transform _player;
        private Action<int> _alPasar;
        private bool _yaAviso;
        private GameObject _visual;

        public static MissionCheckpointMarker Crear(Transform padre, Vector3 pos,
            int indice, Transform player, Action<int> alPasar)
        {
            var go = new GameObject($"Checkpoint_{indice}");
            go.transform.SetParent(padre, false);
            go.transform.position = pos;

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = MissionCheckpoints.RadioMetros;

            var m = go.AddComponent<MissionCheckpointMarker>();
            m.Indice = indice;
            m._player = player;
            m._alPasar = alPasar;
            m.ConstruirVisual();
            m.MostrarVisual(false);
            return m;
        }

        /// <summary>Aro en el piso + columna de luz, ambos SIN collider (son
        /// adorno: el único volumen que cuenta es el trigger de la raíz).</summary>
        private void ConstruirVisual()
        {
            _visual = new GameObject("Marca");
            _visual.transform.SetParent(transform, false);

            var mat = MaterialDorado();

            // Aro a ras de suelo: dice DÓNDE hay que pasar.
            var aro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            aro.name = "Aro";
            aro.transform.SetParent(_visual.transform, false);
            aro.transform.localPosition = Vector3.zero;
            // OJO: un cilindro primitivo mide 2 de alto, así que la escala Y es
            // la MITAD del grosor. Y su collider de cápsula, aplastado, sería
            // una esfera invisible gigante (gotcha del proyecto): por eso se
            // destruye — aquí no hace ninguna falta.
            aro.transform.localScale = new Vector3(MissionCheckpoints.RadioMetros * 1.4f,
                                                   0.06f,
                                                   MissionCheckpoints.RadioMetros * 1.4f);
            Destroy(aro.GetComponent<Collider>());
            aro.GetComponent<Renderer>().material = mat;

            // Columna: se ve de lejos, por encima de casas y muros.
            var columna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            columna.name = "Columna";
            columna.transform.SetParent(_visual.transform, false);
            columna.transform.localPosition = Vector3.up * AltoColumna * 0.5f;
            columna.transform.localScale = new Vector3(3.2f, AltoColumna * 0.5f, 3.2f);
            Destroy(columna.GetComponent<Collider>());
            columna.GetComponent<Renderer>().material = mat;
        }

        /// <summary>Dorado translúcido. El combo de transparencia va COMPLETO:
        /// con solo `_Surface` y `renderQueue` (sin blend ni ZWrite) URP lo
        /// pinta SÓLIDO — así fue como la baliza de meta acabó siendo una
        /// pared naranja que tapaba media pantalla.</summary>
        private static Material MaterialDorado()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(1f, 0.78f, 0.30f, 0.38f) };
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        /// <summary>Enciende o apaga la marca (solo el siguiente se ve).</summary>
        public void MostrarVisual(bool visible)
        {
            if (_visual != null) _visual.SetActive(visible);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_yaAviso || _player == null) return;
            if (other.transform.root != _player.root) return; // solo el jugador
            _yaAviso = true;
            _alPasar?.Invoke(Indice);
        }
    }
}
