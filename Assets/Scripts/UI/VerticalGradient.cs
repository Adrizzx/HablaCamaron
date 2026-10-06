using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HablaCamaron.UI
{
    /// <summary>
    /// Tinta los vértices del mesh de un Graphic con un gradiente vertical de 2 colores.
    /// Da gradientes reales a botones y tarjetas sin necesidad de sprites.
    /// Pon el componente en el mismo GameObject que la Image y llama SetColors().
    /// </summary>
    [AddComponentMenu("UI/Effects/Vertical Gradient (Habla Camaron)")]
    public class VerticalGradient : BaseMeshEffect
    {
        [SerializeField] private Color _top = Color.white;
        [SerializeField] private Color _bottom = Color.black;

        public void SetColors(Color top, Color bottom)
        {
            _top = top;
            _bottom = bottom;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            var verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);

            // Rango Y del mesh para normalizar.
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < verts.Count; i++)
            {
                float y = verts[i].position.y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            float height = Mathf.Max(0.0001f, maxY - minY);
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                float t = (v.position.y - minY) / height; // 0 abajo, 1 arriba
                v.color = Color.Lerp(_bottom, _top, t);
                verts[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }
}
