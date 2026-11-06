# Pendientes

Única lista de lo que falta, sobre `corregir-bugs` (sale de
`rediseno-tarjetas`, que todavía no está unida a `main`). **Solo va lo que
falta:** al terminar un punto, se borra de aquí y se describe en
`docs/QUEMADURA_INMERSION.md` (qué hay y cómo probarlo), diciendo si se probó
en el Quest, en el editor o si solo se compiló.

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

## Plan: orden para lo que falta

| # | Pedido | Por qué en este orden | Tamaño |
|---|---|---|---|
| 2 | Variar el caso + evaluar la quemadura y decidir (E.1 y E.2) | Que no se pueda memorizar la secuencia. El tamaño de la quemadura cambia cuánto film hace falta. Incluye "evaluar" durante los 20 min. | Grande |
| 3 | **Chompa con tela simulada** (D.2) | Hoy se ve aceptable y es lo que más arriesga los 72 fps. Si no hay tiempo, queda como está. | Grande |
| 4 | Voces reales (F); práctica de perilla en el tutorial (E.11, opcional) | Contenido. Las voces se pueden grabar en paralelo. | Variable |
| 5 | **Validación final en el Quest** con las manos y fps (G); pruebas con usuarios e informe | Al final, con todo estable. | Mediano |
| — | Manos fantasma (RF-04) | Recortar y quitarlo del informe. | — |

**Riesgos**

- Rendimiento: el film en tiras y la chompa con tela son lo único que puede
  bajar de 72 fps. Medir en la validación final.
- Una textura propia de piel quemada solo si en el visor la quemadura todavía
  se ve artificial.

## D. Objetos sobre la paciente

- **Chompa con tela simulada (D.2):** ya no entra en el cuerpo; falta tela
  Verlet como `TowelCloth` con las 14 cápsulas de `PatientPhysics`. Se toma de
  los hombros y se deja caer desde atrás. Si no da 72 fps, copiar la piel de
  hombros y espalda como `HandSkinMesh`.

## E. Arco del juego

- **Variar el caso (E.1, RF-11):** grado (`BurnInjury.randomDegree`),
  extensión, zona y quizá edad. Hoy solo el botón "Otro caso" cambia el grado.
- **Evaluar la quemadura y decidir (E.2):** ampollas y tamaño comparado con
  la palma; luego casa, centro de salud o emergencias (SAMU 106). Si se quiere
  llamar al 106, hacer un celular nuevo aquí: llamar es lo correcto solo en
  casos graves (quemadura grande, cara, eléctrica). El tamaño de la quemadura
  ya cambia cuántas tiras de film hacen falta (`FilmCoverage` usa la máscara
  de `BurnSkinOverlay`).
- **Practicar una perilla o una llave en el tutorial (E.11, opcional).**

## F. Contenido

- Voz real del narrador (`guia_*.wav`) y de la paciente (`voz_*.wav`),
  reemplazando las TTS con el mismo nombre. `guia_anillo` ya no coincide con
  el texto de la tarjeta. Las frases TTS nuevas están listadas en
  `docs/QUEMADURA_INMERSION.md`, secciones 9, 11 y 12.

## G. Validación, pruebas e informe

- **Probar en el Quest con las manos** todo lo hecho en el editor. Con más
  atención: brazo de la tía, agarre doble, agarre distante, paño, **tirar
  el film con las dos manos** (sostener el rollo, tomar el borde con la otra
  mano, cortar y soltar la tira a lo largo; solo se probó colocando las
  tiras por código), **templar el agua en el minuto 9** (tocar la llave
  caliente con la fría abierta; el panel tiene ahora 4 respuestas), pisar el
  charco (solo aviso; no se probó), gesto de pausa y que los paneles de la
  tía no estorben mientras se le sostiene el brazo.
- Medir **72 fps** con agua, vapor, tela y ampollas (RNF-01).
- Detalles abiertos: la tarjeta puede subir por encima de la cabeza de la
  tía cuando ella está justo delante; la olla volcada cae al piso (decidir si
  se prefiere tumbada sobre la cocina).
- Movilidad, si hace falta: fundido al girar, giro continuo, `Fixed
  Timestep` a 1/72 si los objetos tiemblan, cápsula del jugador.
- **Pruebas con usuarios**, modo guiado contra modo evaluación (JSON en
  `Application.persistentDataPath/sesiones_quemadura/`).
- **Informe** (`Informe_Hito_1__.../main.tex`): código de alumno de Sebastián
  (8 dígitos), capturas nuevas, fuente clínica citable (Cruz Roja, "Cool,
  Call, Cover"; guía LSEBN) y las limitaciones: la mano del jugador atraviesa
  a la tía y no tiene expresión facial.
