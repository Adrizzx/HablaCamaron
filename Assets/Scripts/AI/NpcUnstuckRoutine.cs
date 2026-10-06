namespace HablaCamaron.AI
{
    /// <summary>Qué hace el NPC para destrabarse en este frame.</summary>
    public enum UnstuckMove { Nada, Retroceso, Esquive, Replanificar, Reciclar }

    /// <summary>
    /// Rutina de desatasco PURA (pedido del dueño: los NPC trabados NO se
    /// destruyen, se destraban). Escalera por tiempo parado: esperar (puede
    /// ser un semáforo), retroceder un poco, esquivar dentro del carril,
    /// replanificar, y recién a los 30 s reciclar como salvavidas.
    /// </summary>
    public sealed class NpcUnstuckRoutine
    {
        public const float EsperaSegundos = 3f;
        public const float RetrocesoHasta = 4.5f;
        public const float EsquiveHasta = 6f;
        public const float ReplanificaEn = 6f;
        public const float ReciclaEn = 30f;
        private const float CicloSegundos = 3f; // alterna retroceso/esquive tras replanificar

        // Techo duro de deriva lateral (finding de revisión, ronda 1): sin
        // esto, un atasco largo en un cruce con mucho tráfico (Hora pico,
        // 12 NPCs) repite bloques de Esquive cada 6 s hasta el reciclado a
        // los 30 s y puede arrastrar al NPC ~14-16 m fuera de su carril.
        // Medio carril de margen, nunca más.
        public const float MaxDerivaMetros = 2.5f;

        // Debe calzar con el desplazamiento lateral REAL que aplica
        // NpcDriver en el caso Esquive (misma constante en ambos lados: la
        // rutina es la única fuente de verdad de "cuánto se mueve" un
        // Esquive, así el techo que ella calcula no puede desincronizarse
        // del movimiento real que aplica el driver).
        public const float EsquiveMetrosPorSegundo = 1.2f;

        private float _bloqueado;
        private bool _replanifico;
        private float _derivaAcumulada;

        public float TiempoBloqueado => _bloqueado;

        /// <summary>Deriva lateral acumulada del episodio de atasco actual (m).</summary>
        public float DerivaAcumulada => _derivaAcumulada;

        public UnstuckMove Tick(bool bloqueado, float dt)
        {
            if (!bloqueado)
            {
                _bloqueado = 0f;
                _replanifico = false;
                _derivaAcumulada = 0f;
                return UnstuckMove.Nada;
            }

            float antes = _bloqueado;
            _bloqueado += dt;

            if (_bloqueado >= ReciclaEn) return UnstuckMove.Reciclar;
            if (_bloqueado < EsperaSegundos) return UnstuckMove.Nada;
            if (_bloqueado < RetrocesoHasta) return UnstuckMove.Retroceso;
            if (_bloqueado < EsquiveHasta) return Esquivar(dt);

            if (!_replanifico && antes < ReplanificaEn)
            {
                _replanifico = true;
                return UnstuckMove.Replanificar;
            }

            // Sigue trabado: alterna maniobras hasta el salvavidas (Esquivar
            // ya se niega sola a seguir arrastrando una vez lleno el cupo).
            float fase = (_bloqueado - EsquiveHasta) % (CicloSegundos * 2f);
            return fase < CicloSegundos ? UnstuckMove.Retroceso : Esquivar(dt);
        }

        /// <summary>
        /// Emite Esquive mientras quede cupo de deriva lateral; agotado el
        /// techo, se comporta como si no hubiera Esquive disponible y cae a
        /// Retroceso (que no desplaza al NPC fuera de su carril) — así el
        /// NPC nunca deja de intentar destrabarse, solo deja de derivar.
        /// </summary>
        private UnstuckMove Esquivar(float dt)
        {
            if (_derivaAcumulada >= MaxDerivaMetros) return UnstuckMove.Retroceso;
            float restante = MaxDerivaMetros - _derivaAcumulada;
            _derivaAcumulada += UnityEngine.Mathf.Min(restante, EsquiveMetrosPorSegundo * dt);
            return UnstuckMove.Esquive;
        }
    }
}
