# ¡Habla, Camarón! — Checklist COMPLETO de verificación (Fases 0-4)

Marca cada casilla. Si algo falla, anota qué pasó y en qué paso.

## A. Compilación y tests

- [ ] A1. Abrir Unity → Console (Ctrl+Shift+C) **sin errores rojos**.
- [ ] A2. Test Runner → EditMode → Run All → **126 verdes**.
- [ ] A3. Test Runner → PlayMode → Run All → **8 verdes** (no tocar mientras corre).

## B. Fase 0 — El auto (escena `N0_TestDrive`, Play)

**Encendido y embrague (la mecánica central):**
- [ ] B1. Con 1ª puesta y SIN pisar Shift, presiona F → **no enciende** y avisa "pisa el embrague".
- [ ] B2. Mantén Shift + F → **enciende** (se oye el motor + sonido de arranque "ñi-ñi" + toast "Motor encendido").
- [ ] B3. Con 1ª, suelta Shift **de golpe** sin acelerar → **se cala**: sacudida de cámara, tono que cae, ícono de calado en HUD, frase de Don Pancho.
- [ ] B4. Shift + F + 1ª + W (un poco) + soltar Shift **despacio** → arranca suave y avanza.
- [ ] B5. Mira la barra **EMB** (abajo izquierda) al soltar: el pedal sube gradual, tiene una **línea dorada** (punto de fricción) y se pone **ROJA** justo antes de calar.

**Caja de cambios:**
- [ ] B6. Sube 1→2→3→4→5 pisando Shift → cada cambio: **golpe seco** + el número del HUD **salta y destella dorado** + toast "Marcha: Xª".
- [ ] B7. Intenta meter una marcha SIN Shift → **rechinido áspero**, destello **rojo**, la marcha **no entra**, Don Pancho reclama.
- [ ] B8. A más de ~10 km/h intenta meter **R** → no entra (protege la caja). Detenido casi por completo → sí entra.
- [ ] B9. Acelera a fondo en 1ª sin cambiar → el arco de RPM llega al **rojo** y el motor **corta** (no acelera más) hasta que subas de marcha.

**Freno de mano y la cuesta:**
- [ ] B10. El juego inicia con freno de mano puesto (ícono en HUD). Acelera sin soltarlo → aviso "¡El freno de mano está puesto!".
- [ ] B11. Espacio → toast "Freno de mano quitado" + sonido de trinquete.
- [ ] B12. Sube la cuesta, frena a la mitad, pon freno de mano → **el auto NO rueda hacia atrás**. Quítalo sin acelerar → **sí rueda hacia atrás**. Arranca con embrague+acelerador → sube.
- [ ] B13. Detente en la cuesta con motor encendido → Don Pancho da su consejo de pendiente.

**Los demás controles:**
- [ ] B14. Q → direccional izquierda: **parpadea** en el HUD + **clic-clic** sonoro + toast. Q otra vez → se apaga.
- [ ] B15. E → lo mismo a la derecha.
- [ ] B16. L tres veces → toasts "Luces: cortas" → "LARGAS" → "apagadas".
- [ ] B17. B (mantener) → bocina bitonal.
- [ ] B18. C → cámara exterior siguiendo el auto; C otra vez → vuelve al volante.
- [ ] B19. En 1ª persona: el **retrovisor** (arriba centro) muestra lo que quedó atrás.
- [ ] B20. H → panel de **ayuda** con todos los controles; H lo cierra.
- [ ] B21. El **tutor** (arriba izquierda) te guía paso a paso al arrancar y desaparece cuando ya manejas; si te calas, **reaparece** adaptado.
- [ ] B22. Esc → pausa congela todo; Reanudar continúa.
- [ ] B23. El sonido del motor **sube de tono con las RPM** y calla al apagarlo (F).

## C. Fase 1 — El mundo (3 zonas)

