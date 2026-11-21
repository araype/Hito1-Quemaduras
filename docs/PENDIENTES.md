# Pendientes

Única lista de lo que falta. **Solo va lo que falta:** al terminar un punto,
se borra de aquí y se describe en `docs/QUEMADURA_INMERSION.md` (qué hay y
cómo probarlo), diciendo si se probó en el Quest, en el editor o si solo se
compiló.

Estado al 2026-11-21: la experiencia está completa en `main` (ver
`docs/QUEMADURA_INMERSION.md`, sección 42, con el recorrido final y lo que
se revisó). **Lo único que falta es que las voces suenen naturales.**

## Cómo retomar

- Unity: detener Play **antes** de editar scripts (recompilar en Play hizo
  caer Unity al salir de Play). El MCP de Unity no se conecta solo: hay que
  pulsar conectar en la ventana "MCP for Unity".
- Las voces se cargan **por nombre** desde
  `Assets/Hito1/Resources/Hito1Audio`: para cambiar una basta con poner el
  audio nuevo con el mismo nombre (y borrar el anterior si cambia la
  extensión). Formato de las actuales: `.wav` 44,1 kHz mono, a unos
  -17 dBFS en las partes con voz y con 0,3 s de silencio al final.
- Los textos exactos de cada frase están en `docs/VOCES.md`.

## Naturalidad de las voces

Lista de trabajo con prioridades y textos listos para pegar: `docs/VOCES_ELEVENLABS.md` (rama `voces-elevenlabs`).

Estas frases dicen lo correcto, pero se generaron con las voces de Windows
("Microsoft Sabina" para la narradora, "Microsoft Raul" para el operador) y
suenan robóticas y distintas de las de ElevenLabs. Regenerarlas con
ElevenLabs (la misma voz que las demás de cada personaje), con el mismo
nombre de archivo:

- **Narradora:** `guia_bienvenida`, `guia_anillo`, `guia_preparar`,
  `guia_cubrir`, `guia_apagar`, `guia_agua`, `guia_llamar`,
  `guia_frio_lejos`, `guia_templar`, los errores `guia_err_pano`,
  `guia_err_curita`, `guia_err_anillo`, `guia_err_calma`,
  `guia_err_aguantar`, `guia_err_olla`, `guia_err_charco` y
  `guia_err_caliente`; el tutorial `guia_agarrar_manos`,
  `guia_agarrar_control`, `guia_caminar_manos`, `guia_caminar_control`,
  `guia_pausa_manos`, `guia_pausa_control`, `guia_teleport_manos`,
  `guia_bien_manos` y `guia_bien_control`; y las explicaciones del modo
  guiado `guia_ayuda_frio`, `guia_ayuda_calma`, `guia_ayuda_agua`,
  `guia_ayuda_ampollas_si`, `guia_ayuda_ampollas_no`, `guia_ayuda_centro`
  y `guia_ayuda_casa`.
- **Operador del 106:** `op_centro`, `op_centro_cubierta`, `op_agua_hecha`
  y `op_casa_grande`.
- **Tía Rosa** (hoy suena en su lugar una frase de ElevenLabs que encaja o
  solo el texto, `Hito1FX.TtsStandIns`): `voz_ansiosa`, `voz_calmada`,
  `voz_asustada`, `voz_frio`, `voz_templada`, `voz_aprieta`
  y `voz_templar_pide`. Al regenerarlas, sacarlas de `TtsStandIns` y
  volver a generar los visemas (`Editor/VisemeTracks.cs`, que escribe
  `Resources/Hito1Anim/VisemasVoces.json`) para que la boca siga el
  audio nuevo.

`guia_teleport`, `guia_ayuda_seguir` y `voz_aguanto` ya no se usan: se pueden borrar.

## Regla

Toda mecánica nueva debe estar respaldada por las guías reales (NHS, Cruz
Roja Británica, guía LSEBN). Lo que no tenga respaldo se anota aquí como "a
decidir" antes de implementarlo.
