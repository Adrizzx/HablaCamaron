using UnityEngine;

namespace HablaCamaron.Vehicle
{
    /// <summary>
    /// Ficha técnica de un vehículo. Aveo y BT-50 usan los MISMOS scripts,
    /// solo cambia este asset (misma filosofía que UITheme: datos centralizados).
    /// Crear desde: Assets > Create > Habla Camarón > Vehículo.
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleSpec", menuName = "Habla Camarón/Vehículo")]
    public class VehicleSpec : ScriptableObject
    {
        [Header("Identidad")]
        public string DisplayName = "Chevrolet Aveo";

        [Header("Chasis")]
        public float Mass = 1080f;               // kg
        public Vector3 CenterOfMass = new Vector3(0f, 0.45f, 0.1f); // bajo = estable

        [Header("Motor")]
        public float IdleRpm = 850f;
        public float MaxRpm = 6500f;
        public float StallRpm = 550f;            // debajo de esto con embrague suelto → se cala
        public float MaxTorque = 145f;           // Nm en el pico
        public float EngineResponse = 3200f;      // rpm/s que sube o baja el motor
        [Tooltip("Torque relativo (0-1) según RPM normalizadas (0-1). Bajos con " +
                 "cuerpo (0.40/0.70): el arranque en pendiente es exigente pero " +
                 "justo — ajuste del playtest 2026-07-14.")]
        public AnimationCurve TorqueCurve = new AnimationCurve(
            new Keyframe(0.00f, 0.40f),
            new Keyframe(0.15f, 0.70f),
            new Keyframe(0.45f, 1.00f),
            new Keyframe(0.80f, 0.88f),
            new Keyframe(1.00f, 0.55f));

        [Header("Transmisión (manual)")]
        public float[] GearRatios = { 3.55f, 1.95f, 1.32f, 0.97f, 0.77f }; // 1ª..5ª
        public float ReverseRatio = 3.75f;
        public float FinalDrive = 4.18f;
        public float DrivetrainEfficiency = 0.88f;

        [Header("Embrague (mecánica central del juego)")]
        [Tooltip("Qué tan rápido baja el pedal al presionar Shift (pedal/seg).")]
        public float ClutchPressSpeed = 8f;
        [Tooltip("Qué tan rápido sube el pedal al soltar Shift. Bajo = perdonador.")]
        public float ClutchReleaseSpeed = 1.35f;
        [Tooltip("Punto de fricción: desde aquí el embrague empieza a 'morder'.")]
        [Range(0.2f, 0.8f)] public float BitePoint = 0.45f;

        [Header("Dirección")]
        public float MaxSteerAngle = 33f;         // grados a baja velocidad
        public float SteerAngleAtSpeed = 12f;     // grados a alta velocidad (estabilidad en teclado)
        public float SteerSpeedKmhRef = 90f;      // km/h donde se alcanza el ángulo mínimo
        public float SteerLerpSpeed = 5f;         // suavizado del volante

        [Header("Frenos")]
        public float BrakeTorque = 2800f;         // Nm total, repartido a las 4
        public float HandbrakeTorque = 5000f;     // solo ruedas traseras
        public float EngineBrake = 70f;           // retención con marcha puesta y sin acelerar

        [Header("Suspensión (para el armado automático)")]
        public float SuspensionDistance = 0.22f;
        public float SpringForce = 34000f;
        public float SpringDamper = 3800f;
    }
}
