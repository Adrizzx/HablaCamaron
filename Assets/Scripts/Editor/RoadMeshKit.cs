using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Generador de MALLAS de vía (editor): cintas continuas y bordillos que
    /// siguen una polilínea 3D. Modela avenidas suaves SIN las costuras ni las
    /// grietas que dejan las baldosas prefabricadas de Toon City al girar en
    /// curva. Se usa cuando el asset no da una vía decente: aquí la modelamos.
    ///
    /// Convención: cinta plana con la normal hacia +Y (superficie transitable);
    /// el "derecha" de cada punto se calcula con un inglete suave entre tramos
    /// para que los carriles no se pisen en las curvas.
    /// </summary>
    public static class RoadMeshKit
    {
        /// <summary>Vector "derecha" horizontal por punto (inglete suave entre tramos).</summary>
        public static List<Vector3> ComputeRights(IReadOnlyList<Vector3> pts)
        {
            var rights = new List<Vector3>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 prev = pts[Mathf.Max(0, i - 1)];
                Vector3 next = pts[Mathf.Min(pts.Count - 1, i + 1)];
                Vector3 tan = next - prev;
                tan.y = 0f;                       // el peralte lo da la Y de los puntos
                if (tan.sqrMagnitude < 1e-6f) tan = Vector3.forward;
                tan.Normalize();
                rights.Add(Vector3.Cross(Vector3.up, tan)); // +X cuando avanza +Z
            }
            return rights;
        }

        /// <summary>
        /// Cinta plana entre dos offsets laterales (asfalto, arcén, líneas de
        /// carril). Triángulos con normal hacia arriba; UV.v = distancia recorrida.
        /// </summary>
        public static GameObject BuildRibbon(string name, Transform parent,
            IReadOnlyList<Vector3> pts, IReadOnlyList<Vector3> rights,
            float leftOffset, float rightOffset, float yLift, Material mat, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            int n = pts.Count;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new int[(n - 1) * 6];
            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) dist += Vector3.Distance(pts[i], pts[i - 1]);
                Vector3 lift = Vector3.up * yLift;
                verts[i * 2] = pts[i] + rights[i] * leftOffset + lift;
                verts[i * 2 + 1] = pts[i] + rights[i] * rightOffset + lift;
                uvs[i * 2] = new Vector2(0f, dist);
                uvs[i * 2 + 1] = new Vector2(1f, dist);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int t = i * 6, a = i * 2;
                tris[t] = a; tris[t + 1] = a + 2; tris[t + 2] = a + 1;      // normal +Y
                tris[t + 3] = a + 1; tris[t + 4] = a + 2; tris[t + 5] = a + 3;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>
        /// Bordillo / parapeto / parterre central: cubos estirados tramo a tramo
        /// a lo largo de la polilínea (normales correctas por todas las caras,
        /// sin acertijos de winding). Sigue la pendiente al inclinar cada cubo.
        /// </summary>
        public static GameObject BuildCurb(string name, Transform parent,
            IReadOnlyList<Vector3> pts, IReadOnlyList<Vector3> rights,
            float centerOffset, float width, float height, Material mat, bool collider)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i] + rights[i] * centerOffset;
                Vector3 b = pts[i + 1] + rights[i + 1] * centerOffset;
                Vector3 mid = (a + b) * 0.5f;
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len < 1e-4f) continue;

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Seg";
                cube.transform.SetParent(root, false);
                cube.transform.position = mid + Vector3.up * (height * 0.5f);
                cube.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                cube.transform.localScale = new Vector3(width, height, len + 0.05f);
                cube.GetComponent<Renderer>().sharedMaterial = mat;
                if (!collider) Object.DestroyImmediate(cube.GetComponent<Collider>());
            }
            return root.gameObject;
        }

        /// <summary>Marcas discontinuas (línea de carril a trazos) sobre la cinta.</summary>
        public static GameObject BuildDashes(string name, Transform parent,
            IReadOnlyList<Vector3> pts, IReadOnlyList<Vector3> rights,
            float centerOffset, float halfWidth, float yLift, float dash, float gap, Material mat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                // Un trazo por tramo si el patrón dash/gap lo permite (tramos ~cortos).
                if ((i % 2) != 0) continue;
                Vector3 a = pts[i] + rights[i] * centerOffset + Vector3.up * yLift;
                Vector3 b = pts[i + 1] + rights[i + 1] * centerOffset + Vector3.up * yLift;
                Vector3 dir = (b - a);
                float len = dir.magnitude;
                if (len < 1e-4f) continue;
                Vector3 fwd = dir.normalized;
                Vector3 mid = (a + b) * 0.5f;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Trazo";
                quad.transform.SetParent(root, false);
                quad.transform.position = mid;
                quad.transform.rotation = Quaternion.LookRotation(Vector3.down, fwd);
                quad.transform.localScale = new Vector3(halfWidth * 2f, len * (dash / (dash + gap)), 1f);
                quad.GetComponent<Renderer>().sharedMaterial = mat;
                Object.DestroyImmediate(quad.GetComponent<Collider>());
            }
            return root.gameObject;
        }

        public static Material SolidMat(Color color, float smoothness = 0.1f)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }
    }
}
