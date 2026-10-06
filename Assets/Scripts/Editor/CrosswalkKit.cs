using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Pinta PASOS CEBRA por código (arreglo de playtest: "no veo los pasos
    /// cebra"): franjas blancas unlit apenas sobre el asfalto, cruzando la
    /// vía, con su componente Crosswalk (grupo de semáforo) para la regla de
    /// tránsito de PlayerInfractions. Los builders lo llaman junto a cada
    /// semáforo — la cebra vive donde la gente cruza: en la esquina.
    /// </summary>
    public static class CrosswalkKit
    {
        private const float StripeDepth = 0.55f; // fondo de cada franja (m)
        private const float StripeGap = 0.5f;    // espacio entre franjas

        /// <summary>
        /// Cebra centrada en un punto del MUNDO, cruzando una vía de ancho
        /// roadWidth cuyo eje avanza según yaw (grados). El grupo es el del
        /// semáforo que la gobierna. La altura se mide por raycast.
        /// </summary>
        public static Crosswalk Place(Transform parent, Vector3 worldCenter, float yawDeg,
            float roadWidth, int lightGroup)
        {
            Physics.SyncTransforms();
            float y = worldCenter.y;
            if (Physics.Raycast(worldCenter + Vector3.up * 25f, Vector3.down, out var hit, 60f))
                y = hit.point.y;

            var root = new GameObject("PasoCebra");
            root.transform.SetParent(parent, true);
            root.transform.position = new Vector3(worldCenter.x, y, worldCenter.z);
            root.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            { color = new Color(0.93f, 0.93f, 0.90f) };

            // Franjas a lo ancho de la vía (x local), avanzando en z local.
            int stripes = Mathf.Max(4, Mathf.FloorToInt(roadWidth / (StripeDepth + StripeGap)));
            float span = stripes * (StripeDepth + StripeGap) - StripeGap;
            for (int i = 0; i < stripes; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"Franja_{i}";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(root.transform, false);
                quad.transform.localPosition = new Vector3(
                    -span * 0.5f + i * (StripeDepth + StripeGap) + StripeDepth * 0.5f,
                    0.035f, 0f); // apenas sobre el asfalto (sin z-fighting)
                quad.transform.localRotation = Quaternion.Euler(90f, 90f, 0f);
                quad.transform.localScale = new Vector3(3.2f, StripeDepth, 1f);
                quad.GetComponent<Renderer>().sharedMaterial = mat;
            }

            var cw = root.AddComponent<Crosswalk>();
            cw.LightGroup = lightGroup;
            cw.HalfExtents = new Vector3(span * 0.5f + 0.4f, 2f, 1.9f);
            return cw;
        }
    }
}
