# Proyecto VR: primeros auxilios ante una quemadura doméstica

Curso CS2H01 Interacción Humano-Computador (UTEC). Unity 2022.3.62f3, Meta XR
SDK 201 (rig `OVRCameraRigInteraction`), visor Meta Quest con Quest Link.
El proyecto se centra **solo en la quemadura**; las escenas de
atragantamiento y cortes son de una etapa anterior.

## Antes de empezar

- **Qué falta:** `docs/PENDIENTES.md`. Al terminar un punto, márcalo ahí.
- **Qué hay y cómo probarlo:** `docs/QUEMADURA_INMERSION.md`. Las secciones 5
  y 6 cubren la experiencia principal y la secuencia.
- Historia anterior: `docs/HITO2.md` y `docs/AUDITORIA.md`.

## Ramas

`quemaduras` → `experiencia-quemadura` → `secuencia-quemadura`. Cada una sale
de la anterior; trabaja sobre la última.

## Cómo está armado

- Escena principal: `Assets/Hito1/Escenas/Escenario_Hito1.unity`.
- **No se editan las escenas a mano.** Todo lo nuevo se instala al dar Play
  desde `Hito1RuntimeBootstrap` (solo si la escena tiene
  `BurnSequenceManager`) y no se duplica si ya está en la escena.
- `BurnSequenceManager` maneja los estados (Idle → Burned → Cooling → Cooled
  → Covered → Success) y el canal de eventos `OnEvent` (evento, detalle,
  gravedad). Lo nuevo debe avisar por `Report(...)` en vez de llamar
  directamente al puntaje.
- `BurnProtocol` arma los pasos, los errores, el puntaje y el resumen a
  partir de esos eventos.
- Objetos creados por código con `PropKit`; paneles y textos con `VRPanelKit`
  (TextMesh con la fuente integrada: el proyecto no tiene los recursos de
  TextMesh Pro); botones táctiles con `TouchButton`.
- Sonidos y materiales se cargan por nombre desde
  `Assets/Hito1/Resources/{Hito1Audio,Hito1FX}` con `Hito1FX.Clip/Mat` o
  `PropKit.Mat`. Los efectos están sintetizados y las voces son TTS.

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
