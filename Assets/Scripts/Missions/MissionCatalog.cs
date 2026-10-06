using HablaCamaron.UI;

namespace HablaCamaron.Missions
{
    /// <summary>
    /// Cómo elige la meta el MissionRunner. Varias misiones comparten zona,
    /// así que la meta no puede ser siempre "el nodo más lejano".
    /// </summary>
    public enum GoalMode
    {
        /// <summary>[MetaExamen] si la escena lo trae; si no, el nodo más lejano.</summary>
        MetaOrFarthest,
        /// <summary>El semáforo más cercano al punto de partida (la entrada al redondel).</summary>
        RoundaboutEntry,
        /// <summary>Volver al punto de partida (se "arma" recién al alejarse).</summary>
        ReturnToStart,
        /// <summary>Volver, pero la meta queda A UN LADO del inicio (playtest:
        /// la baliza encima del spawn confundía). También se arma al alejarse.</summary>
        ReturnAside,
        /// <summary>La meta es un ancla CON NOMBRE de la escena (MissionDef.GoalAnchor).
        /// Así una misma ciudad grande aloja varias misiones con rutas distintas.</summary>
        NamedAnchor,
    }

    /// <summary>Definición de una misión de campaña.</summary>
    public class MissionDef
    {
        public int Id;
        public string Title;
        public string SceneName;      // escena de zona que la aloja
        public float TimeLimit;       // segundos
        public int TrafficDensity;    // NPCs simultáneos
        public int Laps = 1;          // vueltas (solo metas de "volver"; C2 usa 2)
        public string GoalAnchor;     // nombre del ancla de meta (GoalMode.NamedAnchor)
        public bool NightMood;        // la ciudad se pone nocturna para esta misión
        public bool IsFinal;          // el examen: usa las pantallas de final
        public bool StrictRules;      // muerte súbita: rojo/contravía/salirse = reprobado al instante
        public bool RouteLocked;      // hay que seguir el camino de la misión (RouteCorridor)
        public bool Checkpoints;      // la meta nace apagada hasta pasarlos todos en orden
        public GoalMode Goal = GoalMode.MetaOrFarthest;
        public MissionBriefingData Briefing;
        public MishelMessage[] ChatAfter; // chat de Mishel al aprobar (null = ninguno)
    }

    /// <summary>
    /// Las SIETE misiones del GDD (T1-T2 tutorial, C1-C4 conducción, F1 examen),
    /// en código para que el flujo completo funcione sin armar assets. El mapa
    /// de campaña, el briefing, la evaluación y los finales leen de aquí.
    /// Varias misiones comparten escena: el id activo viaja en la clave
    /// PlayerPrefs "hc_current_mission" (la escribe el mapa y los reintentos).
    /// </summary>
    public static class MissionCatalog
    {
        /// <summary>Clave PlayerPrefs con el id de la misión que se va a jugar.</summary>
        public const string KEY_CURRENT = "hc_current_mission";
        /// <summary>Clave PlayerPrefs con el chat de Mishel pendiente de mostrar.</summary>
        public const string KEY_PENDING_CHAT = "hc_pending_chat";

