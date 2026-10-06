using UnityEngine;
using HablaCamaron.Core;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// La flota del juego (Fase 4): el Aveo del papá (siempre disponible) y la
    /// Mazda BT-50 (se desbloquea aprobando "La Simón de noche" — C3 del GDD).
    /// Mismo patrón que DriverProfile: presets en código, mismos scripts,
    /// datos distintos. La selección se guarda en PlayerPrefs "hc_vehicle" y
    /// MissionSystemBootstrap re-equipa el auto de la escena al cargar.
    /// </summary>
    public static class VehicleRoster
    {
        public const string KEY_SELECTED = "hc_vehicle";

        public const int AveoId = 0;
        public const int Bt50Id = 1;

        /// <summary>Id de la misión del catálogo que desbloquea el BT-50
        /// (C3 "La Simón de noche"). Aprobar = puntaje ≥ 70.</summary>
        public const int Bt50UnlockMissionId = 4;

        /// <summary>¿Está disponible este vehículo con el progreso dado? PURA
        /// (GameData es una clase simple) para probarla en EditMode.</summary>
        public static bool IsUnlocked(int vehicleId, GameData data)
        {
            if (vehicleId == AveoId) return true; // el carro escuela, siempre
            if (vehicleId != Bt50Id) return false;
            return data != null &&
                   Bt50UnlockMissionId < data.MissionScores.Length &&
                   data.MissionScores[Bt50UnlockMissionId] >= 70;
        }

        /// <summary>El vehículo elegido en el garaje, validado contra el
        /// progreso: si el guardado apunta a uno bloqueado (o basura), Aveo.</summary>
        public static int SelectedId(GameData data)
        {
            int id = PlayerPrefs.GetInt(KEY_SELECTED, AveoId);
            return IsUnlocked(id, data) ? id : AveoId;
        }

        /// <summary>
        /// La ficha técnica de cada vehículo, construida en código (mismo
        /// patrón que DriverProfile). El Aveo son los valores por defecto de
        /// VehicleSpec (los afinados en la Fase 0); la BT-50 es pesada, con
        /// más torque pero motor más lento y embrague menos perdonador.
        /// </summary>
        public static VehicleSpec CreateSpec(int vehicleId)
        {
            var spec = ScriptableObject.CreateInstance<VehicleSpec>();
            if (vehicleId != Bt50Id) return spec; // defaults = Aveo 2008

            spec.DisplayName = "Mazda BT-50";
            spec.Mass = 1850f;                       // camioneta: casi el doble
            spec.CenterOfMass = new Vector3(0f, 0.55f, 0.05f);
            spec.IdleRpm = 800f;
            spec.MaxRpm = 5200f;                     // motor de trabajo, no revienta
            spec.StallRpm = 600f;                    // cala más fácil que el Aveo
            spec.MaxTorque = 270f;                   // pero empuja mucho más
            spec.EngineResponse = 2600f;             // sube de vueltas más lento
            spec.GearRatios = new[] { 4.0f, 2.32f, 1.52f, 1.0f, 0.79f };
            spec.ReverseRatio = 4.2f;
            spec.FinalDrive = 4.1f;
            spec.DrivetrainEfficiency = 0.86f;
            spec.ClutchReleaseSpeed = 1.25f;         // pedal algo más exigente
            spec.BitePoint = 0.5f;
            spec.MaxSteerAngle = 30f;                // dirección más pesada
            spec.SteerAngleAtSpeed = 10f;
            spec.SteerLerpSpeed = 4f;
            spec.BrakeTorque = 3800f;                // frenos para el peso
            spec.HandbrakeTorque = 7000f;
            spec.EngineBrake = 130f;
            spec.SuspensionDistance = 0.26f;         // suspensión de camioneta
            spec.SpringForce = 56000f;
            spec.SpringDamper = 5600f;
            return spec;
        }

        /// <summary>
        /// Re-equipa el auto de la escena con el vehículo elegido (lo llama
        /// MissionSystemBootstrap al cargar). Las escenas se generan con el
        /// Aveo; si el jugador eligió el BT-50 desbloqueado, se le aplica su
        /// ficha en caliente (masa, suspensión y todo).
        /// </summary>
        public static void ApplySelectionTo(VehicleController car)
        {
            if (car == null) return;
            var data = GameManager.Instance != null ? GameManager.Instance.Data : null;
            int id = SelectedId(data);
            if (id == AveoId) return; // la escena ya trae el Aveo
            car.ApplySpec(CreateSpec(id));
        }
    }
}
