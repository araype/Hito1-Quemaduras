# Pendientes para la versión final

Hoja de ruta del proyecto (simulación VR de primeros auxilios ante una
quemadura doméstica). Actualizada el 2026-10-20 sobre la rama
`secuencia-quemadura` (commit `8d784f4`).

**Regla:** al terminar un punto, márcalo `[x]` aquí, con la rama y el commit.

## Estado actual

Ya hecho (ver `docs/QUEMADURA_INMERSION.md`):

- [x] Realismo: paciente con texturas, olla, chorro, paño con tela simulada,
  quemadura sobre la piel, mano mojada, respiración, perilla y sonido
  (`quemaduras`, `7d1d9e3` y `17410c2`; sección 1 del doc).
- [x] Experiencia principal: accidente de la olla, hinchazón y ampollas, llave
  fría y caliente, anillo, chompa, film y gasa, remedios caseros y vibración
  (`experiencia-quemadura`, `928cce7`; sección 5).
- [x] Secuencia: protocolo de 7 pasos, reloj de 20 min en 90 s, decisión
  médica, resumen con puntaje y registro JSON de cada sesión
  (`secuencia-quemadura`, `8d784f4`; sección 6).

## 0. Probar en el visor (urgente)

- [ ] **Nada de lo anterior se ha visto todavía en Unity ni en el Quest.**
  Todo se verificó solo compilando fuera del editor. Probar con Quest Link en
  `Escenario_Quemadura` siguiendo las secciones "Probar" del doc.
- [ ] Revisar primero: ubicación de los objetos sobre el mesón
  (`BurnSceneProps`), dirección del vuelco de la olla (`PotAccident`),
  posición de la llave caliente (`LlaveGrifoCaliente`), tamaño del anillo
  (`PatientRing.innerRadius`), pared del reloj (`CoolingClock`) y lectura de
  los paneles.
- [ ] Medir que se mantenga en **72 fps** con agua, vapor, tela y ampollas
  activos (RNF-01).

## 1. Flujo de la aplicación (siguiente bloque de código)

- [ ] **Menú de inicio** (RF-01) con **modo guiado** y **modo evaluación**.
  El modo evaluación apaga los puntos dorados (`WaterTap`, `StoveKnob`,
  `PatientRing`), las pistas de voz (`BurnPatientReactions.voiceHints`) y el
  texto del reloj (`CoolingClock.showLabel`). Guardar el modo en el JSON
  (`BurnProtocol.SaveLog`).
- [ ] **Tutorial** (RF-02) antes de la emergencia: agarrar, girar la perilla y
  la llave, tomar la muñeca. Incluye el aviso de que la escena tiene gritos y
  una lesión visible (RNF-03). El accidente (`PotAccident.accidentDelay`)
  debe empezar después del tutorial.
- [ ] **Barra de 4 actos** (Planteamiento, Desarrollo, Clímax, Resolución)
  con el contador de aciertos y errores. Leer `BurnProtocol.CurrentAct`,
  `BurnSequenceManager.Errors` y `CriticalErrors`.
- [ ] **Pausa y reiniciar** en cualquier momento (RNF-03).
- [ ] **Variación del caso** (RF-11): activar `BurnInjury.randomDegree` y
  variar la extensión de la quemadura.
- [ ] **Manos fantasma** en modo guiado (RF-04). Es lo primero que se puede
  recortar si falta tiempo; si se recorta, quitarlo también del informe.

## 2. Limpieza del proyecto

- [x] Borrar las escenas de atragantamiento y cortes, `Hito1_Quemaduras` y
  `SampleScene`, sus scripts (`Choking*`, `Cut*`, `BleedingEffect`,
  `PoseAtragantamiento`, `WoundContactDetector`, `ManoHeridaVisibility`,
  `SceneCycler`), los modelos de `Modelos/Cortes` y los materiales de comida
  que solo usaban esas escenas. La escena principal pasó a llamarse
  `Escenario_Quemadura` (mismo GUID) y es la única del build, ahora activada
  (antes estaba desactivada y la activa era la de atragantamiento)
  (`secuencia-quemadura`).
- [ ] Abrir el proyecto en Unity y confirmar que la consola no muestra
  errores ni scripts faltantes en `Escenario_Quemadura`.

## 3. Contenido

- [ ] Grabar las **voces reales** de la paciente y reemplazar las de TTS
  (21 archivos `voz_*.wav` en `Assets/Hito1/Resources/Hito1Audio`, mismo
  nombre). Guía en la sección 1 del doc.

## 4. Evaluación e informe

- [ ] **Pruebas con usuarios**, comparando el modo guiado con el modo
  evaluación. Los JSON quedan en
  `Application.persistentDataPath/sesiones_quemadura/`.
- [ ] **Actualizar el informe** (`Informe/`):
  - sección 4.3: describir lo implementado y agregar capturas nuevas;
  - secciones 4.3 a 4.5 y RNF-02: el proyecto usa el **Meta XR Interaction
    SDK** y `SimpleGrab`, no XR Interaction Toolkit (`XR Grab Interactable`,
    `Direct Interactor`, `Teleportation Provider`);
  - la quemadura se cubre con **film o gasa**; el paño es un error;
  - el modelo de la paciente (Mixamo Ch07) **no tiene blendshapes**: quitar
    la expresión facial y explicar que la hinchazón se hace escalando el
    hueso de la mano;
  - Cuadro 4: quitar "latido" y agregar los sonidos sintetizados y las voces
    TTS creados por el equipo;
  - Cuadro 5: corregir el cronograma;
  - verificar que exista la referencia [2] (Rajan et al., IEEE TVCG 2021) y
    citar o quitar el dato del 80 % de la sección 2.1;
  - el código de alumno de Sebastián en la portada tiene 8 dígitos.
