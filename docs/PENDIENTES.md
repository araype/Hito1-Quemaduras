# Pendientes

Única lista de lo que falta. Actualizada el 2026-11-05 sobre `corregir-bugs`
(sale de `rediseno-tarjetas`, `728eeba`). Lo ya hecho está descrito en
`docs/QUEMADURA_INMERSION.md` y en el historial de git.

**Regla:** al terminar un punto, márcalo `[x]` con la rama y di si se probó en
el Quest, en el editor o si solo se compiló.

Fuentes de los bugs: `Bugs_Experiencia_VR_Quemaduras.pdf` (raíz, sin versionar;
prueba en Quest 2 del 2026-11-02 sobre `rediseno-tarjetas`, 38 hallazgos). Los
números de captura se refieren a ese PDF.

## Estado de los bugs del PDF

Los 38 hallazgos tienen arreglo en `corregir-bugs` (2026-11-05), salvo dos
limitaciones (C). **Probado en el editor** (Play con Quest Link, acciones
simuladas por código y capturas); **falta jugarlo con las manos en el Quest**,
sobre todo lo marcado "(visor)", que depende de agarrar de verdad.

## Orden de trabajo

1. **Probar en el Quest** todo lo de A, B y C. Anotar aquí lo que siga mal.
2. Modo evaluación sin puntos dorados (E.5).
3. Variación del caso + evaluar la quemadura + decisión de tres opciones
   (E.1 y E.2).
4. Resistencia de la paciente al enfriar (E.3).
5. El porqué en el resumen (E.4).
6. Chompa con tela simulada y film en tiras (D.2 y D.3).
7. Resto de E, contenido (F) y pruebas e informe (G).
8. Manos fantasma (RF-04): recortar y quitarlo del informe.

## A. Bugs graves y medios

1. [x] **Mover la olla antes de tiempo arruinaba todo** (capturas 9, 10 y
   12). Antes del accidente la olla queda fija sobre el quemador
   (`PotAccident.lockUntilAccident`): kinemática, sin sus agarres de Meta y,
   si algo la mueve, vuelve sola. Los objetos quietos en el piso vuelven a su
   lugar a los 2 s (`Grabbable.returnFromFloor`; no aplica a la cuchara ni al
   paño). Después de volcarse la olla cae al piso junto a la tía, como estaba
   diseñado; si se prefiere tumbada sobre la cocina, es un cambio chico.
2. [x] **Brazo de la tía** (capturas 3, 4 y 13) (visor). Su mano sigue a la
   del jugador como un resorte (`Grabbable.followSharpness`,
   `maxFollowSpeed`); si se tira a más de 0,6 m se suelta y ella se queja
   (`SimpleGrab.OnPulledAway`); no cruza al otro lado del cuerpo
   (`PatientBody.crossBodyLimit`); si se la sostiene cerca del chorro 0,8 s,
   ella misma pone la mano bajo el agua (`PatientBurnArm.placeInWaterAfter`,
   `assistRadius` 0,42); al soltarla vuelve antes a proteger la mano
   (`holdAfterRelease` 1,2 s). Probado en el editor: el límite del cuerpo y
   la mano al pecho tras quemarse.
3. [x] **La tía tapaba la hornilla** (captura 3). Al quemarse retrocede 0,5 m
   (`PatientBurnArm.stepBackAfterBurn`, `PatientReach.WalkTo`): la perilla
   queda a 0,67 m de ella (antes 0,18 m).
4. [x] **Dos objetos a la vez con una mano** (captura 5) (visor). La mano que
   sostiene algo con `SimpleGrab` apaga sus agarres de Meta, y si el de Meta
   sostiene algo, `SimpleGrab` no agarra (`SimpleGrab.SyncMetaGrab`,
   `MetaHolding`).
5. [x] **Agarre distante** (captura 7) (visor). Al agarrar, el objeto se
   acerca hasta tocar la mano (`SimpleGrab.snapToHand`) y se apagó el agarre
   a distancia de Meta en las manos (`disableDistanceGrab`; era por donde se
   tomaba la olla desde lejos).
6. [x] **Llama** (capturas 5 y 7). La perilla se cambiaba con cada toque: un
   segundo toque la volvía a prender, pero el paso ya contaba. Ahora, una vez
   apagada, no se prende (`StoveKnob.stayOffOnceOff`), y no responde antes
   del accidente (`onlyAfterBurn`). Llama más grande y brillante.
7. [x] **Chompa** (capturas 6 y 7). Más ancha y alta (cubre los hombros), los
   vértices no entran en las cápsulas del cuerpo en cada cuadro
   (`SweaterDrape.PushOut`) y las mangas caen por delante, sobre el pecho.
8. [x] **Menú y decisión médica con el estilo nuevo** (capturas 1 y 8):
   `RoundedPanel` y botones tipo píldora. El menú es un 20 % más chico.
9. [x] **Límite del área de juego** (captura 11). `PlayArea` (zona de la
   cocina con margen de 0,9 m) recorta el paso del jugador
   (`VRLocomotion`) y de la tía (`PatientReach`). La salida al comedor queda
   fuera (x > -0,34).
10. [x] **La tarjeta tapaba el reloj** (captura 6): ver B.
11. [x] **Aspas negras en la taza** (capturas 2 y 3): el café y la tapa de la
    taza estaban a la misma altura y se peleaban al dibujarse. El café sube
    1 mm.
12. [x] **La quemadura parecía un guante rojo** (captura 3 y notas). Shader
    nuevo `Hito1/PielQuemada` (usa el color de cada vértice como máscara) y
    `BurnSkinOverlay.BuildMask`: al quemarse, más intensa en lo que miraba
    hacia arriba, manchas irregulares, borde difuso hacia la muñeca, centro
    más oscuro y brillante. Menos brillo de plástico (0,2).

