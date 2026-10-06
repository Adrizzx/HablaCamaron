# ¡Habla, Camarón! — Esqueleto + Menú Principal

Scripts base en C# para Unity (uGUI por código). Esto cubre el **núcleo del juego**
y la **navegación entre escenas** (criterio 3 de la rúbrica).

## Qué incluye

```
Scripts/
├── Core/
│   ├── GameManager.cs    → singleton, persiste entre escenas, estado de partida
│   ├── GameData.cs        → progreso (misión desbloqueada, puntajes) en PlayerPrefs
│   ├── SceneLoader.cs     → navegación entre escenas con fundido a negro
│   └── Bootstrap.cs       → auto-crea los managers (no tocar nada en el editor)
└── UI/
    ├── UITheme.cs         → sistema de diseño: paleta, radios, estilos de botón
    ├── UIFactory.cs        → helpers para construir UI por código (esquinas redondeadas)
    ├── MainMenuController.cs → menú principal v2 (jerarquía + grid 2x2)
    └── OptionsPanel.cs       → panel de configuración (audio por capa, pedales, idioma)
```

> Diseño revisado con principios anti-slop: un solo color de acento (terracota),
> una escala de esquinas, jerarquía clara (Continuar primario, Salir desjerarquizado),
> estados hover/press/disabled reales en cada botón.

## Pasos en Unity (una sola vez)

1. **Copia la carpeta `Scripts/`** dentro de `Assets/` de tu proyecto.
   (Si usas Claude Code, esto lo hace él directamente en tu repo.)

2. **Crea 3 escenas** vacías en `Assets/Scenes/` con estos nombres EXACTOS:
   - `MainMenu`
   - `CampaignMap`
   - `Gameplay`

3. **Agrega las 3 escenas a Build Settings:**
   `File > Build Settings > Add Open Scenes` (abre cada una y agrégala).
   Asegúrate de que `MainMenu` quede primera (índice 0).

4. **En la escena `MainMenu`:**
   - Crea un GameObject vacío (`GameObject > Create Empty`), llámalo `MenuController`.
   - Arrástrale el script `MainMenuController.cs`.
   - Play. El menú se construye solo.

¡Eso es todo! No hay que armar Canvas ni botones a mano.

## Cómo probar

- Dale Play en la escena `MainMenu`.
- "Continuar" aparece desactivado hasta que tengas progreso guardado (correcto).
- "Nueva partida" reinicia el progreso y te lleva a `CampaignMap` con fundido.
- "Salir" cierra el juego (en el editor, detiene el Play).

## Lo que sigue (próximos pasos)

1. **Panel de Opciones** — volumen por capa (música/SFX/voz), sensibilidad de
   pedales, idioma. Se monta como overlay sobre el menú.
2. **CampaignMap** — mapa isométrico con íconos de misión por dificultad.
3. **HUD de conducción** — velocímetro, marcha, mini-mapa, burbujas de Don Pancho.
4. **Pantalla de evaluación 0–100** — desglose con barras animadas.
5. **PausaController** — overlay con Time.timeScale = 0.

---

## Trabajando con Claude Code

Para que Claude Code continúe el desarrollo en tu repo, dale contexto al inicio:

> "Proyecto Unity 'Habla Camarón': simulador de conducción manual ambientado en
> Quito. UI en uGUI por código, paleta café/terracota/ocre. Ya está el esqueleto
> (GameManager, SceneLoader, MainMenuController). Sigue el mismo estilo: namespaces
> HablaCamaron.Core / .UI, UI generada por código, comentarios en español."

Luego pídele tareas concretas, por ejemplo:
- "Crea el OptionsPanel con sliders de volumen por capa siguiendo el estilo del menú."
- "Genera el CampaignMapController con los 6 íconos de misión."

Claude Code lee los scripts existentes y mantiene la consistencia.
