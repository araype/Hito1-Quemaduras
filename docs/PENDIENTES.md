# Pendientes

Única lista de lo que falta, sobre `consolidada` (une `experiencia-inmersiva`
y `corregir-bugs`, 2026-11-07; ver `docs/QUEMADURA_INMERSION.md`, sección
14). **Solo va lo que falta:** al terminar un punto, se borra de aquí y se
describe en `docs/QUEMADURA_INMERSION.md` (qué hay y cómo probarlo), diciendo
si se probó en el Quest, en el editor o si solo se compiló.

## Cómo retomar

- Unity: detener Play **antes** de editar scripts (recompilar en Play hizo
  caer Unity al salir de Play). El MCP de Unity no se conecta solo: hay que
  pulsar conectar en la ventana "MCP for Unity".
- Se prueba en el editor con Quest Link; la prueba con las manos en el Quest
  queda para el final (último paso del plan).
- Los valores nuevos de `BurnEvent` y demás enums van **al final**: las
  escenas guardan el número.

## Regla

Toda mecánica nueva debe estar respaldada por las guías reales (NHS, Cruz
Roja Británica, guía LSEBN). Lo que no tenga respaldo se anota aquí como "a
decidir" antes de implementarlo como error. La última revisión (templar el
agua, charco, gasa) está en `docs/QUEMADURA_INMERSION.md`, sección 12.

## 0. Probar la consolidación (primero)

`consolidada` **solo se compiló** (Roslyn fuera de Unity, sin errores). Las
dos ramas que une se probaron por separado en el editor, pero juntas no.
Abrirla en Unity, confirmar que la consola no muestra errores ni scripts
faltantes y recorrer la partida completa en el editor. Con más atención:

- **Film en tiras sobre la quemadura de Laura:** `FilmCoverage` ahora mide
  con el canal rojo de `BurnSkinPattern` (antes, la máscara de la quemadura
  de `corregir-bugs`, que se descartó). Revisar que 2 o 3 tiras a lo largo
  lleguen al 80 % y que la tarjeta muestre el porcentaje. Si no llega,
  bajar `FilmCoverage.burnAlphaMin` o `coverageNeeded`.
- **Llamada al 106 y diálogos de la tía:** no se abren a la vez. Si la tía
  pregunta algo, la llamada empieza al responderle; durante la llamada, su
  pregunta espera. Probar llamar justo en el minuto 3 y en el 9.
- **Menú de inicio:** estilo de tarjeta de Araype con la ubicación de
  jjosnayo (`PanelFollow`, escala 0,8 y brillo en los dos botones).
- **Olla:** fija hasta el accidente con la versión de `corregir-bugs` más el
  reintento de apagar los agarres de Meta cada 0,5 s.
- **Agarres:** versión de `corregir-bugs` (sin agarre a distancia, el objeto
  se acerca a la mano, se suelta si se tira fuerte) más la búsqueda de los
  agarres de Meta durante los primeros 30 s si el rig aún no estaba.
- **La tía al quemarse:** se aparta hacia el lavadero (`StepAwayFromStove`
  de Araype; se quitó `StepBackFromStove`) y luego muestra la mano y pide el
  anillo. Si tiras fuerte de su mano se suelta y se queja (`OnPulledAway`).

## 1. Voces

- **Ya dicen lo correcto** (2026-11-12, `QUEMADURA_INMERSION.md`, sección
  27): las 12 frases del narrador que decían algo incorrecto o faltaban y
  `op_centro` se regeneraron con las voces de Windows (Sabina para la
  narradora, Raúl para el operador). Falta **reemplazarlas con ElevenLabs**
  para que la narradora no cambie de voz: `guia_bienvenida`, `guia_anillo`,
  `guia_abrigo`, `guia_preparar`, `guia_cubrir`, `guia_err_pano`,
  `guia_err_curita`, `guia_err_anillo`, `guia_err_calma`,
  `guia_err_aguantar`, `guia_err_olla`, `guia_err_charco`, `guia_llamar`,
  `guia_apagar`, `guia_frio_lejos` (sección 29) y `op_centro`
  (textos en `docs/VOCES.md`).
