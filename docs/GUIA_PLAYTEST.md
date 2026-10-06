# Guía de playtest — ¡Habla, Camarón!

Guía nivel por nivel para el playtest humano: cómo se DISEÑÓ que se pase cada
misión, con tiempos ideales y técnica esperada. Compárala con tu experiencia
real: donde el juego se sienta distinto a lo escrito aquí, hay un hallazgo de
balance que anotar (usa CHECKLIST.md para los ítems técnicos).

## La base de todo: el ritual de arranque

Toda misión empieza con el motor apagado. La secuencia que enseña el juego:

1. **Shift** (mantener) — pisa el embrague a fondo.
2. **F** — enciende el motor (sin embrague no arranca en marcha).
3. **1** — mete primera con el embrague pisado (sin él: rechinido, −2 técnica).
4. **W** un toque — sube el RPM un poco sobre el ralentí (~1500).
5. **Suelta Shift DESPACIO** — el pedal simulado sube con inercia; si lo
   sueltas de golpe bajo ~550 RPM, el motor se cala (−3 técnica). El cluster
   de pedales del HUD marca el **punto de fricción**: ahí es donde el auto
   empieza a empujar.

El `StartupTutor` te guía paso a paso la primera vez. **H** muestra la ayuda
de controles en cualquier momento.

## La fórmula del puntaje (qué te descuenta qué)

| Criterio | Puntos | Pierdes por |
|---|---|---|
| Objetivo | 30 | No llegar a la meta (imposible aprobar sin llegar) |
| Señales | 20 | −4 por rojo pasado o exceso junto a señal de límite |
| Técnica | 20 | −3 por calada, −2 por rechinido de caja |
| Defensiva | 15 | −4 por choque, −2 por giro franco sin direccional |
| Tiempo | 10 | Llegar (el timer en 0 = misión fallida) |
| Vehículo | 5 | −2 por choque |

Se aprueba con **≥70**: llegar a la meta ya da 40, así que tienes ~30 puntos
de margen de error. El "¡Habla, camarón!" (≥90) exige una vuelta casi limpia.
Direccionales: **Q/E hasta 3 s ANTES del giro** cuenta como avisar; curvas
suaves no penalizan; tras un giro hay 5 s de tregua (el redondel no ametralla).

**Reprobación INMEDIATA (arreglos de playtest):** salirte de la calle (>10 m
de toda vía por 2 s) o volcar el auto (1.5 s de lado/de techo) termina la
misión al instante — evaluación reprobada y a repetir el nivel. Don Pancho
avisa el motivo. Los brincos de un segundo no cuentan (el juez perdona).

**Pasos cebra (niveles Toon City):** junto a cada semáforo hay una cebra
pintada. Detenerte ENCIMA de ella con el semáforo en rojo por más de 1.5 s
= bloquear el paso de la gente: **−2 en señales** por estadía y reclamo de
Don Pancho. Para SIEMPRE antes de la raya.

---

## T1 — Sacar el Aveo (Zona Sur · 150 s · sin tráfico)

**El diseño:** misión de embrague puro, en calma total. Nadie te choca.

- Ritual de arranque dentro del garaje. Sal RECTO y despacio: las paredes
  del garaje cuentan como choque (−4 defensiva, −2 vehículo).
- Primera marcha todo el trayecto está bien; el semáforo del redondel está
  al final del brazo sur (~80 m).
- OJO: el brazo sur tiene **límite 30** señalizado — en 1ª/2ª ni te acercas.
- **Tiempo ideal: 60–90 s.** Si los 150 s te aprietan, algo anda mal (anótalo).
- Puntaje esperado en el primer intento real: 85–100 (una calada es normal).

## T2 — La vuelta a la manzana (Zona Sur · 240 s · 3 NPCs)

**El diseño:** primera navegación completa + caja de cambios 1ª→3ª.

- La meta es VOLVER al barrio, pero **la baliza ya no está en tu casa**: queda
  calle abajo, a un lado del garaje (arreglo de playtest — inicio ≠ meta). Se
  arma recién al alejarte 60 m (el HUD dice "ALÉJATE DEL BARRIO" hasta
  entonces; la guía te lleva primero al punto más lejano y luego de regreso).
- Cruza el redondel en sentido **antihorario** (es dirigido: contravía no hay).
- Practica 1ª→2ª→3ª: embrague ANTES del número (el cambio en espera perdona
  pulsar casi a la vez, pero no al revés).
- Con 3 NPCs apenas verás tráfico; si uno te estorba, espera — jamás te embisten.
- **Tiempo ideal: 100–140 s.** Direccionales Q/E en cada esquina desde ya:
  es el hábito que C2 evalúa en serio.