**Zona Sur (`N1_ZonaSur`):**
- [ ] C1. Menú Habla Camarón → 3 regenera la zona → diálogo: problemas "**ninguno ✓**" y ruta garaje→cima "**alcanzable ✓**".
- [ ] C2. En la ventana Scene con **Gizmos activados**: esferas cian sobre cada carril, **flechas** con el sentido, anillo de 8 nodos en el redondel, esferas **rojas** en las 4 entradas (semáforos), **amarillas** donde hay señales.
- [ ] C3. Play: los 4 semáforos del redondel ciclan — **N/S en verde mientras E/O en rojo** y viceversa; hay un momento con **todos en rojo** (colchón); "amarillo" = ambas lámparas encendidas.
- [ ] C4. Es de **día** (el tutorial es de día según el GDD).
- [ ] C5. Maneja: garaje → redondel → brazo norte → cuesta hasta los barriles, sin caerte por huecos de colisión.

**Av. Simón Bolívar (`N1_SimonBolivar`):**
- [ ] C6. Menú 4 la genera → diálogo "✓". Al abrir: **noche cerrada** con niebla.
- [ ] C7. Play sin luces → casi no ves la vía. **L (cortas)** → ves ~25 m, apuntando al piso. **L (largas)** → ves lejos y plano. Esa diferencia es la misión.
- [ ] C8. El perfil de la vía: **sube → meseta → BAJA → llano → vuelve a subir** (bajada real).
- [ ] C9. Guardavías a los lados en los tramos planos; postes escasos y **solo algunos alumbran** (naranja sodio).
- [ ] C10. Es una vía **más ancha** que las calles de la Zona Sur.

**Corredor del Examen (`N1_CorredorExamen`):**
- [ ] C11. Menú 5 lo genera → diálogo: ruta Guamaní→meta "**alcanzable ✓**".
- [ ] C12. Ambiente **atardecer** (la hora narrativa del examen).
- [ ] C13. Sur: barrio con casas y veredas → redondel con semáforos → cuesta → **La Carolina** con edificios modernos y arbolitos.
- [ ] C14. Al final del brazo norte: el **arco** de la meta y el objeto `[MetaExamen]` en la Hierarchy.

## D. Fase 2 — IA de NPCs (en cualquier zona, tras el menú 6)

- [ ] D1. Menú Habla Camarón → 6 → "Tráfico NPC inyectado en 3 zona(s)".
- [ ] D2. Play: en ~20 segundos hay **autos de Toon City circulando** (si no corriste el menú 6, serían cajas de colores — eso también es correcto como fallback).
- [ ] D3. Los NPCs van **por su carril y en el sentido correcto** (nunca de frente contra ti en tu carril).
- [ ] D4. Frente a un semáforo en **rojo**, los NPCs **se detienen** y arrancan en verde.
- [ ] D5. Observa un rato: **algún taxista se pasa el rojo** de vez en cuando (¡correcto! es su personalidad, 35% de las veces).
- [ ] D6. Alguna **buseta se detiene en plena vía** unos segundos (doble fila) y luego sigue.
- [ ] D7. Un NPC detrás de otro más lento **no lo choca**: baja la velocidad y lo sigue.
- [ ] D8. Chócale suavecito a un NPC → se queda **detenido ~3 segundos** (estado Crashed) y luego sigue.
- [ ] D9. **F9** → overlay con el conteo por estado FSM (Cruising, Braking, Following...).
- [ ] D10. En Play, ventana Scene: selecciona un `NPC_Car_...` en la Hierarchy → su **ruta A\* en magenta** dibujada sobre las calles.
- [ ] D11. Aléjate mucho de una zona → los NPCs lejanos desaparecen y aparecen nuevos cerca (spawn dinámico).
- [ ] D12. **Burbuja defensiva**: párate en medio de la vía → el NPC que se acerca FRENA a tiempo (por TTC) y a menos de ~10 m gatea; no te embiste ni de frente ni en curva.

## E. Fase 3 — El flujo completo del juego (desde `Splash`, Play)

**Menú y mapa:**
- [ ] E1. Splash pasa solo al menú principal.
- [ ] E2. Primera vez: "**Continuar" está deshabilitado** (no hay partida).
- [ ] E3. "Nueva partida" → mapa de campaña con **las 7 misiones en dos filas**: misión 1 con botón **Jugar**, las demás con **candado que explica** cómo desbloquear; progreso "0 de 7".

