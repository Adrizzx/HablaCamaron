using System.Collections.Generic;
using UnityEngine;
using HablaCamaron.Vehicle;

namespace HablaCamaron.UI
{
    /// <summary>
    /// Las FOTOS de los autos del garaje (`Assets/Resources/Portraits/`,
    /// importadas como Sprite). Mismo trato que PortraitLibrary: se cargan una
    /// sola vez y se cachean; si el asset falta, TryPhoto devuelve false y la
    /// tarjeta cae al glifo dibujado de siempre (la foto es un lujo, no una
    /// dependencia dura).
    /// </summary>
    public static class VehiclePortraits
    {
        private static readonly Dictionary<int, string> Rutas = new Dictionary<int, string>
        {
            { VehicleRoster.AveoId, "Portraits/TAXI_chevrolet_aveo" },
            { VehicleRoster.Bt50Id, "Portraits/mazda_bt" },
        };

        private static readonly Dictionary<int, Sprite> _cache = new Dictionary<int, Sprite>();

        /// <summary>Ruta dentro de Resources; null si el id no está en el roster.</summary>
        public static string ResourcePathFor(int vehicleId) =>
            Rutas.TryGetValue(vehicleId, out var ruta) ? ruta : null;

        public static bool TryPhoto(int vehicleId, out Sprite foto)
        {
            if (!_cache.TryGetValue(vehicleId, out foto))
            {
                var ruta = ResourcePathFor(vehicleId);
                foto = ruta == null ? null : Resources.Load<Sprite>(ruta);
                _cache[vehicleId] = foto;
            }
            return foto != null;
        }
    }
}
