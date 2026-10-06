namespace HablaCamaron.AI
{
    /// <summary>Estados de la Máquina de Estados Finitos del conductor NPC.</summary>
    public enum NpcState { Cruising, Following, Braking, Yielding, Overtaking, DoubleParked, Crashed }

    /// <summary>Lo que el NPC "ve" en un instante (lo llena NpcDriver con raycasts).</summary>
    public struct NpcSensors
    {
        public float AheadDistance;      // metros al vehículo de adelante (MaxValue = nada)
        public float AheadClosingSpeed;  // m/s a los que NOS ACERCAMOS a eso (0 = no cierra)
        public bool RedLightAhead;       // hay semáforo en rojo/amarillo en la ruta próxima
        public float LightDistance;      // metros a ese semáforo
        public bool YieldAhead;          // señal de ceda con tráfico cercano en el cruce
        public bool PlayerNear;          // el JUGADOR está en la burbuja defensiva
        public bool PlayerStalemate;     // llevamos demasiado parados por él y no se aparta
        public bool HasPriorityOverBlocker; // ambos parados y YO tengo prioridad (anti-deadlock)
        public bool AheadIsStatic;       // el de adelante está DETENIDO (doble fila / choque)
        public bool LeftLaneClear;       // el carril izquierdo está libre para adelantar

        // --- Percepción MUTUA (no el cono frontal: cualquier rumbo) ---
        // Los llena NpcDriver.Sense() con NpcConflictMath sobre el registro de
        // NPC vivos. Sin esto, en un cruce o un redondel el otro auto llega de
        // COSTADO, fuera de los bigotes, y como los NPC son cinemáticos tampoco
        // hay colisión física que lo salve: se traspasan.
        public float NeighborTimeToCollision; // s hasta el encuentro (MaxValue = sin conflicto)
        public bool NeighborIsOnMyRight;      // ese vecino viene por mi derecha
        public bool NeighborHasPriority;      // ...y llega ANTES al cruce: me toca ceder
    }

    public struct NpcDecision
    {
        public NpcState State;
        public float TargetSpeed;      // m/s
    }

    /// <summary>
    /// El CEREBRO del NPC: función PURA sensores + perfil → decisión.
    /// Toda la lógica de transición de la FSM vive aquí, sin Unity, para poder
    /// probarla en EditMode caso por caso. NpcDriver solo ejecuta lo decidido.
    /// Prioridades (de mayor a menor): choque inminente → semáforo → seguir al
    /// de adelante → ceda el paso → crucero.
    /// </summary>
    public static class NpcBrain
    {
        /// <summary>Tiempo-a-colisión crítico: por debajo, frenada de emergencia.</summary>
        public const float CriticalTtc = 1.4f;
        /// <summary>TTC de confort: por debajo, seguir al de adelante en vez de cerrar.</summary>
        public const float ComfortTtc = 3f;
        /// <summary>Metros antes del semáforo donde el objetivo de frenado ya
        /// es 0 EN SECO (no una asíntota): garantiza un PARE real, no un
        /// reptar infinito hacia la línea.</summary>
        public const float StopMargin = 4f;

        /// <summary>Por debajo de este tiempo-a-colisión con un vecino que
        /// tiene prioridad, se cede el paso. Más alto haría a los NPC
        /// pararse por conflictos que se resuelven solos.</summary>
        public const float ConflictoTtc = 3f;

        /// <summary>Hueco temporal que se mantiene con el de adelante (s). El
        /// clásico "dos segundos" de autoescuela, algo apretado por lo urbano.</summary>
        public const float Headway = 1.5f;

        /// <param name="obeysThisLight">Decidido al acercarse (perfil + azar):
        /// el taxista a veces se pasa el rojo — pero se decide UNA vez, no por frame.</param>
        public static NpcDecision Decide(in NpcSensors s, DriverProfile p, bool obeysThisLight)
        {
            // 1) Colisión inminente: MUY cerca, o cerrando tan rápido que el
            //    tiempo-a-colisión (distancia / velocidad de cierre) es crítico.
            //    El TTC es la técnica estándar: frena por FÍSICA, no por metros.
            float ttc = s.AheadClosingSpeed > 0.1f
                ? s.AheadDistance / s.AheadClosingSpeed : float.MaxValue;
            if (s.AheadDistance < p.MinGap * 0.6f || ttc < CriticalTtc)
                return new NpcDecision { State = NpcState.Braking, TargetSpeed = 0f };

            // 2) El JUGADOR en la burbuja defensiva: DETENERSE DEL TODO y
            //    esperar a que se quite (le pita, no lo empuja). Quien está
            //    aprendiendo JAMÁS es embestido por el tráfico — regla de oro.
            //    PERO si el jugador no se aparta (se quedó calado, o está
            //    mirando el mapa), quedarse clavado para siempre TAPA la calle
            //    y el nivel se vuelve injugable — playtest: "los NPC se quedan
            //    bloqueados". Pasado ese punto se reanuda al PASO, que sigue
            //    siendo seguro: el frenazo por choque inminente de la regla 1
            //    manda sobre esta y lo detiene antes de tocarlo.
            //    OJO — el estado es Cruising A PROPÓSITO, aunque el desvío
            //    lateral de NpcDriver.Move() solo se aplique en Overtaking.
            //    Se probó devolver Overtaking aquí para que "rodee" de verdad
            //    y salió PEOR, medido: el desvío es de ~3.2 m, y dentro del
            //    redondel estrecho de la Zona Sur eso deja dos autos
            //    físicamente superpuestos y circulando por el carril contrario
            //    (lo cazaron NpcRedondelDiagTests y NpcCarrilDiagTests). Aquí
            //    se reanuda AL PASO y de frente; si de verdad queda clavado,
            //    el anti-bloqueo (replanificar/reciclar) es quien lo resuelve.
            if (s.PlayerNear)
                return s.PlayerStalemate
                    ? new NpcDecision { State = NpcState.Cruising, TargetSpeed = p.CruiseSpeed * 0.25f }
                    : new NpcDecision { State = NpcState.Braking, TargetSpeed = 0f };

            // 3) Semáforo en rojo dentro de la distancia de frenado (si lo respeta).
            if (s.RedLightAhead && obeysThisLight && s.LightDistance < p.LightBrakeDistance)
            {
                // Frena proporcional, PERO con margen de seguridad (StopMargin)
                // donde el objetivo es 0 EN SECO, no una aproximación cada vez
                // más lenta: el objetivo original solo llegaba a 0 exactamente
                // AL LLEGAR al nodo (asíntota), así que el auto nunca terminaba
                // de parar — seguía reptando hacia la línea para siempre y
                // terminaba cruzándola en rojo, sin importar el perfil ni su
                // obediencia (SemaforosNpcDiagTests medía esto hasta con el
                // Particular 100% obediente). Con el objetivo en 0 desde
                // StopMargin metros antes, el auto de verdad se detiene con
                // margen — y el rate de frenado de emergencia (target ≤ 0.01)
                // ya se activa ahí, no solo pegado a la línea.
                // El invariante esperado es LightBrakeDistance > StopMargin
                // (la zona proporcional debe tener ancho positivo); un perfil
                // mal configurado con LightBrakeDistance <= StopMargin daría
                // un denominador cero o negativo (división por cero → NaN si
                // encima el numerador también es 0). Mathf.Max con un piso
                // chico evita el NaN; el numerador queda negativo en ese caso
                // (LightDistance < LightBrakeDistance <= StopMargin), así que
                // Clamp01 igual resuelve t = 0 — PARE total, el resultado
                // seguro para un perfil sin margen real de frenado.
                float ancho = UnityEngine.Mathf.Max(0.01f, p.LightBrakeDistance - StopMargin);
                float t = UnityEngine.Mathf.Clamp01((s.LightDistance - StopMargin) / ancho);
                return new NpcDecision
                {
                    State = NpcState.Braking,
                    TargetSpeed = p.CruiseSpeed * t * 0.5f
                };
            }

            // PROBADO Y REVERTIDO (2026-07-30) — CEDER EL PASO POR PRIORIDAD
            // EN EL PUNTO DE CRUCE. Aquí iba la regla: conflicto a menos de
            // ConflictoTtc segundos con un vecino que llega antes al cruce ⇒
            // Yielding. La regla en sí es CORRECTA y sigue probada (ver
            // NpcConflictMath.DeboCeder y su test de antisimetría, más el test
            // PlayMode que confirma que dos NPC reales nunca ceden los dos ni
            // ninguno). Lo que falló fue el efecto de conjunto, medido con
            // TraficoBoletinTests en 4+4 corridas:
            //   · contravía en la Zona Sur: 5/3/5/9 (antes) → 14/10/10/7.
            //   · bloqueados >10 s:         5/6/1/3        → 15/10/8/4.
            //   · superpuestos:             sin mejorar.
            // Se intentó UN arreglo dirigido —eximir el ceda del anti-atasco,
            // igual que ya se hace con el semáforo, con tope de paciencia— y
            // NO bastó: 18/7/10/17 en contravía. Mecanismo: el que cede se
            // para en mitad del cruce, la escalera de desatasco lo mueve de
            // sitio (retroceso → esquive) y termina fuera de su carril.
            // Para retomarlo haría falta primero que el que cede se detenga
            // ANTES de entrar al cruce (una línea de parada real sobre el
            // grafo), no donde le pille el conflicto. Los sensores
            // NeighborTimeToCollision / NeighborIsOnMyRight / NeighborHasPriority
            // se conservan rellenos y testeados para ese trabajo futuro.

            // 4) Vehículo adelante dentro del colchón (por metros O por TTC de
            //    confort): seguirlo a distancia sin cerrar la brecha.
            if (s.AheadDistance < p.MinGap * 2.5f || ttc < ComfortTtc)
            {
                // 4a) ADELANTAMIENTO seguro: si lo de adelante está DETENIDO
                //     (doble fila, choque) y el carril izquierdo está libre,
                //     lo rodea en vez de quedarse atascado eternamente.
                if (s.AheadIsStatic && s.LeftLaneClear)
                    return new NpcDecision { State = NpcState.Overtaking, TargetSpeed = p.CruiseSpeed * 0.6f };

                // 4b) DESEMPATE anti-deadlock: dos parados que se estorban
                //     mutuamente (ceda/seguir). El que tiene prioridad reanuda
                //     crucero lento y deshace el atasco en <1 s (el otro lo
                //     sigue) — sin esperar el reciclado por timeout.
                if (s.HasPriorityOverBlocker)
                    return new NpcDecision { State = NpcState.Cruising, TargetSpeed = p.CruiseSpeed * 0.4f };

                // 4c) PROBADO Y REVERTIDO (2026-07-30) — SEGUIMIENTO POR HUECO
                //     TEMPORAL. Aquí estuvo `(AheadDistance - MinGap)/Headway`,
                //     el car-following clásico. Sobre el papel es mejor que la
                //     fórmula de abajo (atada a la distancia) y mata el
                //     acordeón; medido, empeoró la Zona Sur y hubo que
                //     quitarlo: contravía 5/3/5/9 → 14/12 y bloqueados 5/6/1/3
                //     → 14/9 (medido con el Paso 3 ya revertido, así que el
                //     efecto es de ESTA fórmula).
                //     Mecanismo: el objetivo llega a 0 en MinGap (5 m) en vez
                //     de en MinGap·0.6 (3 m) y es más bajo en toda la ventana,
                //     así que el NPC se detiene MÁS a menudo; cada parada
                //     cuenta como bloqueo, dispara la escalera de desatasco
                //     (retroceso → esquive) y eso lo saca del carril.
                //     Para retomarlo hay que arreglar antes el anti-atasco:
                //     detenerse en una cola NO puede contar como estar
                //     bloqueado. Es el mismo requisito que dejó pendiente la
                //     regla de prioridad del cruce.
                float t = (s.AheadDistance - p.MinGap * 0.6f) / (p.MinGap * 1.9f);
                return new NpcDecision
                {
                    State = NpcState.Following,
                    TargetSpeed = p.CruiseSpeed * UnityEngine.Mathf.Clamp01(t)
                };
            }

            // 5) Ceda el paso con tráfico en el cruce: detenerse.
            if (s.YieldAhead)
                return new NpcDecision { State = NpcState.Yielding, TargetSpeed = 0f };

            // 6) Vía libre: crucero del perfil.
            return new NpcDecision { State = NpcState.Cruising, TargetSpeed = p.CruiseSpeed };
        }
    }
}