> **CAMBIO GRANDE (2026-07-21):** del **nivel 3 en adelante se juega en la
> CIUDAD COMPLETA de Toon City** (`N1_CiudadToon`). Cada nivel recorre una
> ruta larga distinta del mismo mapa enorme, con su meta propia: la loma más
> alta (nivel 3), **el redondel de la ciudad** (nivel 4, dos vueltas), el
> cruce nocturno (nivel 5), la hora pico (nivel 6) y el examen de punta a
> punta (nivel 7). Los tiempos de abajo que digan "Zona Sur / Simón /
> Corredor" quedaron obsoletos para esos niveles.

## C1 — La cuesta de Guamaní (Zona Sur · 240 s · 4 NPCs)

**El diseño:** el arranque en pendiente, "el examen de fuego de todo quiteño".
*(Arreglos de playtest: la cuesta ahora EXIGE — 4 rampas cada vez más
empinadas. El semáforo vive en la PRIMERA rampa: parada y arranque en
pendiente obligados. La "pared" final (~16°) se pasa entrando CON IMPULSO
en 1ª — si te detienes ahí, retrocede hasta la rampa anterior y toma
carrerilla. OJO: a fondo en 1ª las ruedas PATINAN — el Aveo es tracción
delantera y el peso se va atrás; acelerador dosificado, no a fondo.)*

- La cuesta es el brazo norte (4 rampas crecientes). La meta son los barriles
  de la cima.
- **El semáforo de la primera rampa te VA a agarrar en rojo** (alterna con el
  de la base): esa parada en pendiente es la misión. La técnica de abajo no
  es opcional.
- En la pared final baja a 1ª ANTES de entrar y llega CON IMPULSO (la 2ª ya
  no tiene fuerza y parado ahí no arranca ni el instructor); dosifica el
  acelerador — a fondo patinas y resbalas hacia atrás.
- **La técnica que se evalúa:** si te detienes a media cuesta —
  1. **Espacio** (freno de mano ON) — el auto no rueda atrás.
  2. Shift + 1ª + gas hasta ~2000 RPM.
  3. Suelta Shift hasta el punto de fricción (el cluster lo marca) — el auto
     "quiere" avanzar.
  4. **Espacio** (freno de mano OFF) y termina de soltar suave.
- Rodar hacia atrás no descuenta puntos por sí mismo, pero acaba en calada o
  choque. El semáforo de media cuesta te hace practicar la técnica sí o sí.
- **Tiempo ideal: 110–160 s** (con la parada del semáforo incluida). La parte
  baja va cómoda en 2ª; la pared, en 1ª.

## C2 — El redondel (Zona Sur · 330 s · 7 NPCs · DOS VUELTAS)

**El diseño:** convivencia vial. Misma vuelta que T2 pero con tráfico real y
direccionales evaluadas. *(Ajustado en esta auditoría: antes 200 s — con
semáforos que DEBES esperar y timer de fallo duro, era un castigo al paciente.
Arreglos de playtest: densidad 10 → 7 — con 10 el barrio chico se atascaba —
y la meta del regreso queda a un lado del inicio, como en T2.)*

- **E antes de entrar al redondel, Q/E antes de cada salida.** Girar franco
  sin avisar = −2 y reclamo de Don Pancho.
- El semáforo del redondel alterna N/S vs E/O: si lo agarras en rojo, son
  ~20-25 s de espera. Está PRESUPUESTADO en los 240 s — no te lo saltes (−4).
- Los taxistas (25% del tráfico) a veces se pasan el rojo: NO los imites y
  no arranques en verde sin mirar el cruce.
- Si una buseta para en doble fila delante de ti, rodéala con direccional o
  espera ~8 s a que siga; los NPC bloqueados pitan y se reciclan solos.
- **Ahora son DOS vueltas** (niveles finales más largos): al tocar la baliza
  la primera vez se APAGA y el HUD pasa a "VUELTA 2/2" — aléjate de nuevo,
  repite el circuito y recién la segunda llegada cierra la misión.
- **Tiempo ideal: 230–300 s** (dos esperas de semáforo incluidas).

## C3 — La Simón de noche (SimonBolivar · 380 s · 4 NPCs · +43% de ruta)

**El diseño:** visibilidad y pendientes largas. La soledad es intencional
(GDD): pocos autos, la mitad de los postes sin luz. Aprobarla con ≥70
**desbloquea la Mazda BT-50** en el garaje.

- Perfil de la avenida (alargada 2026-07-15): **sube 6 / baja 6 / sube 4 /
  BAJA al segundo valle / sube 6 final**. Vía ancha, límite **90** señalizado.
- **El PEAJE del segundo valle** (caseta + conos del paquete): la vía se
  angosta — baja a 2ª y pasa despacio entre los conos; embestir la caseta
  cuenta como choque (−4 defensiva).
- **Luces con L** (ciclo off→cortas→largas): cortas en los tramos con postes,
  **largas en los tramos oscuros** — sin ellas literalmente no ves los
  guardavías de la bajada. Esto es lo que el playtest debe validar (CHECKLIST C).
