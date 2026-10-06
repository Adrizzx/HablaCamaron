using System;
using UnityEngine;

namespace HablaCamaron.World
{
    /// <summary>Forma de la placa (el lenguaje visual del reglamento).</summary>
    public enum SignShape { Octagono, Triangulo, Circulo, Rombo, Rectangulo }

    /// <summary>Una señal ecuatoriana como DATO: el kit del editor la dibuja
    /// (placa + texto) y las funcionales llevan su SignType para el grafo.</summary>
    [Serializable]
    public class SignSpec
    {
        public string Key;        // id estable ("pare", "lim30"...)
        public string Texto;      // lo que dice la placa (\n = multilínea)
        public string Leyenda;    // nombre completo (documentación y tests)
        public SignShape Shape;
        public Color Fondo, Borde, ColorTexto;
        public SignType Efecto = SignType.None; // None = decorativa
        public bool Tachada;      // banda diagonal roja (prohibición)
    }

    /// <summary>
    /// Señalética ecuatoriana (Fase 4): ≥20 señales del reglamento INEN con
    /// identidad quiteña (el Trole, el "no pitar"). Catálogo PURO y testeado;
    /// EcuadorSignKit (editor) las construye por código en las zonas.
    /// Colores del reglamento: reglamentarias blanco/rojo, preventivas
    /// amarillo/negro, informativas azul/blanco.
    /// </summary>
    public static class EcuadorSignCatalog
    {
        private static readonly Color Rojo = new Color(0.78f, 0.12f, 0.10f);
        private static readonly Color Blanco = new Color(0.96f, 0.96f, 0.94f);
        private static readonly Color Negro = new Color(0.10f, 0.10f, 0.10f);
        private static readonly Color Amarillo = new Color(0.98f, 0.78f, 0.12f);
        private static readonly Color Azul = new Color(0.10f, 0.30f, 0.65f);

        public static readonly SignSpec[] All =
        {
            // ---- Reglamentarias (blanco/rojo) ----
            R("pare", "PARE", "Pare", SignShape.Octagono, Rojo, Blanco, Blanco, SignType.Stop),
            R("ceda", "CEDA\nEL PASO", "Ceda el paso", SignShape.Triangulo, Blanco, Rojo, Negro, SignType.Yield),

            // Los límites de velocidad ecuatorianos (R4-1 del RTE INEN 004-1)
            // son RECTANGULARES, blancos con orla negra y llevan la leyenda
            // "MÁXIMA" arriba y "km/h" abajo. El círculo blanco con anillo rojo
            // es la señal EUROPEA (Convención de Viena): Ecuador sigue el
            // modelo interamericano. Estaban dibujadas como círculos rojos.
            R("lim30", "MÁXIMA\n30\nkm/h", "Límite 30 km/h", SignShape.Rectangulo,
              Blanco, Negro, Negro, SignType.SpeedLimit30),
            R("lim50", "MÁXIMA\n50\nkm/h", "Límite 50 km/h", SignShape.Rectangulo,
              Blanco, Negro, Negro, SignType.SpeedLimit50),
            R("lim90", "MÁXIMA\n90\nkm/h", "Límite 90 km/h", SignShape.Rectangulo,
              Blanco, Negro, Negro, SignType.SpeedLimit90),

            T("no_estacionar", "E", "No estacionar", SignShape.Circulo),
            T("no_pitar", "NO\nPITAR", "Prohibido usar la bocina (¡en Quito!)", SignShape.Circulo),
            T("no_rebasar", "NO\nREBASAR", "No rebasar", SignShape.Circulo),
            T("no_giro_u", "U", "Prohibido el giro en U", SignShape.Circulo),
            R("no_entre", "NO\nENTRE", "Contravía / no entre", SignShape.Circulo, Rojo, Blanco, Blanco, SignType.None),
            R("una_via", "UNA VÍA  →", "Una vía", SignShape.Rectangulo, Negro, Blanco, Blanco, SignType.None),

            // ---- Preventivas (amarillo/negro) ----
            P("curva", "CURVA", "Curva peligrosa"),
            P("doble_via", "← DOBLE →", "Doble vía"),
            P("redondel", "REDONDEL", "Aproximación a redondel"),
            P("resalto", "RESALTO", "Reductor de velocidad (chapa acostada)"),
            P("zona_escolar", "ZONA\nESCOLAR", "Zona escolar"),
            P("peatones", "PEATONES", "Cruce de peatones"),
            P("ninos", "NIÑOS", "Niños jugando"),
            P("semaforo", "SEMÁFORO", "Semáforo adelante"),

            // ---- Informativas (azul/blanco) ----
            I("parada_bus", "BUS", "Parada de bus"),
            I("parada_trole", "TROLE", "Parada del Trole (identidad quiteña)"),
            I("hospital", "H", "Hospital cercano"),
        };

        public static SignSpec Get(string key)
        {
            foreach (var s in All)
                if (s.Key == key) return s;
            return null;
        }

        // Fábricas cortas por familia (mantienen los colores del reglamento).
        private static SignSpec R(string key, string texto, string leyenda, SignShape shape,
            Color fondo, Color borde, Color txt, SignType efecto) => new SignSpec
        { Key = key, Texto = texto, Leyenda = leyenda, Shape = shape, Fondo = fondo, Borde = borde, ColorTexto = txt, Efecto = efecto };

        private static SignSpec T(string key, string texto, string leyenda, SignShape shape) => new SignSpec
        { Key = key, Texto = texto, Leyenda = leyenda, Shape = shape, Fondo = Blanco, Borde = Rojo, ColorTexto = Negro, Tachada = true };

        private static SignSpec P(string key, string texto, string leyenda) => new SignSpec
        { Key = key, Texto = texto, Leyenda = leyenda, Shape = SignShape.Rombo, Fondo = Amarillo, Borde = Negro, ColorTexto = Negro };

        private static SignSpec I(string key, string texto, string leyenda) => new SignSpec
        { Key = key, Texto = texto, Leyenda = leyenda, Shape = SignShape.Rectangulo, Fondo = Azul, Borde = Blanco, ColorTexto = Blanco };
    }
}
