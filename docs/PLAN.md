# ¡Habla, Camarón! — Plan Maestro de Desarrollo

**Objetivo:** juego completo y jugable de inicio a fin: 3 categorías de misiones,
tráfico NPC con IA (A* + Máquina de Estados Finitos), física arcade con embrague
simulado, entorno construido con **Toon City**, y todo el ciclo
briefing → conducción → evaluación → desbloqueo funcionando de verdad.

**Base existente (no se rehace):**
- UI completa por código (13 pantallas): `UITheme`, `UIFactory`, menú, mapa de
  campaña, briefing, HUD, chat Mishel, pausa, evaluación, finales, créditos.
- Núcleo: `GameManager`, `GameData` (PlayerPrefs), `SceneLoader`, `Bootstrap`.
- Asset **Toon City** importado (calles, autopistas, edificios, ~25 autos, props,
  semáforos, terreno, skyboxes).
- Unity 6000.3 LTS, URP, Input System, AI Navigation instalado (no lo usaremos
  para NPCs: implementamos A* propio, más defendible académicamente).

**Namespaces nuevos:** `HablaCamaron.Vehicle`, `HablaCamaron.AI`,
`HablaCamaron.World`, `HablaCamaron.Missions`.

---

## Fase 0 — El auto (semana 1, prioridad absoluta)

> Recomendación del propio documento de diseño: "consolidar la física del
> embrague como prioridad técnica desde el inicio".

### 0.1 `VehicleController` (Rigidbody + 4 WheelColliders)
- Motor simulado: RPM en función de marcha + velocidad de ruedas; curva de
  torque simple (AnimationCurve).
- **Embrague** (mecánica central): eje 0–1. Soltarlo brusco con RPM bajas →
  **motor calado** (evento `OnStalled`, re-encender con tecla E). Soltarlo
  gradual → arranque limpio. Punto de fricción perceptible.
- Caja manual: 5 marchas + N + R (teclas 1-5, N, R). Cambiar sin pisar embrague
  → rechinido + penalización. Marcha incorrecta → el motor "pide" cambio
  (RPM al rojo en el HUD).
- Freno de mano (Espacio): retiene en pendiente; arrancar en cuesta sin él →
  el auto rueda atrás (evento para el scoring).
- Direccionales (A/D como toggle con Alt, o Q/E — decidir en pruebas), luces
  (L), bocina (B).
- Perfiles por vehículo (`VehicleSpec` ScriptableObject): Aveo (suave,
  perdonador) y Mazda BT-50 (pesada, más inercia) — mismos scripts, datos
  distintos.

### 0.2 Cámara y feedback
- Cámara primera persona desde el volante con leve cabeceo al calar/frenar.
- Espejo retrovisor central (RenderTexture pequeña, 256px, ocultable si pega
  al rendimiento).

### 0.3 Integración HUD
- Conectar `HUDController` existente a datos reales: km/h, RPM (arco rojo),
  marcha, íconos de freno de mano / direccional / calado.

**Criterio de salida de fase:** en una calle recta de Toon City puedo arrancar,
calar el motor si suelto mal el embrague, pasar las 5 marchas, frenar y
arrancar en una pendiente con freno de mano.

---

## Fase 1 — Quito con Toon City (semanas 1–2)

### 1.1 Escenas de mundo
Reemplazar el contenido de `Gameplay.unity` por **una escena de mundo por zona**
(aditiva sobre una escena base con managers):
- **Zona Sur (Quitumbe/Guamaní):** calles residenciales de Toon City +
  pendientes (terreno o roads inclinados `Road_1A_+2/+4`), garaje del papá
  (punto de inicio del tutorial), un redondel.
- **Av. Simón Bolívar:** prefabs `Highway_*`, larga, con curvas y pendiente,
  versión nocturna (skybox + iluminación) para la misión de luces.
- **Corredor Centro → La Carolina:** tramo urbano denso para el examen final
  (reutiliza piezas de las otras zonas; semi-genérico, como dicta el doc).
