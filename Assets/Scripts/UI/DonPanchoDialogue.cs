using System.Collections.Generic;
using UnityEngine;

namespace HablaCamaron.UI
{
    /// <summary>Eventos que disparan una línea de Don Pancho.</summary>
    public enum DialogueEvent
    {
        MissionStart, GoodShift, BadShift, StallEngine, RedLightRun,
        GoodTurn, HillStart, NearCrash, MissionPass, MissionFail, Speeding,
        MissedBlinker, OutOfBounds, Rollover, CrosswalkBlock, LapDone, WrongWay,
        GoalUnlocked
    }

    /// <summary>
    /// PANTALLA 8 — SISTEMA DE DIÁLOGOS DE DON PANCHO. Singleton.
    /// No crea UI propia: alimenta las burbujas de HUDController.ShowMessage().
    /// Cada evento tiene varias variantes; elige una al azar y encola si hay otra activa.
    /// </summary>
    public class DonPanchoDialogue : MonoBehaviour
    {
        public static DonPanchoDialogue Instance { get; private set; }

        // Hook de audio opcional por evento (para FMOD/AudioSource más adelante).
        public Dictionary<DialogueEvent, AudioClip> Clips = new();

        private readonly Dictionary<DialogueEvent, string[]> _lines = new()
        {
            { DialogueEvent.MissionStart, new[] { "\u00a1Arranque con fe, camar\u00f3n!", "Manos al volante, \u00f1a\u00f1o." } },
            { DialogueEvent.GoodShift,    new[] { "\u00a1As\u00ed se maneja en Quito!", "Eso es, mijo." } },
            { DialogueEvent.BadShift,     new[] { "Suave, suave con el embrague.", "Ah\u00ed le calaste, mijo." } },
            { DialogueEvent.StallEngine,  new[] { "\u00a1Aaay guambrita, se le apag\u00f3!", "Tranquilo, vuelva a encender." } },
            { DialogueEvent.RedLightRun,  new[] { "\u00a1Esa luz estaba en rojo, camar\u00f3n!", "Eso no se hace, \u00f1a\u00f1o." } },
            { DialogueEvent.GoodTurn,     new[] { "Buen giro, mijo.", "As\u00ed, con calma." } },
            { DialogueEvent.HillStart,    new[] { "La cuesta con paciencia, camar\u00f3n.", "Embrague justo y no rueda atr\u00e1s." } },
            { DialogueEvent.NearCrash,    new[] { "\u00a1Ojo, ojo!", "Cuidado, \u00f1a\u00f1o, casi." } },
            { DialogueEvent.MissionPass,  new[] { "\u00a1Habla, camar\u00f3n! Lo logr\u00f3.", "Bien hecho, guambra." } },
            { DialogueEvent.MissionFail,  new[] { "Toca repetir, mijo. Sin pena.", "Otra vuelta y le sale." } },
            { DialogueEvent.Speeding,     new[] { "¡Baje la pata, camarón! La señal no es de adorno.", "Más despacio, ñaño, que aquí multan." } },
            { DialogueEvent.MissedBlinker, new[] { "¡La direccional, camarón! El de atrás no adivina.", "Avise para dónde va, ñaño, que para eso está la palanca." } },
            { DialogueEvent.OutOfBounds,  new[] { "¡Por ahí no es, camarón! La ciudad se acaba aquí.", "De vuelta a la vía, ñaño, que la clase es en la calle." } },
            { DialogueEvent.Rollover,     new[] { "¡Las cuatro ruedas al piso, camarón! Así no se llega a ningún lado.", "¡Volcó el Aveo, guambra! El papá nos mata: a repetir." } },
            { DialogueEvent.CrosswalkBlock, new[] { "¡La cebra es de la gente, camarón! Pare ANTES de la raya.", "Está tapando el paso, ñaño — atrasito la próxima." } },
            { DialogueEvent.LapDone,      new[] { "¡Primera vuelta lista, camarón! Otra más, con calma.", "¡Esa es, guambra! De nuevo — y no olvide las direccionales." } },
            { DialogueEvent.WrongWay,     new[] { "¡Contravía, camarón! Ese carril es de los que vienen.", "¡A su derecha, mijo! Aquí se maneja por la derecha." } },
            { DialogueEvent.GoalUnlocked, new[] { "¡Ya se sabe el camino, camarón! Ahora sí, dele para la meta.", "Listo el recorrido, mijo — derechito a la meta que ya se puede." } },
        };

        /// <summary>Mismo evento no se repite antes de esto (que no sature).</summary>
        public const float EventCooldown = 6f;

        private readonly Queue<(string msg, float dur)> _queue = new();
        private readonly Dictionary<DialogueEvent, float> _lastFired = new();
        private bool _busy;

        private DonPanchoVoice _voice;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // La voz procedural acompaña cada burbuja (slider "Voz" de Opciones).
            _voice = gameObject.AddComponent<DonPanchoVoice>();
        }

        /// <summary>
        /// ¿Ya pasó el cooldown de un evento? PURA para probarla en EditMode.
        /// lastFired = float.NegativeInfinity si nunca sonó.
        /// </summary>
        public static bool CooldownReady(float lastFired, float now, float cooldown) =>
            now - lastFired >= cooldown;

        /// <summary>Dispara una línea aleatoria del evento. Encola si ya hay una
        /// activa; ignora repeticiones dentro del cooldown del evento.</summary>
        public void Trigger(DialogueEvent ev, float duration = 3f)
        {
            if (!_lines.TryGetValue(ev, out var variants) || variants.Length == 0) return;

            float last = _lastFired.TryGetValue(ev, out float t) ? t : float.NegativeInfinity;
            if (!CooldownReady(last, Time.unscaledTime, EventCooldown)) return;
            _lastFired[ev] = Time.unscaledTime;

            string msg = variants[Random.Range(0, variants.Length)];
            _queue.Enqueue((msg, duration));
            if (!_busy) DequeueNext();
        }

        private void DequeueNext()
        {
            if (_queue.Count == 0) { _busy = false; return; }
            _busy = true;
            var (msg, dur) = _queue.Dequeue();

            if (HUDController.Instance != null)
                HUDController.Instance.ShowMessage(msg, dur);
            _voice?.Speak(msg);

            // Libera tras la duración + el fade para no solapar burbujas.
            // Tiempo REAL (no Invoke): la cola no se congela con timeScale = 0.
            StartCoroutine(ReleaseAfter(dur + 0.5f));
        }

        private System.Collections.IEnumerator ReleaseAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            DequeueNext();
        }
    }
}
