# Proyecto VR: primeros auxilios ante una quemadura doméstica

Curso CS2H01 Interacción Humano-Computador (UTEC). Unity 2022.3.62f3, Meta XR
SDK 201 (rig `OVRCameraRigInteraction`), visor Meta Quest con Quest Link.
El proyecto se centra **solo en la quemadura**. Las escenas de
atragantamiento y cortes de la etapa anterior se borraron (siguen en el
historial de git).

## Antes de empezar

- **Qué falta y en qué orden:** `docs/PENDIENTES.md` (única lista, solo lo
  que falta). Al terminar un punto, bórralo de ahí y descríbelo en
  `docs/QUEMADURA_INMERSION.md`. Los bugs vienen de
  `Bugs_Experiencia_VR_Quemaduras.pdf` (raíz, sin versionar).
- **Qué hay y cómo probarlo:** `docs/QUEMADURA_INMERSION.md`.
- Los documentos de etapas anteriores se borraron; siguen en el historial.

## Ramas

`rediseno-tarjetas` va un commit por delante de `main` y aún no se une.
Las correcciones de bugs van en `corregir-bugs` (sale de `rediseno-tarjetas`).
Se une a `main` con un pull request.

## Cómo está armado

- Escena principal: `Assets/Hito1/Escenas/Escenario_Quemadura.unity`.
- **No se editan las escenas a mano.** Todo lo nuevo se instala al dar Play
  desde `Hito1RuntimeBootstrap` (solo si la escena tiene
  `BurnSequenceManager`) y no se duplica si ya está en la escena.
- `BurnSequenceManager` maneja los estados (Idle → Burned → Cooling → Cooled
  → Covered → Success) y el canal de eventos `OnEvent` (evento, detalle,
  gravedad). Lo nuevo debe avisar por `Report(...)` en vez de llamar
  directamente al puntaje.
- `BurnProtocol` arma los pasos, los errores, el puntaje y el resumen a
  partir de esos eventos.
- Objetos creados por código con `PropKit`; paneles y textos con `VRPanelKit`,
  `RoundedPanel` y `PanelText` (TextMesh Pro, desde `rediseno-tarjetas`);
  botones táctiles con `TouchButton`.
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
