# Auditoría del proyecto Hito1-Quemaduras

Rama: `fix/auditoria-locomocion-manos` (sobre `main`, commit `9ddb922`).

> **Alcance y límites.** Esta auditoría es **estática**: leí los scripts, la
> escena `Escenario_Hito1.unity`, los prefabs del kit y el código fuente del
> Meta XR SDK 201.0.0. **No se ejecutó en Unity ni en un visor**, porque en
> esta máquina no hay Unity Editor. Lo que dice "causa probable" es una
> inferencia a partir del código y debe confirmarse en el visor. El código
> nuevo compila contra el API verificado del SDK, pero no se probó en runtime.

## 1. Estado de cada pieza

| Pieza | Estado | Detalle |
|---|---|---|
| Escena de build | OK | Solo `Escenario_Hito1` está en Build Settings. `Hito1_Quemaduras.unity` y `SampleScene.unity` no se usan. |
| `BurnSequenceManager` | **Parcial** | Pasa a `Burned` a los 3 s. Los estados `WaitingWater`, `Cooled`, `WaitingCloth`, `Success`, `Failed` existen pero **nada los dispara**: la secuencia agua → pañuelo no está implementada. |
| `ManoQuemadaVisibility` | Corregido | Si se habilitaba después de `Burned` se quedaba oculta para siempre. Ahora lee el estado actual al habilitarse. |
| IK del brazo (`RightArm_IK`, Animation Rigging) | OK en configuración | `TwoBoneIK` con raíz/medio/punta y target `ManoQuemadaTarget` bien referenciados. |
| `BodyReachFollow` | Funciona, con reserva | Empuja al personaje hacia el objetivo **sin pasar por colisiones**: puede atravesar muebles. No se tocó. |
| `SimpleGrab` | Corregido | Ver sección 3. |
| `VRLocomotion` | Reescrito | Ver sección 2. |
| Colliders del apartamento | **Deficiente** | Ver sección 2.3. |
| Agarre con Interaction SDK (`HandGrabInteractable` en olla y pañuelo) | No verificable sin visor | Convive con `SimpleGrab`; ver sección 4. |

## 2. Los cuatro problemas reportados

### 2.1 "Los controles solo se mueven hacia atrás"

**Causa probable.** El prefab `OVRCameraRigInteraction` (versión *Legacy*)
incluye la locomoción de Meta: teletransporte con el joystick (hacia
adelante), **paso (`ControllerStep`, conectado a `StepBackward`)** y giro con
los lados. El teletransporte necesita superficies `TeleportInteractable`, y
la escena no tiene ninguna, así que hacia adelante no pasa nada y hacia atrás
sí se da el paso. Además esa locomoción tiene su propio `CharacterController`
y movía otro transform (`OVRCameraRig`) distinto al que movía `VRLocomotion`
(la raíz), así que ambos sistemas se pisaban.

**Corrección.** `HandsControllersUnifier` apaga en runtime todos los
componentes del namespace `Oculus.Interaction.Locomotion` y cualquier
`CharacterController` que no sea el de `VRLocomotion`. El movimiento queda en
un único sistema (`VRLocomotion`).

### 2.2 "La cámara se mueve hasta cierto punto"

**Causas probables** (pueden sumarse):

1. La cápsula del `CharacterController` estaba fija en el centro del rig
   (`center = (0, 0.9, 0)`), no donde está la cabeza. Si el jugador se
   desplaza físicamente, la cápsula choca contra paredes y muebles en un
   punto distinto al que ve.
2. La cápsula duplicada de la locomoción de Meta (sección 2.1) colisionaba
   con la propia.
3. Faltan colliders (sección 2.3); donde no hay piso el jugador cae.

**Corrección.** `VRLocomotion` sincroniza cada frame la cápsula con la
cabeza (posición y altura reales) y ya no depende de `Camera.main` (usa
`OVRCameraRig.centerEyeAnchor` y, si no está, cae a `Camera.main`). Se
agregó giro a saltos con el joystick derecho, porque al apagar el giro de
Meta alguien tiene que hacerlo.

### 2.3 "Muchas cosas no tienen collider"

**Medición.** En los 314 prefabs del kit con mallas: 1762 `MeshRenderer`
contra 885 colliders; 27 prefabs no tienen ninguno. Solo `Structure 01`
(el edificio) tiene 670 renderers y 350 colliders. En la escena, los
objetos propios son 2 renderers y 2 colliders.

**Corrección en dos capas.**

- `AutoColliders` (runtime, automático): a cada malla sin collider propio ni
  heredado le agrega un `BoxCollider` ajustado, con grosor mínimo de 5 cm
  para planos. Las mallas de más de 6 m reciben `MeshCollider` solo si la
  malla es legible; si no, se omite (una caja gigante encerraría al
  jugador) y queda un aviso en consola.
