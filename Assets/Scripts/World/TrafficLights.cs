using System;
using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.World
{
    public enum LightState { Green, Yellow, Red }

    /// <summary>
    /// Lógica PURA del ciclo semafórico con dos grupos alternados (por ejemplo,
    /// entradas norte/sur vs este/oeste de un redondel). Estática y sin estado:
    /// el estado se deriva del tiempo → determinista y fácil de probar.
    /// Ciclo de un grupo: Verde → Amarillo → Rojo (mientras el otro pasa por
    /// su verde+amarillo) → un colchón en rojo para ambos (allRed) → repite.
    /// </summary>
    public static class TrafficLightCycle
    {
        /// <param name="group">0 o 1 (grupos alternados).</param>
        /// <param name="time">Tiempo transcurrido (Time.time).</param>
        public static LightState GetState(int group, float time,
            float green = 8f, float yellow = 2f, float allRed = 1f)
        {
            float half = green + yellow + allRed;   // media vuelta del ciclo
            float cycle = half * 2f;
            // El grupo 1 vive media vuelta desfasado del grupo 0.
            float t = Mathf.Repeat(time - (group == 0 ? 0f : half), cycle);

            if (t < green) return LightState.Green;
            if (t < green + yellow) return LightState.Yellow;
            return LightState.Red;
        }
    }

    /// <summary>
    /// Qué focos van ENCENDIDOS para cada estado del ciclo. PURA (testeada):
    /// con foco amarillo propio (semáforo construido por código) cada color es
    /// exclusivo; los postes viejos de Toon City solo tienen verde y rojo, así
    /// que el amarillo se muestra con AMBOS prendidos (comportamiento clásico).
    /// </summary>
    public static class TrafficLampFaces
    {
        public static void For(LightState state, bool hasYellow,
            out bool green, out bool yellow, out bool red)
        {
            if (hasYellow)
            {
                green = state == LightState.Green;
                yellow = state == LightState.Yellow;
                red = state == LightState.Red;
            }
            else
            {
                green = state != LightState.Red;
                yellow = false;
                red = state != LightState.Green;
            }
        }
    }

    // TrafficLightController vive en su propio archivo (TrafficLightController.cs,
    // Tarea 9 2026-07-24): compartir archivo con estas clases hacía que Unity
    // resolviera la clase PRINCIPAL del asset como TrafficLightCycle (la
    // primera declarada) y NINGUNA escena podía guardar una referencia por
    // GUID al componente — ver el comentario al inicio de ese archivo.
}