- **Voces de la tía que siguen como TTS** (`voz_ansiosa`, `voz_calmada`,
  `voz_asustada`, `voz_frio`, `voz_aguanto`, `voz_templada`, `voz_aprieta`,
  `voz_templar_pide`): no dicen nada incorrecto; suena en su lugar una frase
  de ElevenLabs que encaja o solo el texto (`Hito1FX.TtsStandIns`).
  Regenerarlas con ElevenLabs.

## Plan: orden para lo que falta

| # | Pedido | Por qué en este orden | Tamaño |
|---|---|---|---|
| 0 | Probar la consolidación en el editor (sección 0) | Sin esto lo demás se construye sobre algo no probado. | Mediano |
| 1 | Regenerar voces (sección 1) | Se puede hacer en paralelo. | Chico |
| 2 | Variar el caso (E.1) | Que no se pueda memorizar la secuencia. El tamaño de la quemadura cambia cuánto film hace falta. | Grande |
| 4 | Línea de tiempo en el resumen; distractores; práctica de perilla en el tutorial (opcional) | Mejoras de la experiencia. | Variable |
| 4b | Probar la tía de Avaturn en el Quest; poses de mano y háptica (F) | Lo que más suma a la inmersión según la revisión de herramientas. | Grande |
| 5 | **Validación final en el Quest** con las manos y fps (G); pruebas con usuarios e informe | Al final, con todo estable. | Mediano |
| — | Manos fantasma (RF-04) | Recortar y quitarlo del informe. | — |

**Riesgos**

- Rendimiento: el film en tiras y la manta con tela simulada son lo único
  que puede bajar de 72 fps (la manta se duerme cuando está quieta). Medir
  en la validación final.

## D. Objetos sobre la paciente

- **Manta (hecha, `QUEMADURA_INMERSION.md`, sección 24), lo que falta:**
  probarla en el Quest (`PRUEBAS_QUEST.md`, sección 3). Un giro instantáneo
  de la tía (forzado por código) dejó un brazo por fuera; si pasa con los
  giros reales, volver a armar la tela cuando el pecho gire de golpe.
  Buscarla (sección 28): seguir las flechas del piso hasta la sala, rodear
  la mesa de centro sin rebotar y, en modo evaluación, oír la pista de la
  tía; regrabar `voz_pide_manta` y `guia_abrigo` con ElevenLabs.
- **Muebles y cajón (hecho, `QUEMADURA_INMERSION.md`, secciones 25 y 26),
  lo que falta:** probar en el Quest abrir la puerta del mueble alto y el
  cajón con las manos y con controles (el tirador está en el borde libre de
  la puerta y al frente del cajón; si cuesta encontrarlo, subir
  `StorageFront.handleReach`) y que el tiempo del enfriamiento alcance para
  abrigar, llamar y sacar el film o la bolsa del cajón.
- **Cubrir (hecho, sección 26), lo que falta:** probar en el Quest deslizar
  la bolsa por su mano y apoyar las tiras de film (`PRUEBAS_QUEST.md`,
  sección 11). Con la quemadura de Laura, una sola tira bien puesta ya
  cubrió el 88 % en el editor: revisar en la sección 0 si `coverageNeeded`
  debe subir para pedir más de una tira.

## E. Arco del juego

- **Variar el caso (E.1, RF-11):** grado (`BurnInjury.randomDegree`),
  extensión, zona y quizá edad. Hoy solo el botón "Otro caso" cambia el grado.
  Con la llamada al 106 ya hecha, decidir si en los casos leves el operador
  debe decir que no hacía falta llamar (las guías piden llamar solo en casos
  graves: quemadura grande, cara, eléctrica).
- **Distractores:** teléfono que suena, segunda hornilla, la tía que insiste
  en la mantequilla.
- **Línea de tiempo** en el resumen: qué hizo el jugador y cuándo, comparado
  con lo ideal.
- **Practicar una perilla o una llave en el tutorial (E.11, opcional).**

## F. Inmersión (revisión de herramientas, 2026-11-09)

Revisado contra el artículo de Bravent sobre VR con Unity (2024) y lo que ya
usa el proyecto. No se cambia al XR Interaction Toolkit: el proyecto ya
tiene el Interaction SDK de Meta (equivalente y hecho para el Quest), y
pasarse obligaría a rehacer el agarre, los botones y el rig.