- Identidad quiteña: colores de fachadas, letreros propios ("huecas",
  parada del Trole) como texturas/decals sobre props de Toon City.

### 1.2 Sistema de carreteras lógico (la base de la IA)
- **`RoadGraph`**: grafo dirigido de waypoints (nodos con posición + carril;
  aristas con costo = distancia). Se edita con una herramienta de Editor
  (gizmos en escena, snap a las calles de Toon City).
- **`TrafficLightController`**: ciclo verde/amarillo/rojo por intersección;
  los nodos del grafo saben qué semáforo los gobierna.
- Señales de tránsito como datos: cada señal (PARE, ceda, límite) referencia
  el nodo/arista al que aplica → la usan tanto los NPCs como el scoring.

**Criterio de salida:** puedo manejar por las 3 zonas; el grafo cubre todas las
calles y los semáforos ciclan.

---

## Fase 2 — IA de NPCs: A* + FSM (semana 2)

Justificación académica: **A*** resuelve la *navegación* (qué ruta tomar sobre
el grafo de calles) y la **Máquina de Estados Finitos** resuelve el
*comportamiento* (qué hacer a cada instante). Dos algoritmos clásicos,
implementados por nosotros, visibles con gizmos y explicables en la defensa.

### 2.1 `AStarPlanner` (HablaCamaron.AI)
- A* sobre `RoadGraph` con heurística euclidiana. Devuelve lista de waypoints.
- Re-planificación si la ruta se bloquea (doble fila, choque adelante).
- Debug: dibujar la ruta de cada NPC en Scene view.

### 2.2 `NpcDriverFSM` — estados del conductor
| Estado | Qué hace | Transiciones típicas |
|---|---|---|
| `Cruising` | Sigue su ruta A* a velocidad crucero | ve obstáculo → Following/Braking |
| `Following` | Mantiene distancia con el de adelante | se libera → Cruising |
| `Braking` | Frena por semáforo rojo, PARE o peatón | verde → Cruising |
| `Yielding` | Cede el paso en redondel/intersección | vía libre → Cruising |
| `Overtaking` | Cambio de carril (a veces ¡sin direccional!) | completa → Cruising |
| `DoubleParked` | Buseta/taxi detenido en doble fila un rato | timer → Cruising |
| `Stalled/Crashed` | Tras colisión, se detiene | despawn |

- **Sensores:** raycasts frontales/laterales + OverlapSphere para detectar
  jugador, NPCs, semáforos y señales.
- **Personalidades** (`DriverProfile` ScriptableObject): *taxista* (agresivo,
  se mete, pita), *buseta* (para en cualquier lado, arranca lento, sonido del
  Trole), *particular* (conservador). Cambian umbrales de la FSM, no el código.
- Los NPCs usan física simplificada (sin WheelColliders: seguir spline de la
  ruta con velocidad/aceleración) para que 20–30 NPCs corran fluidos.

### 2.3 `TrafficManager`
- Spawn/despawn de NPCs en un radio alrededor del jugador (pooling con los
  prefabs `Car_*` de Toon City recoloreados: taxis amarillos, busetas azules).
- Densidad configurable por misión (hora pico del examen final = densidad max).
- Eventos imprevistos guionizados por misión: buseta que se cruza, perro que
  aparece (trigger + prefab), frenazo del de adelante.

**Criterio de salida:** en la zona Sur circulan ≥15 NPCs que respetan semáforos
(o no, si son taxistas), se detienen entre sí, y puedo mostrar la ruta A* y el
estado FSM de cualquier NPC en pantalla (modo debug F9).

---

## Fase 3 — Misiones y evaluación (semana 3)

### 3.1 `MissionSystem`
- `MissionDef` (ScriptableObject): id, categoría, zona, punto de inicio, meta,
  tiempo límite, densidad de tráfico, eventos guionizados, líneas de briefing,
  chat de Mishel posterior.
- `MissionRunner`: carga la zona, coloca al jugador, arma triggers de
  inicio/fin, corre el timer, dispara eventos.