## B. Paneles: dónde aparecen

- [x] Regla única `PanelPlacement` (script nuevo): de frente a los ojos
  (también en altura), y entre 9 posiciones cerca del centro de la vista
  elige la que no tapa la cabeza, el pecho ni la mano quemada de la tía, el
  reloj ni los brillos del paso. La usan la tarjeta de la guía, el menú, la
  decisión médica y el resumen (`PanelFollow`, que no se mueve mientras una
  mano está delante apretando un botón). Cubre: menú inclinado y tapando a la
  tía (captura 1), tarjeta sobre torso o cabeza (4 y 11), decisión médica
  tapando a la tía (8), panel de canto (9) y reloj tapado (6).
- [x] Tarjeta alta, sobre los gabinetes (captura 2): baja al mirar el mesón.
  Ojo: si la tía está justo delante, la tarjeta puede subir por encima de su
  cabeza para no taparla.
- [x] "Ir al centro de salud" en 3 líneas (captura 8): `FitLabel` parte la
  etiqueta en dos en el espacio más cercano al medio.
- [x] Tarjeta del anillo más alta (captura 5): texto más corto. La voz
  `guia_anillo` (TTS) sigue diciendo el texto anterior; regrabarla.

## C. Pulido visual

- [x] Brillo de la taza casi opaco (captura 2): ahora más tenue.
- [x] El paño atravesaba el borde del mesón (captura 5): la tela choca con la
  escena también en reposo (`TowelCloth`) (visor).
- [x] Disco gris junto a la mano y charcos lejos del lavadero (captura 13):
  eran el charco y el vapor del accidente, que ocurría donde se había
  llevado la olla. Resuelto con A.1.
- [x] La tía de espaldas: mientras protege la mano se gira hacia el jugador
  (`PatientReach.FaceTo`).
- Limitación: la mano del jugador atraviesa a la tía (captura 6). Las manos
  siguen al visor y no se pueden frenar; tocarla vibra.
- Limitación: sin expresión facial (Ch07 no tiene blendshapes). Decirlo en
  el informe.

## D. Objetos sobre la paciente

1. [x] **Aspecto de la quemadura:** hecho en A.12. Si se quiere más, una
   textura de piel quemada propia en `Piel_Quemada`.
2. [ ] **Chompa con tela simulada:** ya no entra en el cuerpo (A.7); falta
   tela Verlet como `TowelCloth` con las 14 cápsulas de
   `PatientPhysics`. Se toma de los hombros y se deja caer desde atrás. Si no
   da 72 fps, copiar la piel de hombros y espalda como `HandSkinMesh`.
3. [ ] **Film en tiras:** una mano sostiene el rollo y la otra tira; cada
   tira cubre una franja (`HandSkinMesh.Build`). Cubierta con ~80 % de la
   zona. A lo largo y suelto: si se envuelve apretado, ella se queja y es
   error. Opción correcta extra: bolsa plástica limpia.
4. [ ] **Gasa:** apoyarla sin frotar ni apretar.

## E. Arco del juego

Riesgo: se puede memorizar la secuencia sin entender el porqué.

1. [ ] **Variar el caso** (RF-11): grado (`BurnInjury.randomDegree`),
   extensión, zona y quizá edad.
2. [ ] **Evaluar la quemadura y decidir:** ampollas y tamaño comparado con la
   palma; luego casa, centro de salud o emergencias (SAMU 106).
3. [ ] **Resistencia de la paciente:** hacia el minuto 8 a 10 quiere sacar la
   mano por el frío y hay que convencerla.
4. [ ] **El porqué en el resumen:** una línea por error en la sección "qué
   mejorar" del resumen nuevo, y ofrecer un segundo caso.
5. [ ] **Modo evaluación sin puntos dorados** (`WaterTap`, `StoveKnob`,
   `PatientRing`).
6. [ ] **Llenar los 20 minutos:** anillo, chompa, calmarla, evaluar.
7. [ ] **Pedir ayuda** a alguien de la casa.
8. [ ] **Seguridad propia:** tocar la olla o pisar el líquido es error.
9. [ ] **Barra de 4 actos** con aciertos y errores (`BurnProtocol.CurrentAct`,
   `BurnSequenceManager.Errors`, `CriticalErrors`).
10. [ ] **Pausa y reiniciar** en cualquier momento (RNF-03).
11. [ ] Opcional: practicar una perilla o una llave en el tutorial.

Los valores nuevos de `BurnEvent` y demás enums van **al final**.

## F. Contenido

- [ ] Voz real del narrador (21 `guia_*.wav`) y de la paciente (`voz_*.wav`),
  reemplazando las TTS con el mismo nombre.

## G. Rendimiento, pruebas e informe

- [ ] Medir **72 fps** en el Quest con agua, vapor, tela y ampollas (RNF-01).
- [ ] Movilidad, si hace falta tras probar: fundido al girar, giro continuo,
  `Fixed Timestep` a 1/72 si los objetos tiemblan, cápsula del jugador.
- [ ] **Pruebas con usuarios**, modo guiado contra modo evaluación (JSON en
  `Application.persistentDataPath/sesiones_quemadura/`).
- [ ] **Informe** (`Informe_Hito_1__.../main.tex`): código de alumno de
  Sebastián (8 dígitos), capturas nuevas, fuente clínica citable (Cruz Roja,
  "Cool, Call, Cover") y la limitación de la expresión facial.