        public static readonly MissionDef[] All =
        {
            // ---------------- Categoría 1: Tutorial ----------------
            new MissionDef
            {
                Id = 0,
                Title = "Sacar el Aveo",
                SceneName = "N1_ZonaSur",
                TimeLimit = 150f,
                TrafficDensity = 0, // tutorial EN CALMA: nadie te choca aprendiendo embrague
                Goal = GoalMode.RoundaboutEntry,
                Briefing = new MissionBriefingData
                {
                    title = "Tutorial — Sacar el Aveo",
                    zone = "Quitumbe / Guamaní",
                    moment = "Mañana",
                    donPanchoLine = "Primero lo primero, camarón: saque el Aveo del garaje " +
                                    "sin rayarle nada al papá. Embrague, primera, y despacito " +
                                    "hasta el semáforo del redondel.",
                    objectives = new[]
                    {
                        "Saca el auto del garaje sin chocar las paredes",
                        "No cales el motor (suelta Shift DESPACIO)",
                        "Llega al semáforo de entrada al redondel",
                    },
                },
                ChatAfter = new[]
                {
                    new MishelMessage("¡¿Estás aprendiendo a manejar?! 😱", false, "09:40"),
                    new MishelMessage("Hoy saqué el Aveo del garaje yo solito 😎", true, "09:42"),
                    new MishelMessage("Jajaja un metro entero, qué campeón 😂", false, "09:42"),
                },
            },
            new MissionDef
            {
                Id = 1,
                Title = "La vuelta a la manzana",
                SceneName = "N1_ZonaSur",
                TimeLimit = 260f,
                TrafficDensity = 2, // poco tráfico: recién estás soltando el embrague
                Goal = GoalMode.ReturnAside, // la baliza queda calle abajo, no en la casa
                Briefing = new MissionBriefingData
                {
                    title = "Tutorial — La vuelta a la manzana",
                    zone = "Quitumbe / Guamaní",
                    moment = "Mañana",
                    donPanchoLine = "Ahora sí, una vuelta al barrio: cruce el redondel, pasee " +
                                    "el Aveo y regrese a la casa. Marchas uno-dos-tres y ojo " +
                                    "con los semáforos, que aquí sí multan.",
                    objectives = new[]
                    {
                        "Aléjate del barrio cruzando el redondel",
                        "Usa las marchas 1ª a 3ª con embrague",
                        "Regresa a la baliza de la calle del garaje (se enciende al volver)",
                    },
                },
                ChatAfter = new[]
                {
                    new MishelMessage("¿Y hoy qué te enseñó Don Pancho?", false, "11:03"),
                    new MishelMessage("Toda la vuelta a la manzana sin calar 🚗", true, "11:05"),
                    new MishelMessage("Ya casi chofer profesional 😏", false, "11:05"),
                },
            },

            // ---------------- Categoría 2: Conducción ----------------
            new MissionDef
            {
                Id = 2,
                Title = "La cuesta de Guamaní",
                // LA CUESTA SE JUEGA DONDE HAY CUESTA DE VERDAD. Estuvo en la
                // ciudad Toon, pero ahí el ÚNICO relieve (10 m) es la autopista
                // elevada: la ruta metía al jugador por la zona industrial,
                // entre tuberías y sin calles (playtest 2026-07-25, con foto).
                // La Zona Sur tiene la cuesta construida a propósito —4 rampas
                // crecientes y un semáforo a media pendiente para practicar el
                // arranque en cuesta— y es, literalmente, Guamaní. Medido:
                // sube 7.9 m en 290 m con pendiente máxima de 9°.
                SceneName = "N1_ZonaSur",
                TimeLimit = 300f,
                TrafficDensity = 4,
                StrictRules = true,
                // La meta (la cima) nace apagada: playtest — "la meta del nivel
                // se podía tocar desde el arranque". La arman checkpoints
                // invisibles repartidos en la ruta A* spawn→cima, en orden.
                Checkpoints = true,
                Goal = GoalMode.MetaOrFarthest, // la cima del brazo norte
                Briefing = new MissionBriefingData
                {
                    title = "Conducción — La cuesta de Guamaní",
                    zone = "Quitumbe / Guamaní",
                    moment = "Mediodía",
                    donPanchoLine = "Hoy toca la cuesta, mijo, que es donde se ve al chofer. " +
                                    "Suba sin calarse hasta arriba; si le toca parar en la " +
                                    "pendiente: freno de mano, embrague al punto y sin rodar atrás.",
                    objectives = new[]
                    {
                        "Sube la cuesta del barrio hasta la cima",
                        "Si te detienes en la pendiente: freno de mano y embrague al punto",
                        "Respeta el semáforo de media cuesta",
                    },
                },
                ChatAfter = new[]
                {
                    new MishelMessage("Don Pancho me subió a la cuesta 😰", true, "13:21"),
                    new MishelMessage("¿¡La cuesta!? ¿Y sobreviviste? 😂", false, "13:24"),
                    new MishelMessage("Freno de mano y fe, dice el maestro 🙏", true, "13:24"),
                },
            },
            new MissionDef
            {
                Id = 3,
                Title = "El redondel",
                SceneName = "N1_CiudadToon", // nivel 4: la ciudad grande
                // Misma vuelta que T2 pero con tráfico y DOS VUELTAS (niveles
                // finales más largos): el reto es la convivencia vial repetida.
                // El timer en 0 es fallo duro, así que nunca menos tiempo que T2.
                TimeLimit = 440f,
                TrafficDensity = 6,
                StrictRules = true,
                // Desde el nivel 4 hay que seguir el CAMINO de la misión: la
                // ciudad es enorme y sin esto el nivel era pasear a gusto
                // (playtest: "debe dejar ir solo por donde puede").
                RouteLocked = true,
                Laps = 2, // la baliza se apaga al tocarla y pide otra vuelta
                // La meta ES el redondel de la ciudad: dar la vuelta ahí es lo
                // intuitivo (pedido del playtest), no un callejón sin salida.
                Goal = GoalMode.NamedAnchor,
                GoalAnchor = "Meta_Redondel",
                Briefing = new MissionBriefingData
                {
                    title = "Conducción — El redondel",
                    zone = "Quito — la ciudad",
                    moment = "Tarde",
                    donPanchoLine = "El redondel de la ciudad a hora ocupada, guambra: " +
                                    "direccional antes de entrar, ceda el paso al que ya " +
                                    "circula, y nada de meterse a lo taxista. DOS vueltas.",
                    objectives = new[]
                    {
                        "Llega al redondel y DA DOS VUELTAS a su alrededor",
                        "Usa las direccionales (Q/E) al entrar y salir",
                        "Respeta los semáforos y no pares sobre las cebras",
                    },
                },
                ChatAfter = new[]
                {
                    new MishelMessage("¿Cómo va el curso de manejo? 🚙", false, "16:48"),
                    new MishelMessage("Hoy dominé el redondel en hora pico 💪", true, "16:50"),
                    new MishelMessage("Entonces ya puedes venir a verme 👀", false, "16:51"),
                },
            },
            new MissionDef
            {
                Id = 4,
                Title = "La Simón de noche",
                // Nivel 5: la avenida Simón Bolívar, que ES una CARRETERA de
                // verdad (guardavías, postes escasos, peaje) y ya se genera
                // nocturna. El playtest pidió que cada nivel cumpla su promesa:
                // el de la cuesta que suba y el de la Simón que sea carretera.
                SceneName = "N1_SimonBolivar",
                TimeLimit = 420f,
                TrafficDensity = 4, // de noche la avenida es solitaria (GDD)
                StrictRules = true,
                RouteLocked = true,
                NightMood = false,  // la escena YA nace de noche (SetupNightAmbience)
                Goal = GoalMode.MetaOrFarthest, // la avenida de punta a punta
                Briefing = new MissionBriefingData
                {
                    title = "Conducción — La Simón de noche",
                    zone = "Av. Simón Bolívar",
                    moment = "Noche",
                    donPanchoLine = "La Simón de noche es otra cosa, guambra: carretera " +
                                    "abierta, poca luz y curvas largas. Luces cortas en lo " +
                                    "lento, LARGAS en lo oscuro (tecla L), y respete el límite.",
                    objectives = new[]
                    {
                        "Recorre la avenida de punta a punta",
                        "Usa las luces: cortas y largas según el tramo",
                        "Respeta el límite de 90 y no te salgas de la vía",
                    },
                },
                ChatAfter = new[]
                {
                    new MishelMessage("¿Manejaste de NOCHE por la Simón? 😨", false, "21:47"),
                    new MishelMessage("Con luces largas y todo. Ya casi estoy listo 🚗", true, "21:50"),
                    new MishelMessage("El sábado es el concierto... ¿me recoges? 🎶", false, "21:51"),
                },
            },
            new MissionDef
            {
                Id = 5,
                Title = "Hora pico",
                SceneName = "N1_CiudadToon", // nivel 6: la ciudad grande a tope
                TimeLimit = 440f,
                TrafficDensity = 10, // el tráfico ES la misión
                StrictRules = true,
                RouteLocked = true,
                Goal = GoalMode.NamedAnchor,
                GoalAnchor = "Meta_HoraPico",
                Briefing = new MissionBriefingData
                {
                    title = "Conducción — Hora pico",
                    zone = "Quito — la ciudad",
                    moment = "Atardecer",
                    donPanchoLine = "Esto ya es Quito de verdad, mijo: busetas que paran donde " +
                                    "sea y taxistas con apuro. Distancia con el de adelante, " +
                                    "paciencia, y cruzamos la ciudad enteros.",
                    objectives = new[]
                    {
                        "Cruza la ciudad entre el tráfico más denso del juego",
                        "Mantén distancia: sin choques (conducción defensiva)",
                        "Respeta los semáforos aunque los demás no lo hagan",
                    },
                },
                ChatAfter = new[]
                {
                    new MishelMessage("Sobreviví a la hora pico 🚗🚌🚕", true, "18:32"),
                    new MishelMessage("¡Eso ya es nivel Quito! 🏆", false, "18:33"),
                    new MishelMessage("Mañana es el examen... deséame suerte 🤞", true, "18:34"),
                },
            },

            // ---------------- Categoría 3: Examen final ----------------
            new MissionDef
            {
                Id = 6,
                Title = "¡Habla, Camarón!",
                SceneName = "N1_CiudadToon", // el examen cruza LA CIUDAD ENTERA
                TimeLimit = 480f, // la ruta más larga del juego (medida: 988 m urbanos)
                TrafficDensity = 8, // hora pico, pero un examen limpio (playtest)
                StrictRules = true,
                RouteLocked = true,
                IsFinal = true,
                Goal = GoalMode.MetaOrFarthest, // [MetaExamen]
                Briefing = new MissionBriefingData
                {
                    title = "EXAMEN FINAL — ¡Habla, Camarón!",
                    zone = "Quito — la ciudad completa",
                    moment = "Atardecer",
                    donPanchoLine = "Llegó la hora, mijo: la ciudad ENTERA de punta a punta. " +
                                    "Mishel espera bajo el arco y el concierto no espera a nadie. " +
                                    "Semáforos, cebras, cruces... todo lo que aprendió, junto.",
                    objectives = new[]
                    {
                        "Cruza la ciudad hasta el arco antes de que acabe el tiempo",
                        "Respeta semáforos y NO te detengas sobre las cebras",
                        "Sin choques graves: el Aveo es del papá",
                    },
                },
                ChatAfter = null, // el final lo cuentan las pantallas de cierre
            },

            // ---------------- Bonus post-examen: la ciudad completa ----------------
            // Pedido del playtest: "usar TODO el mapa". Se desbloquea al aprobar
            // el examen (RecordMissionResult abre la 8 al pasar la 7).
            new MissionDef
            {
                Id = 7,
                Title = "Quito entero",
                SceneName = "N2_QuitoCiudad",
                TimeLimit = 460f,
                TrafficDensity = 10, // techo de la regla de densidades (arreglos-playtest)
                StrictRules = true,
                RouteLocked = true,
                Goal = GoalMode.MetaOrFarthest, // cruzar la ciudad de punta a punta
                Briefing = new MissionBriefingData
                {
                    title = "BONUS — Quito entero",
                    zone = "La ciudad completa",
                    moment = "Atardecer",
                    donPanchoLine = "Ya es chofer con todas las de la ley, camarón. Le tengo " +
                                    "el premio: la ciudad ENTERA, cuadra por cuadra. Crúcela " +
                                    "de punta a punta sin contravías y sin subirse a la vereda.",
                    objectives = new[]
                    {
                        "Cruza la ciudad completa hasta la baliza",
                        "Solo por las calles y en tu carril (aquí sí se nota)",
                        "Semáforos de verdad en cada intersección del centro",
                    },
                },
                ChatAfter = new[]
                {
                    new MishelMessage("¿Y ahora por dónde andas? 👀", false, "19:10"),
                    new MishelMessage("Crucé TODO Quito manejando 🌆🚗", true, "19:14"),
                    new MishelMessage("Chofer oficial de la casa 😍", false, "19:15"),
                },
            },
        };

        public static MissionDef Get(int id) =>
            (id >= 0 && id < All.Length) ? All[id] : null;

        /// <summary>Escena que aloja una misión (para el mapa y el botón Reintentar).</summary>
        public static string SceneFor(int id) => Get(id)?.SceneName;

        /// <summary>La primera misión que aloja una escena, o null (manejo libre).</summary>
        public static MissionDef ForScene(string sceneName)
        {
            foreach (var m in All)
                if (m.SceneName == sceneName) return m;
            return null;
        }

        /// <summary>
        /// La misión a dirigir en una escena: si preferredId es válido Y vive en
        /// esa escena, manda (varias misiones comparten zona); si no, la primera
        /// de la escena. Pura para poder probarla en EditMode.
        /// </summary>
        public static MissionDef ForScene(string sceneName, int preferredId)
        {
            var preferred = Get(preferredId);
            if (preferred != null && preferred.SceneName == sceneName) return preferred;
            return ForScene(sceneName);
        }

        /// <summary>El examen final (la única misión con IsFinal).</summary>
        public static MissionDef Final
        {
            get
            {
                foreach (var m in All)
                    if (m.IsFinal) return m;
                return All[All.Length - 1];
            }
        }
    }
}
