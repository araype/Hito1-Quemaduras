# Hito 2: integración, interacciones y poses de mano

Rama: `hito2-integracion`. Une `fix/auditoria-locomocion-manos` (que ya
incluye `main`), `escena_cortes` y `hito1-sustentacion`.

Según el cronograma del informe, el Hito 1 pedía los tres escenarios (cocina,
comedor y encimera) con agarre funcional, y el Hito 2 pide **agarre completo
en los tres escenarios (paño, gasa, olla) y poses de mano (Hand Grab Pose)**.

> **Límites de esta entrega.** En la máquina donde se hizo no hay Unity ni
> visor. Todo se verificó fuera del editor (sección 6). **Nada se ejecutó en
> Unity ni en el Quest**: hay que probarlo en el visor siguiendo la sección 5.

## 1. Las tres escenas en un solo build

`Build Settings` tiene, en el orden del informe:

| # | Escena | Caso |
|---|---|---|
| 0 | `Escenario_Hito1` | Quemadura (cocina) |
| 1 | `Escenario_Atragantamiento` | Atragantamiento (comedor) |
| 2 | `Escenario_Cortes` | Corte (encimera) |

Para pasar al siguiente escenario: **botón de menú del control izquierdo**
(≡), o el gesto de menú de la mano izquierda. En el editor también sirve la
tecla **N**. Es provisional hasta la pantalla de inicio (Hito 5).

## 2. Agarre con poses de mano (Hand Grab Pose)

`HandGrabPoseSetup` arma en runtime el agarre del Interaction SDK de Meta con
poses de mano sobre cada objeto:

| Objeto | Escena | Tipo de agarre |
|---|---|---|
| Paño (`panuelo`) | Quemadura | Pinza: pulgar e índice pinzan el grosor en cada borde |
| Olla (`Ollaprovisional`) | Quemadura | Pinza de borde: pulgar por fuera e índice por dentro de la pared |
| Gasas, guantes | Corte | Pinza |
| Antiséptico | Corte | Cilindro (mano envolviendo el frasco) |
| Venda | Corte | Cilindro alrededor del eje del rollo |

- Los dedos salen de poses **grabadas por Meta** en los ejemplos del SDK
  (`Torch` para el cilindro, `Key` para la pinza). `HandGrabPoseLibrary.cs`
  las tiene normalizadas.
- La posición de la mano se calcula con el **tamaño real** de cada objeto
  (sus colliders). Por cada mano se crean varias poses (cada borde, arriba y
  abajo, los dos sentidos del cilindro) y el SDK usa la más cercana a la mano
  real.
- Con **hand tracking** la mano adopta la pose al agarrar. Con controles, el
  rig *Legacy* solo trae `ControllerGrabInteractor` (agarra, pero sin pose);
  si `HandsControllersUnifier` logra activar las manos conducidas por el
  control, también se ve la pose con los controles en la mano.
- Las partes del paciente (la mano quemada, la mano herida, el brazo con
  `GrabRedirect`) las sigue agarrando `SimpleGrab`, con física e IK.
  `SimpleGrab` ya **no** toma los objetos que maneja el SDK de Meta, para que
  los dos sistemas no peleen por el mismo objeto, y las esferas de sus manos
  no los empujan.

**Respaldo:** si en el visor el agarre del SDK no respondiera, activa
`SimpleGrab > Grab Interaction Sdk Objects` en el jugador. `SimpleGrab`
vuelve a agarrar todo (sin poses).

**Ajustes en el inspector** (`HandGrabPoseSetup`): `Grip` (Pinza, Cilindro,
PinzaBorde), `Cylinder Axis` (MasLargo, MasCorto, X, Y, Z) y `Pinch Inset`
(cuánto hacia adentro del borde va la pinza). Para refinar una pose a mano,
usa las herramientas de grabación de poses de Meta sobre el objeto y quita
`HandGrabPoseSetup`.

## 3. Interacciones nuevas

### 3.1 Quemadura: enfriar y cubrir