**Misión (ciclo completo):**
- [ ] E4. Jugar misión 1 ("Sacar el Aveo") → carga la **Zona Sur** → **briefing** de Don Pancho con el juego congelado (título, zona, objetivos).
- [ ] E4b. En la misión 1 **NO hay tráfico** (tutorial en calma); en la 2 hay poco (3 autos). Ningún NPC aparece a menos de ~45 m de ti.
- [ ] E4c. **Límites del mundo**: sal de la vía y maneja lejos → un muro invisible te detiene y Don Pancho dice "¡Por ahí no es, camarón!". Pasa en las 3 zonas.
- [ ] E5. Al cerrar el briefing → aparece el temporizador "**TIEMPO 2:30 · META xxx m**" y la distancia baja al acercarte. La meta es el **semáforo de entrada al redondel**.
- [ ] E6. La meta es una **columna de luz dorada** visible a lo lejos.
- [ ] E7. Llegar a la baliza → **pantalla de evaluación**: barras animadas de los 6 criterios + veredicto de Don Pancho (el juego queda congelado pero la animación corre).
- [ ] E8. **El puntaje no miente**: repite la misión calando 2 veces y pasándote un rojo → Técnica 20→**14** y Señales 20→**16** en el desglose. Pasar embalado junto al **límite 30** del brazo sur también baja Señales (−4).
- [ ] E8b. **Direccionales**: gira en una esquina SIN direccional → Don Pancho reclama ("¡La direccional, camarón!") y Defensiva baja 15→**13** en el desglose. Repite avisando con Q/E justo antes del giro → no descuenta. Una curva suave larga tampoco descuenta.
- [ ] E9. "Continuar" → vuelve al mapa → **llega el chat de Mishel** (solo esta vez, no se repite al reabrir el mapa).
- [ ] E10. Con ≥70: la misión queda **aprobada con su puntaje** en el mapa y la siguiente se **desbloquea**. Con <70: sigue bloqueada.
- [ ] E11. "Reintentar" en la evaluación → recarga la misma zona con **LA MISMA misión** (con 4 misiones en la Zona Sur, reintentar C2 no debe abrir T1).
- [ ] E12. Misión 2 ("La vuelta a la manzana"): el HUD dice "**ALÉJATE DEL BARRIO**", la baliza de volver **aparece recién al alejarse** ~60 m, y la misión NO termina en el segundo cero.
- [ ] E13. Misión 3 ("La cuesta"): la meta es la **cima con los barriles**. Misión 4 ("El redondel"): más tráfico y volver a casa.
- [ ] E14. Misión 5 ("La Simón de noche") → briefing nocturno → misma dinámica con las luces (L).
- [ ] E15. Misión 6 ("Hora pico") → Corredor al atardecer con **mucho más tráfico** → llegar al arco.
- [ ] E16. Misión 7 (examen) → llegar al **arco** a tiempo → pantalla de **final bueno**. Dejar acabar el tiempo → **final alternativo** (Mishel en taxi) — ambos con sus botones (Reintentar examen / Volver al menú) funcionando.
- [ ] E17. Esc funciona en plena misión (pausa) y Reanudar continúa sin romper el timer. Con el **briefing o la evaluación abiertos, Esc NO abre la pausa** (no debe descongelar el flujo).

**Persistencia:**
- [ ] E18. Tras aprobar la misión 1, **detén el Play y vuelve a dar Play desde Splash** → "Continuar" ahora está habilitado y el mapa recuerda tu progreso y puntajes.
- [ ] E19. "Nueva partida" otra vez → el progreso se reinicia (misiones 2 a 7 bloqueadas de nuevo) y **no aparece ningún chat de Mishel viejo**.

**Pantallas restantes (navegación):**
- [ ] E20. Desde el menú: Opciones abre y cierra; Garaje muestra los vehículos; Créditos corre y regresa.

## F. Fase 4 — Audio, garaje, señales y build

**Mixer y música:**
- [ ] F1. En el menú principal suena un **pad suave de atardecer** (música procedural); en el mapa y el garaje también; al entrar a una misión, se **funde a silencio**.
- [ ] F2. Opciones → mover el slider de **Música** se oye EN VIVO (sin guardar ni reabrir). A 0% calla del todo.
- [ ] F3. El slider de **Efectos** cambia en vivo el volumen del motor Y del ambiente de la zona.
- [ ] F4. **Sensibilidad de pedales** al máximo → el acelerador responde notablemente más rápido; al mínimo, más calmado. En 50% se siente EXACTAMENTE como siempre.
- [ ] F5. Al aprobar una misión suena un **arpegio que sube**; al reprobar, uno que **baja** (con el juego congelado igual suenan).

