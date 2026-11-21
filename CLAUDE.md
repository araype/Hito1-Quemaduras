# Proyecto VR: primeros auxilios ante una quemadura doméstica

Curso CS2H01 Interacción Humano-Computador (UTEC). Unity 2022.3.62f3, Meta XR
SDK 201 (rig `OVRCameraRigInteraction`), visor Meta Quest con Quest Link.
El proyecto se centra **solo en la quemadura**. Las escenas de
atragantamiento y cortes de la etapa anterior se borraron (siguen en el
historial de git).

## Antes de empezar

- El proyecto está en su **versión final** en `main`. Qué es, cómo
  instalarlo, cómo se juega y la estructura del repositorio: `README.md`.
- Textos de las voces: `docs/VOCES.md` (lo lee `Editor/VisemeTracks.cs`
  para generar los visemas de la tía; no cambiar el formato de sus tablas).
- Los documentos de trabajo de cada etapa (pendientes, bugs, historial de
  cambios por rama) se borraron el 2026-11-21; siguen en el historial de git
  (por ejemplo, `docs/QUEMADURA_INMERSION.md`).

## Cómo está armado

- Escena única (la del build, con luz precalculada):
  `Assets/Hito1/Escenas/Escenario_Quemadura.unity`. La tía de Avaturn ya
  está guardada en ella.
- Lo nuevo se instala al dar Play desde `Hito1RuntimeBootstrap` (solo si la
  escena tiene `BurnSequenceManager`) y no se duplica si ya está en la
  escena. La escena principal se puede modificar si el usuario lo pide.
- `BurnSequenceManager` maneja los estados (Idle → Burned → Cooling → Cooled
  → Covered → Success) y el canal de eventos `OnEvent` (evento, detalle,
  gravedad). Lo nuevo debe avisar por `Report(...)` en vez de llamar
  directamente al puntaje.
- `BurnProtocol` arma los pasos, los errores, el puntaje y el resumen a
  partir de esos eventos.
- La tía: animaciones de Mixamo (`PatientAnimator`, controlador en
  `Resources/Hito1Anim`) para cuerpo, piernas y gestos; `PatientBody` mueve
  los brazos por cinemática inversa cuando van a un objetivo. Al dar Play,
  `PatientModelSwap` cambia el modelo de Mixamo por la tía de Avaturn
  (`Resources/Hito1Avatar/TiaAvaturn`, Humanoid, se rearma con el menú
  Hito1 > Preparar tia de Avaturn) y `PatientFace` mueve su cara.
- Objetos creados por código con `PropKit`; paneles y textos con `VRPanelKit`,
  `RoundedPanel` y `PanelText` (TextMesh Pro, desde `rediseno-tarjetas`);
  botones táctiles con `TouchButton`.
- Sonidos y materiales se cargan por nombre desde
  `Assets/Hito1/Resources/{Hito1Audio,Hito1FX}` con `Hito1FX.Clip/Mat` o
  `PropKit.Mat`. Los efectos están sintetizados y las voces, generadas con
  ElevenLabs.

## Convenciones

- Namespace `SojaExiles`. Comentarios en español, **sin tildes** en los
  comentarios. Los textos que ve el jugador sí van con tildes.
- Los valores nuevos de los enums se agregan **al final**: las escenas guardan
  el número, no el nombre.
- Cada script nuevo necesita su `.meta` con un GUID propio.

## Verificar

Si no hay Unity en la máquina, al menos compilar los scripts con Roslyn contra
las DLL de Unity 2022.3.62 y de los paquetes. Indicar siempre si algo se
probó en el editor o en el Quest, o si solo se compiló.