### 3.2 `ScoringSystem` — los 6 criterios del documento (0–100, aprueba con 70)
| Criterio | Pts | Fuente del dato |
|---|---|---|
| Cumplimiento del objetivo | 30 | MissionRunner (llegó, parqueó, maniobra) |
| Señales y semáforos | 20 | −4 por infracción (semáforo rojo, PARE, límite) |
| Técnica de manejo | 20 | calados, frenazos, cambios sin embrague |
| Conducción defensiva | 15 | distancia segura, anticipación, direccionales |
| Tiempo | 10 | solo penaliza si excede el límite razonable |
| Estado del vehículo | 5 | roces/choques; choque grave = misión fallida |

- Conectar el desglose a la `EvaluationScreen` ya existente (barras animadas).

### 3.3 `DonPanchoBrain` — retroalimentación en tiempo real
- Suscrito a los eventos de gameplay (calado, semáforo en rojo, buen arranque
  en pendiente, choque…) → elige frase contextual del repertorio de 10 tipos
  de eventos y la manda a la burbuja del HUD (con cooldown para no saturar).

### 3.4 Contenido de misiones (7 misiones, 3 categorías)
1. **T1 – "Sacar el Aveo"**: salir del garaje sin rayar la pared (embrague, R, 1ª).
2. **T2 – "La vuelta a la manzana"**: marchas 1-3, semáforos, PARE.
3. **C1 – "La cuesta de Guamaní"**: arranque en pendiente con freno de mano.
4. **C2 – "El redondel"**: ceder el paso, direccionales, tráfico medio.
5. **C3 – "Simón Bolívar de noche"**: luces, alta velocidad, 4ª-5ª marcha (BT-50 se desbloquea al aprobar).
6. **C4 – "Hora pico"**: tráfico denso, busetas agresivas, conducción defensiva.
7. **F1 – "Habla, Camarón" (examen)**: Guamaní → La Carolina con timer, hora
   pico, todo evaluado. Aprobar = final bueno (concierto); reprobar = final
   alternativo (Mishel en taxi) — ambas pantallas de final ya existen en UI.

---

## Fase 4 — Audio, pulido y cierre (semana 3–4)

- **Audio con AudioSource nativo de Unity** (FMOD queda como "trabajo futuro"
  del informe: integrarlo ahora cuesta más de lo que aporta):
  - Motor por RPM (pitch shifting), calado, embrague, direccionales, bocina.
  - Ambiente por zona (sur: perros/vendedores; Simón Bolívar: viento).
  - Música de menús/evaluación; mixer con volúmenes por capa (ya hay sliders
    en `OptionsPanel`).
  - Frases de Don Pancho: placeholders TTS o grabaciones del equipo.
- Señaléticas ecuatorianas (≥20 señales como texturas sobre props).
- Garaje funcional: Aveo desde el inicio, BT-50 tras la categoría 2.
- Balanceo de puntajes jugando cada misión; ajustar umbrales de la FSM.
- Build Windows + prueba con el mando de carreras del laboratorio (Input
  System ya abstrae los ejes; solo es mapear bindings).

---

## Riesgos y decisiones tomadas
- **NPCs sin WheelColliders** (siguen la ruta cinemáticamente): decisión
  deliberada de rendimiento; solo el jugador tiene física completa.
- **NavMesh instalado pero no usado para tráfico:** los autos deben respetar
  carriles y sentidos → el grafo de waypoints es el modelo correcto, y A*
  propio da mérito académico.
- **El embrague es el riesgo #1 de gameplay:** por eso es la Fase 0 y se
  prueba con gente desde la semana 1.
- **Alcance de mapas:** semi-genéricos con identidad quiteña (decisión del
  documento de diseño) — no replicar calles reales.

## Orden de trabajo sugerido para el equipo
- **Mateo (programación/arquitectura):** Fase 0 (vehículo) → Fase 2 (IA).
- **Marco (entorno 3D):** Fase 1 (zonas con Toon City + identidad quiteña).
- **Eduardo (UI/UX/arte):** conexión HUD↔gameplay, señaléticas, evaluación,
  audio y contenido narrativo (briefings, chats de Mishel).
