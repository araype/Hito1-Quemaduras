# Voces que faltan pasar a ElevenLabs

Rama `voces-elevenlabs`, salida de `main` (`2e0f004`, "version final") el
2026-11-21.

Estas frases se generaron con las voces de Windows ("Microsoft Sabina" para la
narradora y la tía, "Microsoft Raul" para el operador). Dicen lo correcto, pero
suenan robóticas y con otra voz que las de ElevenLabs del 2026-11-06. Esta rama
existe para reemplazarlas y unirlas a `main` con un pull request.

La lista sale de `docs/VOCES.md`, de `docs/PENDIENTES.md` ("Naturalidad de las
voces") y de buscar en `Assets/Hito1/Scripts` qué archivo llama a cada audio.

## Cómo entregarlas

1. **La misma voz de ElevenLabs** que las demás de cada personaje (la de la
   tanda del 2026-11-06). Si no, la narradora o la tía cambian de voz a mitad
   de la partida.
   - Narradora: voz femenina adulta, calmada, ritmo pausado.
   - Tía Rosa: mujer de 50 a 60 años, voz cálida, con dolor real.
   - Operador del 106: voz masculina adulta, profesional y calmada.
2. Modelo **Eleven v3**. Las etiquetas entre corchetes (`[calm]`, `[firm]`,
   `[in pain]`) van en inglés porque así las entiende el modelo; si una no
   suena bien, prueba sin ella. Con Multilingual v2, quítalas y baja
   *Stability* a cerca de 30 %.
3. Descarga cada audio con **el nombre exacto** de la primera columna y
   déjalo en `Assets/Hito1/Resources/Hito1Audio/`, reemplazando el actual.
   No dejes un `.mp3` y un `.wav` con el mismo nombre: Unity se confunde.
   **No borres el `.meta`**: conserva el GUID.
4. Formato final: `.wav` 44,1 kHz mono, a unos -17 dBFS en las partes con voz
   y 0,3 s de silencio al final. Si entregas `.mp3`, se convierten antes de
   hacer el commit.
5. Los `.wav` van por Git LFS (`.gitattributes`): `git lfs install` antes de
   hacer el commit.
6. Al terminar una voz, márcala `[x]` en esta lista.

### Después de reemplazar (lo hace quien una la rama)

- **Tía:** borrar su línea de `Hito1FX.TtsStandIns`
  (`Assets/Hito1/Scripts/Hito1FX.cs`). Mientras esté ahí, el juego no usa el
  audio nuevo: hace sonar otra frase o nada.
- **Boca de la tía:** volver a generar los visemas con
  `Editor/VisemeTracks.cs` (escribe `Resources/Hito1Anim/VisemasVoces.json`)
  para que la boca siga el audio nuevo.
- Las voces del operador pasan por el filtro de teléfono del juego
  (`EmergencyCall`): se entregan sin filtro.
- Probar en Play al menos el tutorial, la llamada al 106 y dos errores.

## Prioridad 1: el flujo principal (17)

Las oye cualquier jugador en una partida normal, en modo guiado.

| Hecha | Archivo | Cuándo suena | Para ElevenLabs |
|---|---|---|---|
| [x] | `guia_bienvenida` | Menú de inicio | `[calm] Hola. Vas a practicar qué hacer si alguien se quema en la cocina. Elige un modo: guiado, si quieres que te indique cada paso, o evaluación, si prefieres hacerlo solo. Para elegir, toca el botón con la punta del dedo.` |
| [x] | `guia_agarrar_manos` | Tutorial 1, con manos | `[calm] Para agarrar algo, acerca la mano y junta el pulgar y el índice, o ciérrala. Agarra la taza del mesón.` |
| [x] | `guia_agarrar_control` | Tutorial 1, con controles | `[calm] Para agarrar algo, acerca el control y aprieta el botón lateral, el del dedo medio. Agarra la taza del mesón.` |
| [x] | `guia_caminar_manos` | Tutorial 2, con manos | `[calm] Ahora camina hasta tu tía y acércale la taza a la mano.` |
| [x] | `guia_caminar_control` | Tutorial 2, con controles | `[calm] Camina con el joystick izquierdo. Para ir lejos, empuja el derecho hacia adelante, apunta y suéltalo. Llévale la taza a tu tía.` |
| [x] | `guia_pausa_manos` | Tutorial 3, con manos | `[calm] Ahora, la pausa. Mira la palma de tu mano izquierda: en la muñeca aparece Pausa. Tócalo con un dedo de la otra mano.` |
| [x] | `guia_pausa_control` | Tutorial 3, con controles | `[calm] Ahora, la pausa. Mira la palma de tu mano izquierda: en la muñeca aparece Pausa. Tócalo con la punta del otro control.` |
| [x] | `guia_teleport_manos` | Tutorial 4, solo con manos | `[calm] Por último, ve hasta el círculo verde. Haz una ele con la mano: índice y pulgar estirados, los otros dedos doblados. Apunta al círculo verde y junta el pulgar al índice para saltar.` |
| [x] | `guia_bien_manos` | Fin del tutorial, con manos | `[calm] Muy bien. Ya sabes agarrar, pausar y moverte lejos. Vuelve junto a tu tía.` |
| [x] | `guia_bien_control` | Fin del tutorial, con controles | `[calm] Muy bien. Ya sabes agarrar, caminar y pausar. Quédate cerca de tu tía.` |
| [x] | `guia_apagar` | Paso: apagar la hornilla | `[calm] Primero, apaga la hornilla para que nadie más se queme. Acerca la mano a la perilla que brilla y apriétala.` |
| [x] | `guia_agua` | Paso: abrir el agua fría | `[calm] Ahora abre el agua fría: toma la palanca del caño y gírala un poco hacia ti.` |
| [x] | `guia_anillo` | Paso: sacar el anillo | `[calm] Quítale el anillo antes de que la mano se hinche. Pellizca el punto dorado con tu mano libre, con el botón lateral o juntando el pulgar y el índice, y deslízalo hacia la punta de su dedo.` |
| [x] | `guia_llamar` | Paso: llamar al 106 | `[calm] Mientras se enfría, pide ayuda. Toma el celular del mesón y toca Llamar ciento seis en su pantalla. La llamada sale en altavoz: puedes dejarlo en el mesón y seguir atendiéndola.` |
| [x] | `guia_preparar` | Paso: buscar con qué cubrirla | `[calm] Mientras se enfría, abre el cajón bajo el mesón junto al lavadero y saca el film o la bolsa plástica.` |
| [x] | `guia_cubrir` | Paso: cubrir la quemadura | `[calm] Ya está enfriada. Cúbrela sin apretar: desliza la bolsa por su mano hasta la muñeca, o apoya tiras de film a lo largo.` |
| [ ] | `op_centro` | Operador: tiene ampollas | `[calm] Con ampollas en la mano, debe verla un médico. Cuando termine de enfriarla, cúbrala sin apretar con film de cocina o una bolsa plástica limpia.` |

## Prioridad 2: errores, ayudas y respuestas (22)

Suenan según lo que haga el jugador: errores, ayudas del modo guiado y las
variantes de la llamada.

### Narradora (15)

| Hecha | Archivo | Cuándo suena | Para ElevenLabs |
|---|---|---|---|
| [ ] | `guia_err_caliente` | Error: sale agua caliente | `[firm] Está saliendo caliente. Regresa la palanca hacia arriba, hasta que salga fría.` |
| [ ] | `guia_err_pano` | Error: paño de cocina | `[calm] El paño de cocina suelta pelusa. Usa film o una bolsa limpia.` |
| [x] | `guia_err_curita` | Error: curita sobre la quemadura | `[firm] Las curitas se pegan a la piel quemada. Cúbrela con film o una bolsa limpia.` |
| [x] | `guia_err_anillo` | El anillo ya no sale | `[calm] Su mano ya se hinchó: no fuerces el anillo. Sigue con el agua fría; en el centro de salud se lo sacarán.` |
| [x] | `guia_err_aguantar` | Saca la mano por el frío | `[calm] Si el agua le parece muy fría, témplala un poco: así completa los veinte minutos sin sacar la mano.` |
| [x] | `guia_err_olla` | Toca la olla caliente | `[firm] No toques la olla: sigue caliente y también te quemarías.` |
| [x] | `guia_err_charco` | Pisa el agua derramada | `[calm] El piso mojado resbala: rodea el agua derramada.` |
| [x] | `guia_frio_lejos` | La tía tiene frío y el jugador está lejos | `[calm] Tu tía se queja de que el agua está muy fría. Vuelve con ella antes de que saque la mano.` |
| [x] | `guia_templar` | Eligió templar el agua | `[calm] Gira la palanca del caño un poco más hacia ti, hasta la mitad: así sale tibia. Su mano sigue bajo el chorro.` |
| [x] | `guia_ayuda_frio` | Guiado: ella quiere sacar la mano | `[calm] La respuesta correcta está en verde: templarle un poco el agua, girando la palanca hasta tibia. Pedirle que aguante no le quita el frío, y terminará sacando la mano. Y el hielo daña todavía más la piel.` |
| [x] | `guia_ayuda_agua` | Guiado: el 106 pregunta por el agua | `[calm] El operador no ve la cocina: cuéntale lo que pasa de verdad. La respuesta correcta está en verde.` |
| [x] | `guia_ayuda_ampollas_si` | Guiado: tiene ampollas | `[calm] Mira su mano: tiene ampollas, unas burbujas de piel hinchada con líquido claro. Es una quemadura de segundo grado.` |
| [x] | `guia_ayuda_ampollas_no` | Guiado: no tiene ampollas | `[calm] Mira su mano: está roja e irritada, pero no tiene burbujas de piel con líquido. Eso no son ampollas: es una quemadura de primer grado.` |
| [ ] | `guia_ayuda_centro` | Guiado: decidir, con ampollas | `[calm] Tiene ampollas: es una quemadura de segundo grado. En la mano, aunque sea pequeña, debe verla un médico. Por eso lo correcto es llevarla al centro de salud.` |
| [ ] | `guia_ayuda_casa` | Guiado: decidir, sin ampollas | `[calm] Solo está roja, sin ampollas: es de primer grado, y se cuida en casa aunque sea grande. Si después salen ampollas o empeora, llévala al centro de salud.` |

### Operador del 106 (3)

| Hecha | Archivo | Cuándo suena | Para ElevenLabs |
|---|---|---|---|
| [ ] | `op_agua_hecha` | Ya la enfrió | `[warmly] Muy bien, eso es lo más importante.` |
| [ ] | `op_centro_cubierta` | Con ampollas y ya cubierta | `[calm] Con ampollas en la mano, debe verla un médico. Si ya la cubrió sin apretar con film o una bolsa limpia, déjela así.` |
| [ ] | `op_casa_grande` | Sin ampollas y quemadura grande | `[calm] Si solo está roja, sin ampollas, puede cuidarla en casa, aunque ocupe buena parte de la mano. Si salen ampollas o empeora, llévela al centro de salud.` |

### Tía Rosa (4) — hoy no suenan (`TtsStandIns`)

| Hecha | Archivo | Cuándo suena | Para ElevenLabs |
|---|---|---|---|
| [ ] | `voz_frio` | Quiere sacar la mano por el frío | Transcribir del audio actual (Sabina); por el momento suena `voz_hielo_duele`. |
| [ ] | `voz_templar_pide` | Le van a templar el agua | `[worried] Ya, pero ábrela solo un poquito, que la caliente quema.` |
| [ ] | `voz_templada` | El agua ya está tibia | `[relieved] Ay, así está mejor. Aguanto.` |
| [ ] | `voz_aprieta` | Le envuelven la mano con film | `[in pain] ¡Ay, me aprieta! No me envuelvas la mano, por favor.` |

## Prioridad 3: solo con la pregunta de los nervios (5)

`PatientDialog.worryQuestion` está apagada por defecto, así que estas no suenan
en una partida normal. Regenerarlas solo si se vuelve a activar.

| Hecha | Archivo | Quién | Para ElevenLabs |
|---|---|---|---|
| [ ] | `guia_err_calma` | Narradora | `[firm] Háblale con calma: dile que el agua fría alivia.` |
| [ ] | `guia_ayuda_calma` | Narradora | `[calm] La respuesta en verde la calma. El miedo aumenta el dolor, y los remedios caseros dañan la quemadura.` |
| [ ] | `voz_ansiosa` | Tía | Transcribir del audio actual (Sabina). |
| [ ] | `voz_calmada` | Tía | Transcribir del audio actual (Sabina). |
| [ ] | `voz_asustada` | Tía | Transcribir del audio actual (Sabina). |

## No hace falta regenerar (5)

Ningún script las llama en `main`:

- `guia_teleport` (la reemplaza `guia_teleport_manos`);
- `guia_ayuda_seguir` (la reemplazan `guia_ayuda_centro` y `guia_ayuda_casa`);
- `voz_aguanto`;
- `guia_abrigo` y `voz_pide_manta` (el paso de la manta ya no está en
  `BurnGuide.Steps`).

Se pueden borrar en otro commit, pero no hace falta.

## Créditos

Las 39 de prioridad 1 y 2 suman unos 4.100 caracteres, es decir, unos 4.100
créditos de ElevenLabs (el plan gratis da 10.000 al mes); un poco más si hay
que repetir alguna toma. Con el plan gratis hay que citar **elevenlabs.io**
en los créditos del juego y del informe.