- **Tía de Avaturn, lo que falta probar** (hecha y probada en Play,
  `QUEMADURA_INMERSION.md`, sección 18): en el Quest, los 72 fps (35 000
  triángulos, materiales Standard y el pelo con alpha to coverage), que el
  pelo se vea suave con el MSAA y que la cara, los brazos y la manta se vean
  bien de cerca; en el editor, el resto del caso con el modelo nuevo (agua,
  gasa, film, sacar el anillo, llevarla del brazo, caminar). Sacar el
  anillo con la mano real en el Quest (el tiempo para que se trabe se alargó:
  `QUEMADURA_INMERSION.md`, sección 19). Las cápsulas
  del torso y la cadera ahora son más gruesas (16 y 17 cm): revisar que no
  estorben al acercar las manos. Mide unos 11 cm más que la de Mixamo: si
  choca con la campana o los muebles, bajar la escala.
- **Usar más del Interaction SDK de Meta (evaluar):** poses de mano al
  agarrar (`HandGrabInteractable` con pose: la mano rodea la taza, el
  celular, el rollo de film en vez de quedar abierta) y probar el
  `PokeInteractable` contra nuestro `TouchButton`, solo si se ve mejor.
- **Háptica, lo que falta:** ya vibra en el agua, el fuego, los errores, los
  aciertos, los botones, el anillo, al tocar a la tía, al agarrar, al cortar
  el film y al sostener la gasa (`QUEMADURA_INMERSION.md`, sección 20).
  Falta: clips diseñados con Meta Haptics Studio (`HapticClipPlayer`, el SDK
  ya está instalado) para el Quest 3 y los Touch Pro, y reforzar con sonido y
  brillo cuando se juega con las manos (sin controles no hay vibración).
- **Video en pantalla (a decidir):** en el resumen o el tutorial, un video
  corto de la técnica real (enfriar 20 min, cubrir con film) en una
  pantalla de la cocina o en el celular. Debe tener respaldo de las guías
  (ver Regla).
- **Multijugador (fuera de alcance, a decidir):** modo instructor que mira
  y comenta la sesión (Netcode for GameObjects). Grande; solo si el curso
  lo pide.

## H. Auditoría (2026-11-09)

### H.1 Assets de más calidad (lo que queda)

El botiquín y la gasa ya están (`QUEMADURA_INMERSION.md`, sección 21), hechos
por código. Si se quiere más realismo, cambiar las piezas por modelos:
Poly Haven y ambientCG (CC0, sin atribución), Sketchfab (muchos CC BY: citar
al autor) o Unity Asset Store (gratis con cuenta). Los que piden cuenta los
baja el usuario. Presupuesto del Quest: menos de 10 000 triángulos y
texturas de 1024 por objeto de mano.

### H.2 Otras mejoras de inmersión

- **Boca:** ya sigue los visemas sacados del texto (`QUEMADURA_INMERSION.md`,
  sección 22). Si en el Quest no convence, la alternativa más precisa es
  Oculus Lipsync (lo baja el usuario desde la página de Meta, porque pide
  aceptar su licencia). El Movement SDK no sirve para esto: saca la cara
  del micrófono de quien juega, no de un audio grabado.
- Ver también F (poses de mano, háptica) y E (distractores, línea de tiempo).

## G. Validación, pruebas e informe

- **Probar en el Quest con las manos** todo lo hecho en el editor. Con más
  atención: los arreglos de `docs/QUEMADURA_INMERSION.md`, sección 16
  (animaciones de la tía: sección 17, y que sigan los 72 fps; cubrir con la
  gasa manteniéndola 1 s encima, solapa del film, cortar la tira con las manos y si se ve bien, gesto
  de envolver la mano con el film, la tía que se voltea para recibir
  la taza y la deja en el mesón, llevarla del brazo (que camine mirando
  hacia donde va), agarrar el celular, caminar entre
  objetos en el piso, empujar la olla volcada al caminar, que lo agarrado
  quede en los dedos que se ven (manos y controles), tarjetas junto al
  meson al acercarse, llamada en espera), brazo de la tía, agarre doble, agarre distante, paño, **tirar
  el film con las dos manos** (sostener el rollo, tomar el borde con la otra
  mano, cortar y soltar la tira a lo largo; solo se probó colocando las
  tiras por código), **templar el agua en el minuto 9** (tocar la llave
  caliente con la fría abierta; el panel tiene ahora 4 respuestas), pisar el
  charco (solo aviso; no se probó), gesto de pausa, que los paneles de la
  tía no estorben mientras se le sostiene el brazo, **la llamada al 106**
  (tocar "Llamar 106" en la pantalla con la otra mano o llevarlo a la oreja;
  el altavoz y el panel que acompaña: `QUEMADURA_INMERSION.md`, sección 29)
  y **sacar el anillo** con la mano ofrecida.
