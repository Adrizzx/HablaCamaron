using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>
    /// Sentido de giro de un anillo de nodos (PURO, testeado en EditMode).
    ///
    /// EL BUG QUE ESTO IMPIDE (playtest 2026-07-30, con foto): la plaza de
    /// retorno de la cima del nivel 3 generaba sus nodos con
    /// `(x, z) = (sin a, cos a)` en vez de `(cos a, sin a)`. Las dos recorren un
    /// círculo, pero la primera es la versión ESPEJADA: el anillo quedaba en
    /// sentido HORARIO. Como las flechas doradas del asfalto y el minimapa se
    /// dibujan sobre la ruta A*, heredaban el error y mandaban al jugador a
    /// rodear el redondel por el lado contrario al del tránsito real.
    /// El comentario del código decía "antihorario" — por eso no se veía
    /// leyendo: hay que MEDIRLO.
    ///
    /// Ecuador conduce por la derecha, así que los redondeles se circulan en
    /// sentido ANTIHORARIO visto desde arriba (la isla queda a la izquierda del
    /// conductor).
    /// </summary>
    public static class RingOrientation
    {
        /// <summary>
        /// Área con signo del polígono en el plano XZ (fórmula del cordón de
        /// zapato). Positiva = antihorario visto desde arriba, con X a la
        /// derecha y Z hacia el norte. El valor absoluto es el área real, así
        /// que también sirve para detectar un "anillo" degenerado.
        /// </summary>
        public static float AreaConSigno(IReadOnlyList<Vector3> puntos)
        {
            if (puntos == null || puntos.Count < 3) return 0f;

            float suma = 0f;
            for (int i = 0; i < puntos.Count; i++)
            {
                Vector3 a = puntos[i];
                Vector3 b = puntos[(i + 1) % puntos.Count];
                suma += a.x * b.z - b.x * a.z;
            }
            return suma * 0.5f;
        }

        /// <summary>
        /// ¿Este anillo se recorre como debe (antihorario)? Un anillo
        /// degenerado (menos de 3 puntos, o todos alineados) devuelve false:
        /// en la duda, que salte el invariante y lo mire alguien.
        /// </summary>
        public static bool EsAntihorario(IReadOnlyList<Vector3> puntos) => AreaConSigno(puntos) > 0.001f;
    }
}
