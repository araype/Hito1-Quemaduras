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

- Volvió la gasa (sección 16 de `QUEMADURA_INMERSION.md`): `guia_cubrir`,
  `guia_err_pano` y `guia_err_envolver` ya son de ElevenLabs. Quedan 12 voces
  TTS por regenerar (`docs/VOCES.md`); mientras tanto no suenan: se usa
  una frase de ElevenLabs que encaja o solo el texto (`Hito1FX.TtsStandIns`).

- **Regenerar con ElevenLabs las voces que llegaron como TTS** desde
  `corregir-bugs` (narrador Raúl y tía Sabina), para que no cambie la voz a
  mitad de la partida, y `op_centro`, que todavía dice "gasa". Lista y
  textos en `docs/VOCES.md`, sección "Pendientes de regenerar".
- `guia_anillo` no coincide del todo con el texto de la tarjeta.

## Plan: orden para lo que falta

| # | Pedido | Por qué en este orden | Tamaño |
|---|---|---|---|
| 0 | Probar la consolidación en el editor (sección 0) | Sin esto lo demás se construye sobre algo no probado. | Mediano |
| 1 | Regenerar voces (sección 1) | Se puede hacer en paralelo. | Chico |
| 2 | Variar el caso (E.1) | Que no se pueda memorizar la secuencia. El tamaño de la quemadura cambia cuánto film hace falta. | Grande |
| 3 | **Chompa con tela simulada** (D.2) | Hoy se ve aceptable y es lo que más arriesga los 72 fps. Si no hay tiempo, queda como está. | Grande |
| 4 | Línea de tiempo en el resumen; distractores; práctica de perilla en el tutorial (opcional) | Mejoras de la experiencia. | Variable |
| 4b | Probar la tía de Avaturn en el Quest; poses de mano y háptica (F) | Lo que más suma a la inmersión según la revisión de herramientas. | Grande |
| 5 | **Validación final en el Quest** con las manos y fps (G); pruebas con usuarios e informe | Al final, con todo estable. | Mediano |
| — | Manos fantasma (RF-04) | Recortar y quitarlo del informe. | — |

**Riesgos**

- Rendimiento: el film en tiras y la chompa con tela son lo único que puede
  bajar de 72 fps. Medir en la validación final.

## D. Objetos sobre la paciente

- **Chompa con tela simulada (D.2):** ya no entra en el cuerpo; falta tela
  Verlet como `TowelCloth` con las 14 cápsulas de `PatientPhysics`. Se toma de
  los hombros y se deja caer desde atrás. Si no da 72 fps, copiar la piel de
  hombros y espalda como `HandSkinMesh`.
- Cubrir con la bolsa todavía es "soltarla sobre la mano". El film ya se
  apoya en tiras; si se quiere, la bolsa podría usar el mismo "mantener
  encima 1 s" con `HoldHint` que tenía la gasa de Araype.

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
  triángulos y el shader de glTFast) y que se vean bien de cerca la cara, el
  pelo y las manos; en el editor, el resto del caso con el modelo nuevo
  (agua, gasa, film, chompa, sacar el anillo, llevarla del brazo,
  caminar). Mide unos 11 cm más que la de Mixamo: si choca con la campana o
  los muebles, bajar la escala.
- **Usar más del Interaction SDK de Meta (evaluar):** poses de mano al
  agarrar (`HandGrabInteractable` con pose: la mano rodea la taza, el
  celular, el rollo de film en vez de quedar abierta) y probar el
  `PokeInteractable` contra nuestro `TouchButton`, solo si se ve mejor.
- **Háptica:** hoy vibran solo dos casos. Con controles, vibrar al tocar
  la olla, el agua fría, los botones y la mano de la tía. Con las manos no
  hay vibración: reforzar con sonido y brillo.
- **Video en pantalla (a decidir):** en el resumen o el tutorial, un video
  corto de la técnica real (enfriar 20 min, cubrir con film) en una
  pantalla de la cocina o en el celular. Debe tener respaldo de las guías
  (ver Regla).
- **Multijugador (fuera de alcance, a decidir):** modo instructor que mira
  y comenta la sesión (Netcode for GameObjects). Grande; solo si el curso
  lo pide.

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
  (llevar el celular a la oreja) y **sacar el anillo** con la mano ofrecida.
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
