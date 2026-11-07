# Guion de voces

Las 45 frases habladas del juego: 24 de la tía Rosa (`voz_*`) y 21 del
narrador (`guia_*`). Están en `Assets/Hito1/Resources/Hito1Audio` y el
juego las carga **por nombre**, así que para reemplazar una voz basta con
poner el audio nuevo con el mismo nombre y borrar el anterior (si quedan
`voz_x.wav` y `voz_x.mp3`, Unity se confunde). El formato `.mp3` sirve.

Los textos se sacaron transcribiendo los audios originales (TTS) el
2026-11-06 y se corrigieron a mano.

**Estado:** las 45 frases y tres quejidos nuevos (`voz_dolor_3` a `voz_dolor_5`)
se generaron con ElevenLabs (plan gratis) y se pasaron al juego el
2026-11-06: convertidos a `.wav` 44,1 kHz mono con el mismo nombre (se
conservan los `.meta`), al mismo volumen que las voces anteriores (unos
-17 dBFS en las partes con voz) y con 0,3 s de silencio al final. El
crédito "Voces: elevenlabs.io" está en el resumen final; agregarlo
también en el informe.

## Cómo generarlas (ElevenLabs)

1. Modelo **Eleven v3** (si no está en tu plan: Multilingual v2, sin las
   etiquetas entre corchetes y con *Stability* cerca de 30 %).
2. **Una sola voz por personaje** para todas sus frases. Anota cuál usaste:
   - Tía Rosa: _(voz elegida)_
   - Narrador: _(voz elegida)_
3. Pega la columna "Para ElevenLabs". Las etiquetas van en inglés porque así
   las entiende el modelo; si una no suena bien, prueba sin ella.
4. Descarga con el nombre del archivo de la primera columna.
5. Plan gratis: hay que citar **elevenlabs.io** en los créditos del juego y
   del informe. Las 45 frases usan unos 4.000 de los 10.000 créditos
   mensuales.

## Tía Rosa (24)

Mujer adulta (50 a 60 años), voz cálida; con dolor real, no actuado.

| Archivo | Cuándo suena | Texto | Para ElevenLabs |
|---|---|---|---|
| `voz_pide_taza` | Tutorial: pide la taza | Hola. ¿Me alcanzas esa taza, por favor? | `[warmly] Hola. ¿Me alcanzas esa taza, por favor?` |
| `voz_gracias_taza` | Tutorial: recibe la taza | Gracias. Ya casi está la sopa. | `[cheerfully] Gracias. Ya casi está la sopa.` |
| `voz_quemadura_1` | Se quema | ¡Ay! ¡Me quemé con la olla! | `[screams] ¡Ay! ¡Me quemé con la olla!` |
| `voz_quemadura_2` | Dolor fuerte | ¡Ay, ay! ¡Me arde mucho la mano! | `[in pain] ¡Ay, ay! ¡Me arde mucho la mano!` |
| `voz_dolor_1` | Dolor mientras no se enfría | Me arde… me arde mucho. | `[crying] Me arde… [panting] me arde mucho.` |
| `voz_dolor_2` | Pide ayuda | ¡Ayúdame, por favor! | `[desperate] ¡Ayúdame, por favor!` |
| `voz_pista_agua` | Pista (modo sin narrador) | ¿Qué hago? ¿La pongo en agua fría? | `[anxious] ¿Qué hago? ¿La pongo en agua fría?` |
| `voz_alivio_agua` | Mano bajo el agua fría | Ay… el agua fría me alivia. | `[sighs] Ay… el agua fría me alivia.` |
| `voz_saco_agua` | Le sacan la mano antes de tiempo | ¡No, todavía me arde! | `[in pain] ¡No, todavía me arde!` |
| `voz_agua_caliente` | Le ponen agua caliente | ¡Ay, no! ¡Está caliente, me quema! | `[screams] ¡Ay, no! ¡Está caliente, me quema!` |
| `voz_mano_no` | La toman de la mano quemada | ¡Ay, no! De la mano no, me duele mucho. | `[in pain] ¡Ay, no! De la mano no, me duele mucho.` |
| `voz_hielo_no` | Le acercan hielo | ¡Hielo no! No, no, eso no me pongas. | `[alarmed] ¡Hielo no! No, no, eso no me pongas.` |
| `voz_hielo_duele` | Le ponen hielo | ¡Ay! Está helado, me duele más. | `[in pain] ¡Ay! Está helado, me duele más.` |
| `voz_remedio_no` | Le acercan un remedio casero | No, eso no me pongas, por favor. | `[worried] No, eso no me pongas, por favor.` |
| `voz_remedio_arde` | Le ponen un remedio casero | ¡Ay, ay! Eso me arde todavía más. | `[in pain] ¡Ay, ay! Eso me arde todavía más.` |
| `voz_anillo_sale` | Le sacan el anillo | Ay, mi anillo… gracias, ya se me estaba hinchando. | `[relieved] Ay, mi anillo… gracias, ya se me estaba hinchando.` |
| `voz_anillo_atorado` | El anillo ya no sale | ¡Ay! No sale, ya se me hinchó el dedo. | `[in pain] ¡Ay! No sale, ya se me hinchó el dedo.` |
| `voz_abrigo` | Le ponen la chompa | Gracias, me estaba dando frío. | `[shivering] Gracias, me estaba dando frío.` |
| `voz_enfriada` | Terminó de enfriar | Ya se siente un poco mejor. | `[tired] Ya se siente un poco mejor.` |
| `voz_pista_pano` | Pista: cubrir la quemadura | ¿Me la cubres con algo limpio? | `[softly] ¿Me la cubres con algo limpio?` |
| `voz_pregunta_medico` | Antes de la decisión médica | ¿Crees que tengo que ir al médico? | `[worried] ¿Crees que tengo que ir al médico?` |
| `voz_decision_ir` | Elige ir al centro de salud | Está bien, mejor que me revisen. | `[resigned] Está bien, mejor que me revisen.` |
| `voz_decision_casa` | Elige cuidarla en casa | Bueno, me cuido en casa entonces. | `[hesitant] Bueno, me cuido en casa entonces.` |
| `voz_exito` | Final | Gracias. Ya me siento mucho mejor. | `[relieved] Gracias. Ya me siento mucho mejor.` |

