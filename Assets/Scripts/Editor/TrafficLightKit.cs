using UnityEngine;
using HablaCamaron.World;

namespace HablaCamaron.EditorTools
{
    /// <summary>
    /// Semáforo urbano CONSTRUIDO POR CÓDIGO con primitivas URP (estética toon):
    /// poste oscuro + cabezal con tres cuencas y lámparas rojo/amarillo/verde en
    /// la posición ELEVADA correcta. Reemplaza a los postes Streetlight_3A de
    /// Toon City, cuyas lámparas hijas tienen el pivote en la BASE del prefab:
    /// el foco de 0.7 m se dibujaba EN EL PISO (bug reportado en playtest
    /// 2026-07-14). Los focos brillantes, halos y luces reales los agrega
    /// TrafficLightController en runtime (patrón de siempre); aquí solo viven
    /// los anclajes Light_Red / Light_Yellow / Light_Green con el pivote BIEN.
    /// </summary>
    public static class TrafficLightKit
    {
        // Geometría (metros): a escala de los autos toon (Car_2D mide 3.9 de alto).
        private const float PoleH = 2.7f;
        private const float HeadW = 0.55f, HeadH = 1.6f, HeadD = 0.34f;
        private const float HeadCenterY = PoleH + HeadH * 0.5f - 0.05f; // 3.45
        private const float BulbSize = 0.45f; // diámetro de foco (RegisterStreetlight)

        /// <summary>Construye el semáforo y lo registra en el controlador del ciclo.</summary>
        public static Transform Place(Transform parent, Vector3 localPos, float yaw,
            TrafficLightController controller, int group)
        {
            var root = new GameObject("Semaforo").transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var metal = Mat(new Color(0.16f, 0.17f, 0.19f)); // poste gris grafito
            var housing = Mat(new Color(0.09f, 0.09f, 0.11f)); // cabezal casi negro

            // Poste (cilindro primitivo: alto 2 × escala y). CON collider: es
            // parte física del mundo, chocar contra él cuenta como choque.
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Poste";
            pole.transform.SetParent(root, false);
            pole.transform.localPosition = new Vector3(0f, PoleH * 0.5f, 0f);
            pole.transform.localScale = new Vector3(0.16f, PoleH * 0.5f, 0.16f);
            pole.GetComponent<Renderer>().sharedMaterial = metal;

            // Cabezal (sin collider: en alto, nadie lo alcanza).
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Cabezal";
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0f, HeadCenterY, 0f);
            head.transform.localScale = new Vector3(HeadW, HeadH, HeadD);
            head.GetComponent<Renderer>().sharedMaterial = housing;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            // Tres cuencas (siempre visibles, oscuras) + anclajes de lámpara.
            // Orden del reglamento: rojo ARRIBA, amarillo al medio, verde abajo.
            (string name, float y)[] lamps =
            {
                ("Light_Red", HeadCenterY + 0.5f),
                ("Light_Yellow", HeadCenterY),
                ("Light_Green", HeadCenterY - 0.5f),
            };
            foreach (var (name, y) in lamps)
            {
                var socket = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                socket.name = "Cuenca";
                socket.transform.SetParent(root, false);
                socket.transform.localPosition = new Vector3(0f, y, -HeadD * 0.25f);
                socket.transform.localScale = Vector3.one * (BulbSize + 0.06f);
                socket.GetComponent<Renderer>().sharedMaterial = housing;
                Object.DestroyImmediate(socket.GetComponent<Collider>());

                // El anclaje con el PIVOTE en su sitio: el controlador le cuelga
                // foco, halo y luz real en runtime y lo prende/apaga con el ciclo.
                var lamp = new GameObject(name);
                lamp.transform.SetParent(root, false);
                lamp.transform.localPosition = new Vector3(0f, y, -HeadD * 0.35f);
            }

            controller.RegisterStreetlight(root, group, BulbSize);
            return root;
        }

        private static Material Mat(Color c) =>
            new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
    }
}