- **Orden flexible** (sección 29): recorrer la partida haciendo los pasos
  en otro orden (llamar después de cubrir, la manta al final, el anillo
  bajo el agua) y revisar que la tarjeta no se vuelva demasiado larga con
  la línea "Cuando puedas"; si tapa, mostrarla solo en la tarjeta completa
  o acortar las etiquetas (`BurnGuide.SideLabel`).
- **Perilla y llaves que se aprietan** (sección 29, solo compilado): con el
  simulador y en el Quest, con controles y con la pinza, que se accionen al
  apretar y no al pasar la mano, que con las dos llaves cerca se abra la
  que se apunta y que el alcance (8 cm, `pressReach`) no sea muy corto.
- **Llaves con su mano bajo el chorro** (sección 30): en el simulador y en
  el Quest, abrir la caliente con la fría abierta y cerrar la fría mientras
  ella tiene la mano en el agua. Si no responde, mirar la línea
  `[HandPress]` de la consola (dice a cuántos cm se apretó).
- **Manta abierta por delante** (sección 30, solo compilado): con las
  capturas del usuario se vio que la manta se cerraba por delante y tapaba
  sus brazos. Revisar en Play que ahora quede abierta como un chal, que no
  atraviese los brazos y que no tiemble; ajustar `frontOpenHalfWidth` si
  hace falta.
- **Una sola mano en el simulador** (sección 30, solo compilado): A/X con
  el celular en la mano llama; A/X con el rollo corta una tira y la deja en
  esa mano. Menú Hito1 > Práctica (F9 pausa, F10 terminar).
- **Film y bolsa amarillos (a decidir):** son piezas hechas por código y
  se pintaron de amarillo cuando estaban sueltas en el mesón (en blanco no
  se veían). Ahora están en el cajón: se podría volver a un aspecto real
  (film transparente en su caja, bolsa blanca translúcida) o usar modelos
  (H.1).
- **Queja del frío lejos de ella** (sección 29): ir por la manta justo al
  empezar a enfriar y medir si 30 s (`PatientDialog.awayGraceSeconds`)
  alcanzan para volver caminando con el joystick; ver que al no volver se
  sume el error del enfriamiento interrumpido.
- **Botones con el dedo y brazo de la tía** (`QUEMADURA_INMERSION.md`,
  sección 15): con controles y sin ellos, que el círculo del cursor
  coincida con la punta del índice (si no, ajustar
  `SimpleGrab.controllerFingertipOffset`), que el dedo se vea delante de
  la tarjeta y que llevar a la tía del brazo ya no la contorsione.
- Medir **72 fps** con agua, vapor, tela y ampollas (RNF-01).
- Detalles abiertos: algunas tarjetas quedan en parte dentro del meson
  (`PanelPlacement`); ya no desaparecen, pero convendria ubicarlas fuera.
  La tarjeta puede subir por encima de la cabeza de la
  tía cuando ella está justo delante; la olla volcada cae al piso y el
  charco del piso es grande (decidir si se prefiere tumbada sobre la
  cocina).
- Movilidad, si hace falta: fundido al girar, giro continuo, `Fixed
  Timestep` a 1/72 si los objetos tiemblan, cápsula del jugador.
- **Pruebas con usuarios**, modo guiado contra modo evaluación (JSON en
  `Application.persistentDataPath/sesiones_quemadura/`).
- **Informe** (`Informe_Hito_1__.../main.tex`): código de alumno de Sebastián
  (8 dígitos), capturas nuevas, fuente clínica citable (Cruz Roja, "Cool,
  Call, Cover"; guía LSEBN), crédito "Voces: elevenlabs.io" y las
  limitaciones: no tiene expresión facial.