## Narrador (21)

Voz **femenina** adulta, calmada y clara; ritmo pausado (da instrucciones
en una emergencia).

| Archivo | Cuándo suena | Texto |
|---|---|---|
| `guia_bienvenida` | Menú de inicio | Hola. Vas a practicar qué hacer si alguien se quema en la cocina. Elige un modo: guiado, si quieres que te indique cada paso, o evaluación, si prefieres hacerlo solo. Deja la mano sobre el botón hasta que se llene. |
| `guia_intro` | Inicio del tutorial | Estás de visita en la casa de tu tía Rosa. Ella está preparando el almuerzo. |
| `guia_agarrar` | Tutorial 1 | Para agarrar algo, acerca la mano y aprieta el botón lateral del control, o junta el pulgar y el índice. Agarra la taza del mesón. |
| `guia_caminar` | Tutorial 2 | Para caminar, usa el joystick izquierdo. Llévale la taza a tu tía. |
| `guia_bien` | Fin del tutorial | Muy bien. Ya sabes agarrar y caminar. Quédate cerca de ella. |
| `guia_accidente` | Accidente (modo guiado) | Se le volcó la olla. Se quemó la mano con agua hirviendo. Mantén la calma: vamos paso a paso. |
| `guia_evaluacion` | Accidente (modo evaluación) | Se quemó la mano con agua hirviendo. Atiéndela como aprendiste. |
| `guia_apagar` | Paso 1 | Primero, apaga la hornilla para que nadie más se queme. Toca la perilla que brilla. |
| `guia_agua` | Paso 2 | Ahora abre la llave de agua fría, la del punto azul. |
| `guia_llevar` | Paso 3 | Tómala del antebrazo, no de la mano quemada, y llévala hasta el chorro. Suelta su mano cerca del agua y ella la pondrá debajo. |
| `guia_anillo` | Paso 4 | Quítale el anillo antes de que la mano se hinche. Acerca tu mano libre al punto dorado y mantén apretado el botón lateral, o junta el pulgar y el índice, hasta que el anillo salga. |
| `guia_abrigo` | Paso 5 | El agua fría también le enfría el cuerpo. Abrígala con la chompa. |
| `guia_esperar` | Paso 6 | Bien hecho. Ahora espera a que el reloj complete los 20 minutos, sin sacar su mano del agua. |
| `guia_cubrir` | Paso 7 | Ya está enfriada. Cubre la quemadura con una gasa limpia, sin apretar. Si no hay gasa, usa film. |
| `guia_decidir` | Paso 8 | Por último, decide si necesita atención médica. Mira bien la quemadura. |
| `guia_volver` | Sacó la mano del agua | Su mano salió del agua y el reloj se pausó. Vuelve a ponerla bajo el chorro. |
| `guia_err_caliente` | Error: agua caliente | Esa es la llave caliente. Ciérrala y usa la del punto azul. |
| `guia_err_mano` | Error: la toma de la mano | Sujétala del antebrazo: tocar la quemadura le duele. |
| `guia_err_pano` | Error: paño de cocina | El paño de cocina suelta pelusa. Usa una gasa limpia o, si no hay, film. |
| `guia_err_remedio` | Error: remedio casero | No le pongas nada en la quemadura. Solo agua fría corriente. |
| `guia_err_temprano` | Error: cubre antes de tiempo | Todavía no. Primero termina de enfriar la quemadura. |