Estados de `BurnSequenceManager`: `Idle → Burned → Cooling → Cooled → Success`.

1. A los 3 s la mano se quema (`Burned`) y el marcador se pone **rojo**.
2. **Abrir el grifo:** tocar el **punto dorado** sobre la boca del grifo
   (`WaterTap`). Se vuelve azul y sale el agua. Volver a tocarlo lo cierra.
3. **Enfriar:** llevar la mano del paciente (agarrándola con `SimpleGrab`)
   bajo el chorro. Mientras está bajo el agua el marcador se ve **azul**
   (`Cooling`). A los **10 s seguidos** queda enfriada (`Cooled`, **rosado**).
   Si se saca antes, vuelve a `Burned` y el tiempo empieza de cero. Con el
   grifo cerrado no cuenta. (En la vida real son 10–20 minutos; aquí se
   comprime.)
4. **Cubrir:** apoyar el paño sobre la mano **3 s** → `Success` (**verde**).
   El paño antes de enfriar no cuenta.

### 3.2 Atragantamiento: maniobra de Heimlich

`ChokingSequenceManager`, estados:
`Choking → RescuerBehind → HandsPlaced → Success` (o `Failed`).

1. A los 2 s el paciente se atraganta. Una **marca en el piso** indica dónde
   pararse (detrás del paciente) y una **esfera dorada** marca el abdomen.
2. Ubicarse **detrás** del paciente (a menos de 0,9 m).
3. Poner **las dos manos en la esfera** (entre el ombligo y el esternón). Con
   controles hay que apretar el **agarre** (puño). La esfera se pone **verde**.
4. Hacer **5 compresiones** rápidas **hacia adentro y hacia arriba**. Cada una
   empuja un poco al paciente. A la quinta sale el objeto y el paciente se
   endereza (`Success`).
5. Si pasan **120 s** sin lograrlo: `Failed` (esfera roja).

La esfera y los huesos se calculan en runtime con el esqueleto del personaje.
El umbral de compresión es de 0,40 m/s en la dirección adentro-arriba
(`Thrust Speed`).

### 3.3 Corte

No cambió su lógica (`CutSequenceManager`). Solo se agregaron las poses de
mano y la **llave del grifo** (antes el agua corría siempre). Para lavar la
herida hay que abrir el grifo tocando el punto dorado.

## 4. Correcciones de la revisión de la auditoría

- **Colliders automáticos** (`AutoColliders` y la herramienta de editor):
  - Usan `MeshCollider` con la forma real siempre que se puede: en el editor
    siempre, y en runtime si la malla es legible (paredes y pisos del kit lo
    son). Así ya no se tapan puertas ni ventanas con una caja maciza.
  - En runtime, a las mallas no legibles se les pone caja solo si miden hasta
    2,5 m y no parecen puerta, marco o ventana.
  - Solo se saltan los objetos que ya tienen collider propio o que son parte de
    un objeto con física, en vez de saltarse ramas enteras por un collider en
    un padre.
  - `NoAutoCollider` excluye los indicadores visuales.
- **Cápsula del jugador** (`VRLocomotion`): cuando el jugador camina en su
  espacio real, el cuerpo se mueve con `controller.Move` (respeta paredes) y
  el rig se corre al revés. La cápsula ya no puede quedar metida en una pared.
- **Gestos de locomoción con la mano** (pellizco de medio y anular): se
  ignoran mientras esa mano sostiene algo o pellizca con el índice, para no
  caminar sin querer al agarrar.
- `HandsControllersUnifier` solo apaga componentes **del jugador**, no de otros
  personajes.
- `Hito1RuntimeBootstrap` se instala en **cada** escena que se carga (antes
  solo en la primera) y ya no se salta `AutoColliders`.
- `ManoQuemadaVisibility` muestra la mano en cualquier estado que no sea
  `Idle` y también apaga y enciende los colliders hijos (el trigger de la
  quemadura).
- `Escenario_Atragantamiento`: `SimpleGrab` tenía los campos de la versión
  vieja. Se actualizó al formato nuevo (como `Escenario_Hito1`).