- `Hito1 > Agregar colliders faltantes (escena abierta)` (editor): hace lo
  mismo de forma **permanente** en la escena, con `MeshCollider` para las
  mallas grandes aunque no sean legibles (se cocinan en el build). Es la
  opción recomendada: ejecútala una vez, guarda la escena y el respaldo de
  runtime deja de tener trabajo.

### 2.4 Manos y controles como dos cosas separadas

**Causa.** `OVRHand` tenía `m_showState = ControllerNotInHand`: la mano
solo se dibuja cuando se sueltan los controles, y sin controles no hay
joystick. Por eso o tenías manos y no podías avanzar, o tenías controles.

**Corrección** (`HandsControllersUnifier`):

- Activa `OVRManager.controllerDrivenHandPosesType = Natural`: el sistema
  entrega poses de mano **mientras sostienes los controles**; los dedos
  siguen los botones y los joysticks siguen funcionando.
- Fuerza `OVRHand.m_showState = Always` y oculta el modelo del control.
- `VRLocomotion` y `SimpleGrab` aceptan además solo-manos como respaldo:
  - Mano izquierda: pellizco de **dedo medio = avanzar**, **anular = retroceder**.
  - Mano derecha: pellizco de **medio = girar a la derecha**, **anular = a la izquierda**.
  - Agarre: gatillo/agarre, o pellizco de **índice**.
  - Estos gestos solo se activan cuando no hay controles activos, para que
    sostener un control no cuente como pellizco.

## 3. Otros defectos corregidos

- `SimpleGrab` calculaba la pose del control con `rigRoot.TransformPoint`
  usando la raíz del rig. El control se reporta en el espacio de tracking,
  no en el de la raíz, y la escena tiene overrides sobre objetos internos del
  rig (uno rotado ~31° y desplazado 0.2 m, otro desplazado 1.2 m; no pude
  identificar cuáles sin abrir Unity). Con cualquier desfase así el objeto
  agarrado queda corrido respecto a la mano. Ahora usa las anclas
  `leftHandAnchor`/`rightHandAnchor`, que ya están en espacio mundo.
- Al soltar, `SimpleGrab` dejaba `isKinematic = false` en todo, incluido el
  objetivo del IK (que debe seguir cinemático). Ahora restaura el estado
  original y le da al objeto la velocidad de la mano al soltarlo.
- Una misma pieza ya no puede ser agarrada por las dos manos a la vez.

## 4. Pendientes y riesgos (no resueltos)

> **Actualización (rama `hito2-integracion`, ver `HITO2.md`):** el punto 2
> quedó resuelto (cada objeto lo agarra un solo sistema) y el 3 también (la
> secuencia agua → paño está implementada). También se corrigieron los
> colliders que tapaban puertas, la cápsula que podía quedar dentro de una
> pared y el resto de observaciones de la revisión. Siguen abiertos el 1, el
> 4, el 5 y el 6.

1. **Probar en el visor** todo lo anterior; en particular que
   `controllerDrivenHandPosesType` dibuje las manos en tu dispositivo
   (depende de la versión del sistema del Quest) y que el apagado de la
   locomoción de Meta no deje componentes huérfanos con errores en consola.
2. **Agarre doble**: la olla y el pañuelo tienen `HandGrabInteractable` de
   Meta *y* `SimpleGrab` los puede tomar. Si se ven comportamientos raros al
   agarrar, hay que dejar uno solo. `SimpleGrab` es el que ya funcionaba.
3. **Secuencia de quemadura** incompleta (sección 1): falta implementar las
   transiciones por agua y pañuelo.
4. **`BodyReachFollow`** ignora colisiones (sección 1).
5. **Escenas sobrantes** (`Hito1_Quemaduras.unity`, `SampleScene.unity`):
   conviene borrarlas o moverlas para no confundirlas con la oficial.
6. **Multimodal real** (una mano con control y la otra sin control) requiere
   `launchSimultaneousHandsControllersOnStartup` en `OVRManager`, que solo
   se lee al arrancar; es un cambio de escena/prefab que no se hizo sin
   poder abrir el editor.

## 5. Cómo verificar

1. Abrir `Escenario_Hito1` en Unity 2022.3.62f3.
2. `Hito1 > Agregar colliders faltantes (escena abierta)`, guardar escena.
3. Play en el visor y revisar la consola: deben aparecer
   `[Hito1] Manos+controles unificados...` y, si no hornearon colliders,
   `[Hito1] Colliders automaticos...`.
4. Comprobar: joystick izquierdo avanza/retrocede/lateral; joystick derecho
   gira; no se atraviesan paredes ni muebles; se ven manos con los
   controles en la mano; agarrar la olla y el pañuelo con gatillo.
5. Dejar los controles y comprobar el modo solo-manos (pellizcos de la
   sección 2.4).