Para el narrador conviene `[calm]` al inicio de cada frase, y en los
errores `[firm]` (corrige sin regañar).

## Operador del 106, narrador y tía (llamada)

Generadas con ElevenLabs y pasadas al juego el 2026-11-06. Suenan desde el
celular; las del operador (`op_*`) llevan además un filtro de banda telefónica
(300 a 3400 Hz) para que se oigan como al otro lado de la línea. Si falta
alguna, la llamada igual funciona: el texto se lee en el panel.
Operador: **voz masculina** adulta, profesional y calmada (de central de
emergencias). `guia_llamar` es del narrador: **la misma voz femenina** de las
demás `guia_*`.
Usa `[calm]` en todas; en `op_agua_no`, `[firm]`.

| Archivo | Cuándo suena | Para ElevenLabs |
|---|---|---|
| `op_saludo` | Contesta | `[calm] SAMU, ciento seis. ¿Cuál es su emergencia?` |
| `op_agua` | Pregunta por el agua | `[calm] Entiendo. ¿Ya tiene la mano bajo agua fría corriente?` |
| `op_agua_si` | Ya está bajo el agua | `[calm] Muy bien. Manténgala bajo el agua unos veinte minutos.` |
| `op_agua_no` | Todavía no | `[firm] Póngala ya bajo agua fría corriente, sin hielo, y manténgala ahí.` |
| `op_ampollas` | Pide mirar la quemadura | `[calm] Mire bien la quemadura. ¿Tiene ampollas?` |
| `op_centro` | Con ampollas | `[calm] Con ampollas en la mano, debe verla un médico. Cuando termine de enfriarla, cúbrala con una gasa limpia, sin apretar. Si no tiene gasa, sirve film de cocina.` |
| `op_casa` | Sin ampollas | `[calm] Si solo está roja y es pequeña, puede cuidarla en casa. Si salen ampollas o empeora, llévela al centro de salud.` |
| `op_decidir_centro` | Tras recomendar el centro de salud | `[calm] ¿Puede llevarla al centro de salud?` |
| `op_decidir_casa` | Tras recomendar cuidarla en casa | `[calm] ¿Podrá cuidarla en casa?` |
| `op_cierre` | Antes de colgar | `[warmly] De acuerdo. Si empeora o tiene dudas, vuelva a llamar. Cuídela mucho.` |
| `guia_llamar` | Narrador: paso "Llama al 106" | `[calm] Mientras se enfría, pide ayuda. Toma el celular del mesón, llévalo a tu oreja y responde lo que te pregunten.` |

Tía Rosa (con la misma voz que sus otras frases):

| Archivo | Cuándo suena | Para ElevenLabs |
|---|---|---|
| `voz_mira_mano` | Tras quemarse, le muestra la mano al jugador | `[crying] ¡Mira, mira cómo se me puso!` |
| `voz_aqui_mirala` | En la llamada, antes de la pregunta de las ampollas | `[in pain] Aquí, mírala.` |
| `voz_anillo_pide` | Tras mostrar la mano, pide que le saquen el anillo | `[worried] ¡Mi anillo! Sácamelo antes de que se me hinche.` |
| `guia_err_envolver` | Narradora: si se envuelve la mano con el film | `[firm] No la envuelvas: apoya el film encima. La mano se va a hinchar.` |

Las 13 suman unos 800 créditos.

## Quejidos adicionales

Ya generadas y en uso: `PainLines` de `BurnPatientReactions` alterna seis
quejidos y nunca repite el mismo dos veces seguidas.

| Archivo | Para ElevenLabs |
|---|---|
| `voz_dolor_3` | `[whimpering] Ay… ay… cómo arde.` |
| `voz_dolor_4` | `[crying] No aguanto, me quema todavía.` |
| `voz_dolor_5` | `[panting] Ay, Dios mío… mi mano.` |