## 5. Cómo probar en el visor

1. Abrir el proyecto en Unity 2022.3.62f3 y esperar a que compile.
2. (Recomendado) En cada escena: `Hito1 > Agregar colliders faltantes
   (escena abierta)` y guardar.
3. Play o build al Quest. En la consola deben aparecer
   `[Hito1] Manos+controles unificados...` y `[Hito1] Colliders automaticos...`.
4. **Quemadura:** hacer la sección 3.1 completa. Revisar en consola
   `[BurnSequence] Estado -> Cooling / Cooled / Success`.
5. **Poses:** con hand tracking, agarrar la olla (borde), el paño (borde) y,
   en Corte, las gasas, los guantes, el antiséptico y la venda. La mano debe
   adoptar la pose y quedar sobre el objeto. Probar también con controles.
6. **Atragantamiento** (≡ para cambiar de escena): hacer la sección 3.2.
   Revisar `[Choking] Compresion n/5` y `[Choking] Estado -> Success`.
7. **Corte:** abrir el grifo antes del paso de lavado.
8. Si algo se comporta raro con el agarre, probar el respaldo de la sección 2.

Valores que probablemente haya que ajustar tras probar: `Thrust Speed` y
`Zone Radius` (Heimlich), `Required Seconds` (enfriado), la posición de
`LlaveGrifo` (hija de `Aguadegrifo`) si el punto dorado no cae sobre la
llave del modelo, y `Pinch Inset` de cada objeto.

## 6. Qué se verificó (sin Unity)

- **Compilación** de todo el proyecto y de sus paquetes con Roslyn (C# 9,
  como Unity 2022.3), contra las DLL de **Unity 2022.3.62** y el código del
  **Meta XR SDK 201.0.0**. Se respetaron los `asmdef`, `versionDefines` y
  `defineConstraints` reales. Esto incluye `ISDK_OPENXR_HAND`, que en el SDK 201
  siempre está definido, por eso las poses usan el esqueleto OpenXR. Resultado:
  26 de 26 ensamblados compilan, incluidos `Assembly-CSharp` y
  `Assembly-CSharp-Editor`.
- **Escenas:** un validador revisó que todas las referencias internas existan,
  que padres e hijos, componentes y raíces sean consistentes, que los
  componentes agregados a prefabs estén registrados, que los scripts existan y
  que cada campo serializado exista en su clase. Pasan las tres escenas. Con
  errores introducidos a propósito, el validador los detecta.
- **Poses:** con cinemática directa sobre el esqueleto OpenXR del SDK
  (verificada primero con la pose original de la linterna, cuyos dedos quedan
  a 2,2–2,6 cm del eje de un mango de 2 cm), se comprobó dónde quedan los
  dedos en cada objeto real:
  - Gasa: pulgar e índice a 0,2–2 mm de las caras.
  - Olla: el pulgar justo por fuera de la pared y el índice por dentro.
  - Antiséptico y venda: las yemas a menos de 4,5 mm de la superficie.
  - Guantes y paño: se hunden 4–9 mm, porque son blandos y más gruesos que la
    pinza.
- **Lógica:** simulación cuadro a cuadro (72 fps) del enfriado, el paño y las
  compresiones, incluido el ruido de tracking de hasta 4 mm. Pasan 15 casos.

## 7. Pendientes

1. **Probar en el visor** (sección 5).
2. **Informe:** las secciones 4.3–4.5 y el RNF-02 dicen XR Interaction
   Toolkit, `XR Grab Interactable`, `Direct Interactor` y `Teleportation
   Provider`. El proyecto usa el **Meta XR Interaction SDK** y locomoción con
   joystick. Hay que corregirlo en el informe del Hito 2.
3. Retroalimentación por voz, sonido y háptica, e indicador de tiempo y fuerza:
   es el Hito 3. `WaterTap` ya acepta un `AudioSource` para el sonido del agua.
4. `BodyReachFollow` sigue sin respetar colisiones (ver `AUDITORIA.md`).