- En bajada: motor en 3ª como freno; si bajas en 5ª acelerando, llegarás
  demasiado rápido a la curva del valle.
- Los 4 NPCs aparecen de a poco; con la burbuja defensiva no te embisten,
  pero de noche los ves tarde: esa es la lección de las largas.
- **Tiempo ideal: 220–300 s.** Hay margen — la misión es de supervivencia
  visual, no de reloj.

## C4 — Hora pico (Corredor · 340 s · 12 NPCs · +30% de ruta)

**El diseño:** "el tráfico ES la misión". Guamaní → redondel → cuesta →
La Carolina, al atardecer, con la densidad máxima del juego.

- **Distancia con el de adelante**: los NPC frenan por física (tiempo-a-
  colisión), tú no tienes esa ayuda — si te pegas a una buseta y para en
  doble fila (35% de probabilidad, "¡sube, sube!"), frenas en seco o chocas.
- Los embotellamientos se disuelven solos: el NPC bloqueado pita (~1.4 s),
  replanifica su ruta (6 s) y se recicla si sigue trabado (14 s). Si el
  atasco es POR TI (estás cruzado en la vía), muévete y se deshace.
- Respeta los semáforos aunque el taxista de al lado no lo haga: −4 cada
  rojo — y **no te detengas sobre las cebras** (−2 por bloquearlas).
- Límite **50** señalizado en el corredor; brazos alargados (+2 módulos).
- **Tiempo ideal: 220–290 s.**

## F1 — ¡Habla, Camarón! (CIUDAD TOON · 360 s · 10 NPCs · EXAMEN)

**El diseño (2026-07-15):** el examen ahora cruza LA CIUDAD ENTERA de
Toon City (Demo_Scene_1 adaptada: ~465 m de ruta urbana densa, 5 cruces
semaforizados con pasos cebra, parques, la ciudad completa del paquete).
Mishel espera bajo el arco al otro extremo. No pide contenido nuevo: pide
SOLTURA sostenida — semáforos, cebras, cruces y tráfico, todo junto.

- Todo junto: arranque limpio, marchas fluidas (3ª-4ª donde se pueda),
  direccional en cada giro, semáforos respetados, cero choques.
- Presupuesto mental: ~15 s arranque + ~240 s de ruta urbana + DOS esperas
  de semáforo (~50 s) = ~305 s. **No hay margen para tres rojos + caladas**:
  administra el riesgo y usa la guía de ruta (los cruces se parecen entre sí).
- La guía del copiloto es tu mapa: "EN 40 m GIRA A LA..." te lleva por la
  ruta larga; el minimapa muestra las calles reales de la ciudad.
- El resultado usa las pantallas de FINAL (bueno o alternativo), no la
  evaluación normal. Aprobar = llegar al concierto de Sal y Mileto.
- **Este nivel DEBE sentirse apretado pero posible.** Si en 3 intentos con
  buena técnica no sale, el hallazgo es "subir F1 a ~420 s"; si sale a la
  primera caminando, "bajar a ~320 s".

---

---

## Los niveles 3-7 en la CIUDAD GRANDE (reparto vigente)

| Nivel | Misión | Tiempo | NPCs | Meta (ancla) |
|---|---|---|---|---|
| 3 | La cuesta | 330 s | 6 | `Meta_Cuesta` — el punto más alto de la ciudad |
| 4 | El redondel (**2 vueltas**) | 360 s | 8 | `Meta_Redondel` — **el redondel de la ciudad**: ahí das la vuelta, que es lo intuitivo |
| 5 | La ciudad de noche | 400 s | 5 | `Meta_Noche` — la escena se oscurece en runtime; usa **L** |
| 6 | Hora pico | 380 s | 12 | `Meta_HoraPico` — el tráfico más denso del juego |
| 7 | ¡Habla, Camarón! (examen) | 360 s | 10 | `[MetaExamen]` — el arco, de punta a punta |

Qué mirar en estos niveles: que la **guía de ruta** te lleve bien (los cruces
se parecen), que los **semáforos y cebras** se respeten (parar ANTES de la
raya), y que en el nivel 4 la baliza se apague al tocarla y el HUD pase a
"VUELTA 2/2" — la segunda vuelta se cierra volviendo al mismo redondel.

## Qué anotar durante el playtest (para comparar contra el diseño)

1. Tiempo real usado por misión vs. el "ideal" de arriba.
2. Nº de caladas por misión (esperado: 2-3 en T1-C1, 0-1 desde C2).
3. ¿Algún atasco de NPCs que NO se disolvió solo en <15 s? (dónde, F9 para
   ver los estados FSM).
4. ¿La guía de ruta dio alguna instrucción absurda? (dónde apuntaba).
5. ¿Algún fallo de misión que se sintió injusto? — esa es la señal de
   balance más valiosa de todas.