**Ambiente por zona:**
- [ ] F6. Zona Sur: brisa suave, **pájaros** de vez en cuando y algún **perro lejano**.
- [ ] F7. Simón Bolívar: **viento sostenido** y nada más (la soledad es el diseño).
- [ ] F8. Corredor: **rumor urbano** y alguna **bocina lejana**.

**Señalética ecuatoriana:**
- [ ] F9. Zona Sur: se leen ZONA ESCOLAR, PEATONES, REDONDEL, PITO tachado, TROLE, RESALTO, CURVA, BUS y NIÑOS (9 señales, formas y colores del reglamento).
- [ ] F10. Simón Bolívar: placa **"90"** real (círculo blanco/rojo), CURVA, NO REBASAR y ← DOBLE →.
- [ ] F11. Corredor: placa **"50"** en el brazo sur (y pasar embalado junto a ella descuenta señales −4), SEMÁFORO, U tachada, H de hospital, E tachada, UNA VÍA, PARE y NO ENTRE.
- [ ] F12. Las señales se leen del lado y sentido correctos (encaran al carril que les corresponde, sin texto espejado).

**Garaje con BT-50:**
- [ ] F13. Partida nueva → Garaje: el Aveo **SELECCIONADO**, la BT-50 con **candado** que dice qué misión aprobarla.
- [ ] F14. Aprobar "La Simón de noche" (misión 5) → la BT-50 se **desbloquea** y se puede Elegir.
- [ ] F15. Con la BT-50 elegida, entrar a una misión: el auto **se siente camioneta** (pesado, arranca con fuerza, cala más fácil) — es el mismo modelo 3D, cambia la ficha.
- [ ] F16. "Nueva partida" NO borra la selección, pero si la BT-50 quedó bloqueada de nuevo, el juego **vuelve al Aveo solo** (anti-trampas).

**Build:**
- [ ] F17. Menú Habla Camarón > 7 → genera `Builds/Windows/HablaCamaron.exe` sin errores; el .exe corre el flujo Splash → menú → misión.
- [ ] F18. (Laboratorio) El build responde con el **mando de carreras** — pendiente de hardware.

## G. Cierre — voz, mando y minimapa

**Voz de Don Pancho:**
- [ ] G1. Cada burbuja de Don Pancho viene con un **balbuceo grave** (no palabras) que dura acorde al texto.
- [ ] G2. La **misma frase** suena siempre con la misma "melodía" (repite la misión y compara).
- [ ] G3. El slider **Voz de Don Pancho** en 0 lo calla; a media, se oye a media — EN VIVO.
- [ ] G4. En el veredicto (juego congelado) el balbuceo **también suena**.

**Mando (con un gamepad genérico; el volante del laboratorio es F18):**
- [ ] G5. Stick izquierdo gira el volante de forma **analógica** (medio stick = medio giro); arriba/abajo acelera/frena.
- [ ] G6. **LB mantiene el embrague**; A/B suben/bajan la marcha en secuencia R→N→1..5 (sin embrague, rechina y no entra — la pedagogía manda también en mando).
- [ ] G7. RB = freno de mano, Y = encender, X = bocina. El **teclado sigue funcionando igual** con el mando conectado.

**Minimapa real:**
- [ ] G8. En una zona: el panel del mapa muestra **las calles de verdad** (la forma del redondel y los brazos se reconocen).
- [ ] G9. El punto terracota **se mueve contigo** por el mapa; el norte queda arriba (subir la cuesta de la Zona Sur = el punto sube).
- [ ] G10. En misión: la **meta dorada** aparece en el mapa; en "volver a casa" aparece **recién al armarse la baliza**.
- [ ] G11. M oculta/muestra el minimapa, como siempre.
- [ ] G12. La escena Gameplay (demo) ya **no tiene el tester**: no anima el HUD con datos falsos ni responde a F1-F5.
