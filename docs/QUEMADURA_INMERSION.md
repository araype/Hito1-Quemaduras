# Quemadura: qué hay y cómo probarlo

Referencia de lo que ya está hecho, por etapas (cada sección nombra la rama
donde se hizo). Lo que falta está en `docs/PENDIENTES.md`.

**No se tocan las escenas:** todo se instala solo al dar Play
(`Hito1RuntimeBootstrap`, solo si la escena tiene `BurnSequenceManager`).
Para ajustar un valor, agrega el componente a mano en el objeto indicado y
cambia sus campos en el Inspector; si ya está, no se duplica.

Estado de las pruebas: se jugó en el Quest 2 el 2026-11-02 sobre
`rediseno-tarjetas`; los defectos encontrados están en `PENDIENTES.md`.

## 1. Aspecto, física y sonido (rama `quemaduras`)

| Qué | Antes | Ahora | Componente / dónde |
|---|---|---|---|
| Paciente | Blanco | Piel, ropa y pelo con sus texturas | Materiales en `Personajes/Materiales`, remapeados en el `.fbx` |
| Quemadura | Esfera roja | Piel enrojecida sobre la mano; se calma con el agua y queda rosada | `BurnSkinOverlay` (paciente) |
| Olla | Pila de bowls blancos | Olla de acero con mangos, agua hirviendo, vapor y llama azul en el quemador | `PotFX` (`Ollaprovisional`) |
| Agua | Bolitas blancas | Chorro transparente que se corta en la mano o el lavadero y salpica | `WaterStreamFX` (`Aguadegrifo`) |
| Paño | Caja rígida | Tela que cuelga de los dedos; si se suelta sobre la mano, queda apoyada y cae por los costados | `TowelCloth` (`panuelo`) |
| Paciente (reacciones) | Ninguna | Grita y retrocede al quemarse, se queja, tiembla, mira su mano, se alivia con el agua, protesta si se saca antes, agradece al final | `BurnPatientReactions` (paciente) |
| Ambiente | Silencio; el acero reflejaba el cielo | Sonido de cocina, reflejos reales de la cocina, lámpara cálida sobre el mesón y sol más tibio | `KitchenAmbience` (`BurnSequenceManager`) |
| Respiración | Quieta como maniquí | Pecho y hombros suben y bajan; con dolor respira rápido y se la oye jadear, y se calma al enfriar | `BurnPatientReactions` + `PatientBody.breath` |
| Reflejo | — | Al quemarse sacude la mano | `BurnPatientReactions` + `PatientBody.rightHandShake` |
| Mano mojada | — | Brilla bajo el agua, gotea al sacarla (con sonido) y se seca en ~25 s | `PatientWetHand` (paciente) |
| Lavadero | — | Se junta un charco donde cae el agua y se va por el desagüe al cerrar, con gorgoteo | `WaterStreamFX` |
| Cocina | Llama siempre prendida | Perilla del quemador: se toca con la mano (punto dorado) y apaga la llama; el hervor y el vapor bajan de a poco | `StoveKnob` (perilla `Knob.00X` más cercana) + `PotFX` |

### Sonidos (`Assets/Hito1/Resources/Hito1Audio`)

Los efectos (agua, grifo, hervor, siseo, golpe de olla, roce de tela, ambiente,
logros) están sintetizados. Las frases de la paciente son voz de síntesis
(Microsoft Sabina, es-MX). **Para cambiar un sonido, reemplaza el `.wav`
manteniendo el nombre.** Por ejemplo, con una grabación real de alguien del
equipo para las frases `voz_*`.

| Momento | Sonido |
|---|---|
| Se quema (a los 3 s) | siseo, golpe de la olla, “¡Ay! ¡Me quemé con la olla!” |
| Mientras le arde | quejas cada 7–12 s |
| 18 s sin agua | pista: “¿La pongo en agua fría?” |
| Abrir o cerrar el grifo | chirrido y clic de la llave; agua corriendo |
| Mano bajo el chorro | salpicado más fuerte; “el agua fría me alivia” |
| Saca la mano antes de tiempo | “¡No, todavía me arde!” |
| Enfriada (10 s) | campanita y “Ya se siente un poco mejor” |
| 12 s sin cubrir | pista: “¿Me la cubres con algo limpio?” |
| Cubierta (3 s) | campanitas y “Gracias. Ya me siento mucho mejor.” |

Las pistas se apagan con `BurnPatientReactions > Voice Hints`, para evaluar sin
ayudas.

**Parpadeo:** el modelo de la paciente (Mixamo Ch07) no tiene blendshapes ni
huesos de párpados, así que no puede parpadear. Para eso habría que cambiar el
personaje por uno con blendshapes (por ejemplo, ARKit o Ready Player Me).

### Grabar la voz real

1. Graba cada frase de la tabla, una por archivo, en un lugar silencioso, con
   el celular a unos 20 cm de la boca. Actúa el dolor: un “¡Ay!” leído no
   convence.
2. Recorta el silencio del principio y del final (Audacity, gratis).
3. Exporta como **WAV, mono** y guárdalo con el **mismo nombre** en
   `Assets/Hito1/Resources/Hito1Audio`, reemplazando el anterior
   (`voz_quemadura_1.wav`, `voz_dolor_1.wav`…). No toques los `.meta`.

| Archivo | Frase |
|---|---|
| `voz_quemadura_1` | ¡Ay! ¡Me quemé con la olla! |
| `voz_quemadura_2` | ¡Ay, ay! ¡Me arde mucho la mano! |
| `voz_dolor_1` | Me arde… me arde mucho. |
| `voz_dolor_2` | ¡Ayúdame, por favor! |
| `voz_pista_agua` | ¿Qué hago? ¿La pongo en agua fría? |
| `voz_alivio_agua` | Ay… el agua fría me alivia. |
| `voz_saco_agua` | ¡No, todavía me arde! |
| `voz_enfriada` | Ya se siente un poco mejor. |
| `voz_pista_pano` | ¿Me la cubres con algo limpio? |
| `voz_exito` | Gracias. Ya me siento mucho mejor. |

`respiracion_dolor_loop.wav` también se puede reemplazar por una grabación de
respiración agitada de unos 5 segundos que empalme bien al repetirse.

## 2. Cómo probar

1. Abrir el proyecto y esperar la importación. Las texturas nuevas y el `.fbx`
   del paciente se reimportan, así que tarda un poco.
2. Abrir `Escenario_Quemadura` y dar Play con Quest Link.
3. En la consola deben aparecer `[BurnSkinOverlay] Quemadura sobre N
   triangulos de la mano`, y al soltar el paño sobre la mano,
   `[TowelCloth] Pano apoyado sobre la mano del paciente`.
4. Revisar:
   - La paciente se ve con color. **Si sigue blanca:** selecciona
     `Personajes/Familiarartefacto.fbx`, ve a la pestaña *Materials* y revisa
     que `Ch07_body` y `Ch07_hair` apunten a `Paciente_Cuerpo` y
     `Paciente_Pelo`.
   - La quemadura se ve sobre la mano, no como esfera.
   - Al abrir el grifo, el chorro cae y se corta al poner la mano.
   - El paño cuelga al agarrarlo y queda sobre la mano al soltarlo cerca de
     ella.
   - Se oyen el hervor, el agua y la voz, y cambian con la distancia.

## 3. Valores para ajustar

- **Chorro:** `WaterStreamFX > Nozzle Offset`, si el agua no sale de la boca
  del grifo. Grosor: `Top Radius` y `Bottom Radius`.
- **Paño:** `TowelCloth > Attach Distance` (qué tan cerca de la mano hay que
  soltarlo), `Bend Stiffness` (rigidez), `Palm Radius` (tamaño de la mano
  para la tela).
- **Quemadura:** `BurnSkinOverlay > Min Hand Weight`. Si el rojo sube por la
  muñeca, súbelo; si no cubre los dedos, bájalo. Colores por etapa.
- **Olla:** `PotFX > Water Level`, `Steam`, `Burner Flame`, `Boil Volume`.
- **Voz:** `BurnPatientReactions > Voice Volume`, `Water Hint Delay`,
  `Cloth Hint Delay`.
- **Respiración:** `BurnPatientReactions > Calm/Pain Breaths Per Minute`,
  `Pain Breath Depth`, `Pain Breath Volume`; cuánto se mueve el cuerpo en
  `PatientBody > Breath Chest/Shoulder Degrees`.
- **Mano mojada:** `PatientWetHand > Drying Seconds`, `Drip Rate`.
- **Charco:** `WaterStreamFX > Puddle Max Radius`, `Drain Volume`.
- **Perilla:** `StoveKnob > Turn Angle`, `Start On`. Si la perilla del kit es
  estática (Static), la llama se apaga igual pero la perilla no se ve girar.
- **Luz:** `KitchenAmbience > Lamp Intensity`, `Lamp Height`, `Warm Sunlight`.
  Si el Quest pierde cuadros, desactiva `Warm Light`.
- **Volver a ver la esfera del marcador** (para depurar):
  `BurnSkinOverlay > Hide Marker` = falso.

## 4. Rendimiento en el Quest

- Las texturas del paciente bajan a 2048/1024 en Android.
- La sonda de reflejos se renderiza **una sola vez** al empezar.
- La tela tiene 13 × 9 partículas y 10 iteraciones a 90 Hz.
- Una sola luz extra (la lámpara, sin sombras). Se puede apagar.

## 5. Experiencia principal (rama `experiencia-quemadura`)

Igual que lo anterior: **no se tocó la escena**, todo se instala al dar Play.
Compila contra Unity 2022.3.62 y el Meta XR SDK 201. **Todavía no se ha visto
en el editor ni en el Quest.**

| Qué | Cómo se ve / se usa | Componente |
|---|---|---|
| Accidente | A los 6 s la olla se vuelca hacia la paciente: el agua cae en la mano, se forma un charco humeante en el mesón y otro en el suelo, y sale una nube de vapor. Ahí se quema la mano (ya no a los 3 s fijos). | `PotAccident` (olla) |
| Hinchazón | La mano se engrosa de a poco: rápido sin agua, casi nada bajo el chorro. | `BurnInjury` (paciente) |
| Ampollas | En segundo grado aparecen a los 6 s y crecen mientras no se enfría. Enfriar a tiempo las deja chicas. | `BurnInjury` |
| Llaves | La llave que ya estaba es la **fría** (punto azul). Al lado hay una **caliente** (punto rojo). Con solo la caliente abierta sale vapor del chorro, no enfría, vuelve a enrojecer la mano, la paciente grita y cuenta como error. | `WaterTap.hot/pair` |
| Anillo | Anillo dorado en el anular. Se saca acercando la mano libre al punto dorado y manteniendo apretado el botón lateral (o la pinza) 1,5 s; el anillo avanza, el mando vibra y, si se suelta antes, vuelve. Si la mano ya se hinchó, se traba y cuenta como error. | `PatientRing` (paciente) |
| Manta | Doblada en el sofá de la sala (antes, chompa en la silla; sección 24). Se trae a la cocina y se suelta sobre la espalda o los hombros: queda echada con tela simulada. | `BlanketItem`, `BlanketDrape` |
| Cubrir | Film y gasa junto al lavadero (la gasa se quitó, sección 12). Se sueltan sobre la mano **ya enfriada**: la cubren y el caso termina (Success). Si se intenta antes, es un error. El paño de cocina ya no cierra el caso: sobre la quemadura cuenta como error. Ahora el film va en tiras (sección 11). | `CoverItem`, `TowelMisuse` |
| Remedios caseros | Mantequilla, pasta dental, aceite y algodón en el mesón; hielo cerca del refrigerador. Al acercarlos, la paciente aparta la mano y protesta (error). Si se aplican, queda una capa sobre la piel y grita (error crítico; el algodón es error común). El agua quita la grasa y la pasta. | `HomeRemedy` |
| Vibración | Suave mientras sostienes la mano de la paciente bajo el agua (más fuerte si está caliente), pulsos al acercar la mano al fuego, un golpe por error (dos si es crítico) y un toque leve en cada acierto. | `BurnHaptics` |

Todo lo que hace el jugador sale por `BurnSequenceManager.OnEvent`, con su
gravedad (`Errors` y `CriticalErrors` llevan la cuenta). Esto queda listo para
conectar después el resumen y el puntaje. En la consola cada evento aparece
como `[BurnSequence] Evento ...`.

### Probar

1. Dar Play y esperar el accidente. En la consola deben salir `[BurnInjury] N
   ampollas preparadas`, `[BurnSceneProps] ... en (x, y, z)` por cada objeto y
   `Evento PotSpilled`.
2. Revisar que los objetos quedaron **sobre** el mesón (y no dentro de algo).
   Si alguno sale mal ubicado, la consola avisa con `No hay lugar libre`.
3. Probar cada fila de la tabla y mirar los eventos en la consola.

### Valores para ajustar

- **Accidente:** `PotAccident > Accident Delay`, `Tip Angle`, radios de los
  charcos y `Steam Seconds`.
- **Quemadura:** `BurnInjury > Degree` (o `Random Degree`), `Max Swelling`,
  `Swell Seconds Untreated`, `Blister Count`, `Blister Diameter`.
- **Anillo:** `PatientRing > Inner Radius` (si queda flojo o se hunde en el
  dedo), `Along Finger`, `Hold Seconds`, `Stuck Swelling`.
- **Llave caliente:** se crea a 9 cm de la fría. Muévela en Play
  (`LlaveGrifoCaliente`, hija de `Aguadegrifo`) si queda fuera del grifo.
- **Remedios:** `HomeRemedy > Near Distance` y `Apply Distance`.
- **Vibración:** `BurnHaptics > Intensity` y `Burner Warn Distance`.
- **Qué objetos aparecen:** `BurnSceneProps > Cover Items / Sweater / Remedies`.

Los sonidos nuevos (`agua_derrame`, `anillo_sale`, `film_estirar`,
`hielo_choque`) están sintetizados. Las 8 frases nuevas (`voz_agua_caliente`,
`voz_hielo_no`, `voz_remedio_no`, `voz_hielo_duele`, `voz_remedio_arde`,
`voz_anillo_sale`, `voz_anillo_atorado`, `voz_abrigo`) son TTS y se reemplazan
igual que las demás (sección 1).

## 6. Secuencia, reloj y resumen (rama `secuencia-quemadura`)

Sigue sin tocarse la escena. Compila; **no se ha visto en Unity ni en el
Quest**.

### Flujo completo

1. **Accidente** (a los 6 s): la mano se quema y empieza a correr el tiempo.
2. **Asegurar la escena:** apagar la perilla de la cocina.
3. **Agua fría:** abrir la llave azul y poner la mano bajo el chorro.
4. **Enfriar 20 minutos:** el **reloj de pared** junto al grifo los muestra en
   90 s reales. El minutero avanza, el segundero gira rápido y se encienden 20
   marcas verdes. Si la mano sale del agua, el reloj **se pausa** (no vuelve a
   cero), la paciente se queja y cuenta como error.
5. **Mientras tanto:** sacar el anillo y abrigarla con la manta de la sala.
6. **Cubrir** con tiras de film o una bolsa plástica (sección 11). La paciente agradece y pregunta si tiene que ir
   al médico.
7. **Decisión médica:** a los 5 s aparece frente al jugador un panel con dos
   botones (se tocan con la mano). Segundo grado (ampollas en la mano) → centro
   de salud. Primer grado → casa.
8. **Resumen:** puntaje sobre 100, los 7 pasos (hechos o faltantes), errores y
   errores críticos, tiempo de primera acción, minutos de enfriamiento, la
   decisión y hasta 4 consejos de "qué mejorar". El botón **Repetir práctica**
   recarga la escena.

Al terminar, también cuentan como error los pasos que nunca se hicieron:
apagar el fuego, sacar el anillo (si no se trabó antes) y abrigar.

**Puntaje:** 100 − 10 por error − 25 por error crítico (mínimo 0).

**Registro:** cada sesión se guarda en un JSON en
`Application.persistentDataPath/sesiones_quemadura/` (en el Quest:
`Android/data/<paquete>/files/sesiones_quemadura/`). Sirve para las pruebas con
usuarios y la métrica HEART.

### Valores para ajustar

- **Duración del enfriamiento:** `BurnProtocol > Cooling Seconds` (90 por
  defecto; para probar rápido, pon 15).
- **Pregunta médica:** `BurnProtocol > Decision Delay`.
- **Puntaje:** `Error Penalty`, `Critical Penalty`.
- **Reloj:** `CoolingClock > Diameter`, `Height Above Tap`, `Show Label`. Si
  no encuentra una pared cerca del grifo, queda flotando sobre él (lo avisa la
  consola).

Frases nuevas (TTS): `voz_pregunta_medico`, `voz_decision_ir` y
`voz_decision_casa`.

## 7. Guía, tutorial y paciente física (rama `guia-y-paciente-fisica`)

Respuesta a la primera prueba en el visor: no se entendía qué hacer, costaba
encontrar de dónde agarrar a la paciente y llevarla al agua, y su cuerpo era
"fantasma". Sigue sin tocarse la escena. **Se probó en el editor con Quest
Link conectado, simulando las acciones del jugador por código. Falta jugarlo
con las manos en el visor.**

| Qué | Cómo funciona | Componente |
|---|---|---|
| Escala de la cocina | El kit estaba a ~1,4 veces el tamaño real (mesón de 1,37 m, refrigerador de 2,46 m). Al dar Play se achica a 0,67 alrededor de la paciente: el mesón queda a 0,92 m. Por eso ella tenía que levantar la mano a la cara para revolver la olla. | `BurnStaging` |
| Ubicación inicial | La paciente queda de frente a la cocina con la olla delante de su mano derecha; el jugador, a un costado mirándola. | `BurnStaging` |
| Menú de inicio | Aviso de contenido (gritos y quemadura visible) y dos botones: **Modo guiado** / **Modo evaluación**. El modo se guarda en el JSON (`modo`). | `BurnGuide` |
| Tutorial en la historia | La tía Rosa cocina y le pide al jugador una taza: hay que agarrarla y llevársela (agarrar y caminar). Ella la recibe y la deja en el mesón. Recién ahí se vuelca la olla. | `BurnGuide`, `PatientBurnArm`, `PotAccident.Trigger` |
| Guía paso a paso | Narrador (otra voz que la paciente) + tarjeta que acompaña la vista ("Paso 3 de 8"), a la altura de los ojos y a 90 cm para no tapar el mesón; completa mientras habla el narrador, con un aviso o si se la mira, y luego compacta y translúcida (solo el paso) + brillo azul sobre lo que hay que usar: perilla, llave fría, antebrazo, chorro, anillo, chompa, reloj, film/bolsa. Si no avanza en 30 s, repite. Si la mano sale del agua, vuelve al paso de llevarla. | `BurnGuide` |
| Correcciones | Agua caliente, remedios, paño, cubrir antes de tiempo y agarrar la mano quemada: el narrador corrige y la tarjeta muestra "¡Cuidado!". | `BurnGuide` |
| Modo evaluación | Mismo tutorial; después del accidente no hay tarjeta, ni voz de guía, ni texto en el reloj. | `BurnGuide`, `CoolingClock.showLabel` |
| Cuerpo físico | 14 cápsulas que siguen los huesos (torso, cabeza, brazos, piernas). El jugador no la atraviesa y los objetos chocan con ella. Tocarla vibra. | `PatientPhysics` |
| Agarrar el brazo | Se la puede tomar del brazo, el antebrazo, la muñeca o la mano (antes, solo una esfera invisible de 6 cm en la muñeca). Al acercar la mano aparece un brillo verde (antebrazo) o naranja (mano quemada). Tomarla de la mano quemada le duele y cuenta como error (`BurnedHandGrabbed`). | `PatientPhysics`, `GrabRedirect` |
| Llevarla al agua | Mientras la llevas del brazo camina antes y más rápido (hasta 1,5 m/s) y el agarre no se suelta hasta 0,9 m. Si sueltas su mano a menos de 32 cm del chorro (con agua fría corriendo), ella misma la pone bajo el agua y la mantiene ahí. | `PatientReach`, `PatientBurnArm` |
| Mano quemada sin sostener | Antes: revuelve la olla con una cuchara de palo. Al quemarse: suelta la cuchara y protege la mano contra el pecho, sostenida por la otra. Si la sueltas lejos del agua, la deja un momento y vuelve a protegerla. Si se cierra el agua o termina de enfriar, la saca. | `PatientBurnArm` |
| Caminar | Las piernas dan pasos según la velocidad (antes se deslizaba). | `PatientBody` |
| Charco y remedios | El charco del mesón ya no sobresale del borde; los remedios ya no aparecen sobre las hornillas. | `PotAccident`, `BurnSceneProps` |

### Voces nuevas (TTS)

- Narrador (Microsoft Raúl, es-MX): 21 archivos `guia_*.wav` (bienvenida,
  intro, tutorial, cada paso y cada corrección).
- Paciente (Sabina): `voz_pide_taza`, `voz_gracias_taza`, `voz_mano_no`.

Se reemplazan igual que las demás (mismo nombre). El texto de cada frase
está en `BurnGuide` (tarjetas) y en el informe de la rama.

### Probar

1. Dar Play con Quest Link. En la consola: `[BurnStaging] Cocina escalada a
   0.67` y `[PatientPhysics] 14 colliders en el cuerpo`.
2. Elegir el modo dejando la mano sobre un botón hasta que se llene la barra. Agarrar la taza y llevársela a la tía.
3. Tras el accidente, seguir la tarjeta. Probar a tomarla del antebrazo,
   caminar hasta el lavadero y soltar su mano cerca del chorro.
4. Probar a tomarla de la mano quemada (queja y corrección).

### Valores para ajustar

- **Escala:** `BurnStaging.CounterHeight` (0,92 m).
- **Agarre y guía de la paciente:** `PatientPhysics > Hover Distance`,
  `PatientReach > Led Follow Fraction / Led Max Speed`, `Grabbable >
  Break Distance` del objetivo de la mano (0,9 m).
- **Ponerse sola bajo el agua:** `PatientBurnArm > Assist Radius`, `Palm
  Below Nozzle`, `Withdraw After Cooled`; postura: `Guard Offset`.
- **Guía:** `BurnGuide > Ask Mode`, `Guided`, `Tutorial`, `Reminder
  Seconds`, `Card Distance / Card Drop`, `Accident After Tutorial`.
- **Pasos:** `PatientBody > Step Length / Step Thigh Degrees / Step Knee
  Degrees`.

## 8. Movilidad y tarjetas (`movilidad-mejorada`, `rediseno-tarjetas`)

- **Movilidad** (`VRLocomotion`): caminar a 1,3 m/s (`walkSpeed`), rampa de
  0,12 s al arrancar y frenar (`moveRampSeconds`), joystick sin salto inicial
  (`ApplyDeadZone`) y giro a saltos de 30° (`snapAngle`). Los campos tienen
  nombre nuevo para que la escena (que guarda `moveSpeed: 1.8` y
  `snapTurnAngle: 45`) no los pise: **no volver a los nombres anteriores**.
  Para probar valores, cambiarlos en el Inspector durante Play, sin guardar.
- **Tarjetas y resumen** (`BurnGuide`, `BurnSummaryPanel`, `RoundedPanel`,
  `PanelText`, `OverlayPanel`): paneles redondeados con TextMesh Pro, siempre
  encima de la escena, con puntitos de progreso; resumen con puntaje,
  indicadores, pasos, decisión médica y "qué mejorar"; botón tipo píldora.
- **Interacción:** barra de progreso al sacar el anillo (`PatientRing`),
  film y bolsa en amarillo (`BurnSceneProps`), la cuchara no atraviesa la olla
  (`PatientBurnArm`) y la chompa cae desde los hombros y se balancea
  (`SweaterDrape`).

## 9. Arco del juego (`corregir-bugs`)

| Qué | Cómo funciona | Script |
|---|---|---|
| Modo evaluación sin puntos dorados | El grifo, la perilla y el anillo solo muestran su punto en modo guiado. El círculo de progreso del anillo y el brillo al acercar la mano al brazo siguen en los dos modos. | `IndicatorUtil.HintsVisible` |
| Seguridad propia | Tocar la olla volcada (caliente 10 min) es error una vez. La mano suelta la olla sola, vibra y sisea. Pararse sobre el charco del piso ya no es error (sección 12). | `PlayerSafety`, `PotAccident.TryGetHotFloorPuddle` |
| Calmarla (min 3) | Se asusta y pregunta si le quedará marca. Tres respuestas al azar: calmarla, asustarla (error) o pasta dental (error). | `PatientDialog` |
| Resistencia (min 9) | El agua le parece muy fría y duda. "Aguanta, faltan N minutos" o templar el agua (sección 12) la convencen; "sácala un rato", no responder en 14 s o "hielo" hacen que la saque y el reloj se pausa. | `PatientDialog`, `PatientBurnArm.Hesitate/Withdraw` |
| Pausa | Botón de menú izquierdo (o gesto de menú de la mano; Escape o P en el editor). Detiene el tiempo y el sonido. "Continuar" o "Reiniciar práctica". | `PauseMenu` |
| Resumen | Barra de los 4 actos con pasos y errores de cada uno; "qué mejorar" con hasta 8 líneas; botón "Otro caso" (el otro grado de quemadura). | `BurnSummaryPanel`, `BurnProtocol.CountAct`, `BurnInjury.NextDegree` |

Botones (`TouchButton`): si el panel aparece donde ya está la mano, hay que
sacarla y volver a entrar (antes, una mano en reposo elegía sola). En pausa
detectan la mano por su posición, porque las esferas físicas no se mueven sin
`FixedUpdate`.

### Probar

1. Elegir modo evaluación: no hay puntos dorados en el grifo ni en la perilla.
2. Tras el accidente, tocar la olla del piso: queda como error en el resumen
   ("No toques la olla…").
3. Minuto 3 y minuto 9 del reloj: responder a la tía. Probar "sácala un
   rato": ella saca la mano y hay que volver a llevarla al chorro.
4. Pausar con el botón de menú izquierdo y continuar; luego reiniciar.
5. En el resumen, "Otro caso" reinicia con el otro grado.

### Valores para ajustar

- `PatientDialog > Worry Minute / Resist Minute / Answer Seconds`.
- `PlayerSafety > Pot Hot Seconds / Touch Distance / Step Radius Fraction`.

### Sin celular (2026-11-05)

> En `consolidada` volvió un celular, pero para la **llamada al 106** de
> `experiencia-inmersiva` (sección 13), que reemplaza al panel de decisión.
> Lo que sigue describe la versión de `corregir-bugs`.

Se quitó el celular y el paso "Pedir ayuda a alguien de la casa": ninguna
guía (NHS, Cruz Roja Británica, LSEBN) lo pide. El "Call" del protocolo es
llamar a emergencias o pedir consejo médico si la quemadura es seria, y eso lo
cubre la decisión médica. El resumen queda con 7 pasos. El valor
`BurnEvent.HelpRequested` sigue en el enum, sin uso, porque las escenas
guardan el número.

## 10. Bugs de la prueba en Quest 2 (`corregir-bugs`)

Los 38 hallazgos de `Bugs_Experiencia_VR_Quemaduras.pdf` (olla fija antes
del accidente, brazo de la tía, agarres, llama, chompa, paneles, área de
juego, aspecto de la quemadura, etc.) están arreglados. El detalle de cada
uno, con los scripts y valores, está en `docs/PENDIENTES.md` del commit
`e8d5756` (`git show e8d5756:docs/PENDIENTES.md`).

## 11. Cubrir con film en tiras o bolsa plástica (`corregir-bugs`)

Respaldo: NHS y Cruz Roja Británica (film transparente puesto a lo largo, sin
envolver, porque la mano se hincha; para una mano sirve una bolsa plástica
limpia) y la guía LSEBN ("Cover").

| Qué | Cómo funciona | Script |
|---|---|---|
| Rollo de film | Se sostiene con una mano. Mientras tanto, el borde de la tira espera en la superficie del rollo, del lado de la otra mano; al agarrar el rollo con esa mano se toma el borde (no se cambia el rollo de mano). Al tirar, la tira se estira desde el rollo y se corta a los 15 cm (o al soltarla si ya mide 8 cm). Si se suelta el rollo, la tira se corta si ya medía 8 cm. | `FilmRoll`, `FilmStrip` |
| Poner una tira | Se suelta la tira sobre la mano quemada (a menos de 15 cm). Si va a lo largo (de la muñeca a los dedos) cubre una franja de 6 cm del lado donde se apoyó, como una capa sobre la piel que sigue a la mano. Cada tira queda un poco más afuera que la anterior. | `FilmCoverage`, `HandSkinMesh` (filtro de triángulos), `HandFrame` |
| Cuánto falta | Se mide sobre la máscara de la quemadura (`BurnSkinOverlay`): con 80 % cubierto pasa a "Covered". En la tarjeta del narrador se ve el porcentaje. Con la quemadura de prueba hacen falta 3 tiras, una al lado de la otra (13 %, 59 %, 84 %). | `FilmCoverage`, `BurnGuide` |
| Envolver | Dar la vuelta a la mano quemada con la tira en la mano (unos 270° alrededor de su eje largo, a menos de 15 cm) o soltar la tira atravesada la deja **envuelta**: se ve una banda de film alrededor de la mano durante 6 s y se retira. No cuenta para cubrir. La tía dice "me aprieta", el narrador corrige y es error una vez (`BurnEvent.FilmWrapped`, valor nuevo al final). Ver sección 16. | `FilmStrip` (gesto), `FilmCoverage.Wrap`, `BurnPatientReactions`, `BurnProtocol` |
| Bolsa plástica | En el mesón, junto al film. Se suelta sobre la mano y la cubre entera hasta la muñeca, holgada. Cuenta como cubierta correcta (`CoverItem.Kind.Bolsa`, valor nuevo al final). | `CoverItem`, `BurnSceneProps` |
| Gasa | Se quitó (sección 12). | — |

El paso del resumen se llama ahora "Cubrir sin apretar (film o bolsa)".

### Voces nuevas (TTS)

- Narrador (Raúl): `guia_cubrir` (texto nuevo de la tarjeta) y
  `guia_err_envolver` ("No envuelvas su mano: pon las tiras a lo largo,
  sueltas.").
- Paciente (Sabina): `voz_aprieta` ("¡Ay, me aprieta! No me envuelvas la
  mano, por favor.").

### Probar

1. Terminar de enfriar. Tomar el rollo de film con una mano.
2. Con la otra mano, agarrar el rollo: se toma el borde. Alejarla hasta que
   la tira se corte.
3. Soltar la tira sobre el dorso de su mano, a lo largo. La tarjeta muestra
   el porcentaje cubierto. Repetir al lado hasta llegar a 80 %.
4. Probar una tira atravesada: ella se queja y en el resumen aparece "No
   envuelvas la mano con el film…".
5. En otra partida, soltar la bolsa plástica sobre su mano: queda cubierta de
   una vez.

**Probado en el editor** colocando las tiras y la bolsa por código (3 tiras
a lo largo: 84 % y "Covered"; tira atravesada: error una sola vez; bolsa:
"Covered" sin errores). **Falta probar con las manos** tirar del rollo (en el
Quest o con Quest Link).

### Valores para ajustar

- `FilmRoll > Strip Width / Cut Length / Min Keep Length / Regrab Seconds`.
- `FilmStrip > Attach Distance / Loose Seconds`.
- `FilmCoverage > Coverage Needed / Lengthwise Min / Length Margin / Burn Alpha Min`.
- Materiales `Film_Tira` (la tira en la mano del jugador), `Film_Mano`
  (sobre la piel), `Bolsa_Mano` y `Bolsa_Plastico`.

## 12. Revisión contra el protocolo real (`corregir-bugs`, 2026-11-05)

Tres mecánicas que no coincidían con las guías (NHS, Cruz Roja Británica,
LSEBN: "Stop, Remove, Cool and warm, Cover, Call").

| Qué | Cómo funciona ahora | Script |
|---|---|---|
| Templar el agua (min 9) | Las guías piden agua fresca o tibia, no helada. Cuando ella dice que el agua está muy fría hay dos respuestas correctas: "Aguanta un poco más" o "Espera, te templo un poco el agua con la llave caliente". Con la segunda, ella dice "ábrela solo un poquito, que la caliente quema" y espera hasta 12 s con la mano en el agua a que se abra también la llave caliente (con las dos abiertas sale tibia, `WaterTap.Heat` = 0,5). Si se templa: "así está mejor, aguanto" (`CoolingConvinced`, detalle `Templada`). Si no: saca la mano (`CoolingGaveUp`, detalle `SinTemplar`) y el reloj se pausa. También vale abrir la caliente directamente con el panel abierto, sin tocar ningún botón. Si el agua ya sale tibia al llegar el minuto 9, no se queja. El panel tiene ahora 4 respuestas (al azar). | `PatientDialog` |
| Llave caliente con la fría abierta | El narrador ya no la corrige ("Esa es la llave caliente…"): solo templa el agua. Sola sigue siendo error al caer sobre la quemadura (`HotWaterOnBurn`). | `BurnGuide` |
| Charco del piso | Ya no es error: el agua derramada se enfría rápido y no quema a través del zapato. Al pisarlo vibra y suena una gota; en modo guiado el narrador avisa "El piso mojado resbala: rodea el agua derramada" (`HotSpillStepped` sin gravedad). Tocar la olla caliente sigue siendo error. | `PlayerSafety`, `BurnGuide` |
| Gasa | Se quitó del mesón: una gasa seca puede pegarse a la herida y las guías prefieren film o una bolsa plástica limpia. `CoverItem.Kind.Gasa` queda en el enum sin uso (las escenas guardan el número). Se borraron los materiales `Gasa_Mano` y `Gasa_Paquete`; `Gasa_Trama.png` se queda porque la usa `Remedio_Algodon`. | `BurnSceneProps`, `CoverItem` |

### Voces nuevas (TTS)

- Paciente (Sabina): `voz_templar_pide` ("Ya, pero ábrela solo un poquito,
  que la caliente quema.") y `voz_templada` ("Ay, así está mejor. Aguanto.").
- Narrador (Raúl), regrabadas con texto nuevo: `guia_err_charco` ("El piso
  mojado resbala: rodea el agua derramada."), `guia_err_pano` ("…Usa film o
  una bolsa limpia.") y `guia_err_aguantar` ("Anímala a aguantar o témplale
  un poco el agua: hay que completar los veinte minutos.").

### Probar

1. Llevar su mano al chorro frío y esperar al minuto 9 del reloj.
2. Elegir "te templo un poco el agua" y tocar la llave caliente (punto rojo):
   ella dice "así está mejor" y sigue en el agua; sin errores en el resumen.
3. Otra vez, elegir esa respuesta y no abrir la caliente: a los 12 s saca la
   mano y hay que volver a llevarla.
4. Otra vez, sin tocar el panel, abrir la caliente: cuenta como templar.
5. Tras el accidente, pararse sobre el charco del piso: vibra, el narrador
   avisa (modo guiado) y el resumen no muestra error.
6. En el mesón junto al lavadero solo están el film y la bolsa.

**Probado en el editor** forzando el minuto 9 por código: con el panel
abierto, abrir la caliente da `CoolingConvinced Templada`; elegir templar y
abrir la caliente después, también; sin responder, `CoolingGaveUp
SinRespuesta`; ningún error al templar; la gasa ya no aparece. **Solo
compilado:** el aviso del charco (no se probó pisarlo). **Falta en el Quest:**
tocar las llaves con la mano y ver que el panel de 4 respuestas se lea bien.

### Valores para ajustar

- `PatientDialog > Temper Seconds` (12 s de espera para templar).

## 13. Experiencia más inmersiva (`experiencia-inmersiva`, Araype, 2026-11-06)

Pedido del profesor: que la experiencia sea menos "básica". Probado en Play
en el editor con acciones simuladas y sin sonido; **falta probarlo con las
manos en el visor**.

| Qué | Cómo funciona | Script |
|---|---|---|
| Llamada al 106 (SAMU) | Celular en el mesón. Después del accidente, al llevarlo a la oreja suena el timbre y el operador pregunta qué pasó, si la mano está bajo agua fría y si hay ampollas. Describirla mal es error (`WrongCallInfo`). Recomienda y el jugador decide; la decisión se juzga contra lo que recomendó, sin doble castigo. La guía lo pide durante el enfriamiento (paso "Llama al 106", después de la chompa). La práctica termina con la quemadura cubierta y la llamada hecha; si se cubre sin llamar, la tía lo sugiere y a los 90 s (guiado) o 40 s (evaluación) termina con el paso faltante. Sin celular en la escena se usa el panel de decisión. En modo evaluación la pantalla parpadea. | `EmergencyCall`, `BurnProtocol.Decide` |
| La tía muestra la mano | Tras el sobresalto y otra vez en la llamada, antes de la pregunta de las ampollas: a la altura del pecho, dorso a la vista. No si la mano está en el agua o el jugador la sostiene. | `PatientBurnArm.ShowHand` |
| Anillo | El paso va antes de llevarla al chorro. Tras mostrar la mano, la tía pide el anillo (`voz_anillo_pide`) y deja la mano ofrecida. Al acercar la mano libre aparecen "Mantén apretado para sacarlo" y el círculo de progreso; zona de 10 cm y 1 s. Si se lo salta, se puede sacar bajo el chorro. | `PatientRing`, `PatientBurnArm`, `BurnGuide`, `HoldHint` |
| Quemadura realista | Rama `quemadura-realista` (Laura): mancha irregular en el dorso, relieve, piel levantada en segundo grado y ampollas en cúpula (algunas rotas, translúcidas). Sin línea en la muñeca. | `BurnSkinPattern`, `BurnSkinOverlay`, `BurnInjury`, shader `Hito1/PielQuemada` |
| Tarjetas unificadas | Menú de inicio y decisión médica con el estilo de la tarjeta del narrador (`VRPanelKit.CardPanel`, `BoldText`, `RoundedButton`). | `VRPanelKit`, `BurnGuide`, `MedicalDecisionPanel` |
| No atravesar a la tía | La cámara se corre fuera de su cuerpo y las manos del SDK se quedan en su superficie. | `VRLocomotion.KeepHeadOutOfPatient`, `PatientHandLimiter`, `PatientPhysics.PushOut` |
| Reinicio | Si durante el menú se mueve algo, se abre una llave o se vuelca la olla, al elegir el modo se recarga la escena. | `BurnGuide.SceneDisturbed` |
| Objetos en el piso | Lo que queda quieto en el piso vuelve a su lugar tras 1,5 s (no toca la olla volcada). | `FloorRecovery` |
| Voces | Las 45 frases, los quejidos y la llamada con ElevenLabs (guion en `docs/VOCES.md`). Crédito en el resumen. | `Hito1Audio` |

## 14. Rama `consolidada` (2026-11-07)

Une `experiencia-inmersiva` (que ya traía `quemadura-realista` y
`rediseno-tarjetas`) con `corregir-bugs`. Las dos ramas arreglaron varias
cosas en paralelo; cuando chocaban se decidió así:

| Tema | Se quedó | Se descartó |
|---|---|---|
| Cubrir | Film en tiras y bolsa plástica (`corregir-bugs`, sección 11). Sin gasa: una gasa seca se pega a la herida y el NHS pide film o bolsa. **Revertido el 2026-11-07: vuelve la gasa como opción principal (sección 16).** | La gasa como opción principal y la lámina de film estirada entre las manos de `experiencia-inmersiva` (`CoverItem` de Araype). |
| Decisión médica | Llamada al 106 (sección 13) más los diálogos de la tía y templar el agua (sección 12). | El panel como paso final cuando hay celular. |
| Aspecto de la quemadura | El de Laura (`BurnSkinPattern`). `FilmCoverage` mide con su canal rojo. | El shader de `corregir-bugs` en `Assets/Hito1/Shaders/PielQuemada.shader` (tenía el mismo nombre). |
| Olla fija | `PotAccident` de `corregir-bugs` (además expone el charco a `PlayerSafety`) más el reintento de apagar los agarres de Meta. | `SetLocked` de Araype. |
| Agarres | `SimpleGrab` de `corregir-bugs` (sin agarre a distancia, acercar a la mano, `OnPulledAway`) más buscar los agarres de Meta los primeros 30 s. | El emparejamiento de interactores por distancia de Araype. |
| Perilla | `StoveKnob.CanToggle` de `corregir-bugs` (cubre lo mismo que la versión de Araype). | `BurnHappened`. |
| La tía al quemarse | `StepAwayFromStove` de Araype (se aparta hacia el lavadero y luego muestra la mano). | `StepBackFromStove`. Un solo `PatientReach.WalkTo(punto, velocidad)`. |
| Movilidad | Los dos: límite del área de juego (`PlayArea.Clip`) y no atravesar a la tía. | — |
| Resumen | "Repetir práctica" y "Otro caso" (`corregir-bugs`) más el crédito de ElevenLabs. | — |
| Enum `BurnEvent` | Los valores nuevos de los dos lados, al final; `FilmWrapped` una sola vez. | — |

Además, la tía no pregunta nada durante la llamada y la llamada no empieza
mientras ella espera una respuesta (`PatientDialog`, `EmergencyCall`).

**Solo compilado** (Roslyn fuera de Unity). Qué probar primero:
`docs/PENDIENTES.md`, sección 0. Voces por regenerar: `docs/VOCES.md`.

## 15. Botones con el dedo y brazo más natural (`mejoras-demo`, 2026-11-07)

Pedido tras probar `corregir-bugs` en el Quest: costaba apretar los botones
(sobre todo en el resumen), la mano "se transparentaba" al acercarse a la
tarjeta y el brazo de la tía se veía rígido y se contorsionaba al llevarla.

| Qué | Cómo funciona | Script |
|---|---|---|
| Botones con la punta del índice | El botón cuenta la punta del índice de la mano visible (con manos o con controles, que también dibujan una mano). Antes solo contaba un punto invisible a la altura de la palma, 3 cm delante del control, y la mano dibujada llegaba ~10 cm más adelante. El dedo cuenta desde 3,5 cm delante del botón hasta 3 cm detrás. Sigue valiendo la esfera de la mano. | `SimpleGrab.TryGetFingertip`, `controllerFingertipOffset`, `TouchButton` |
| Cursor del dedo | Círculo sobre la tarjeta donde apunta el dedo, cuando está a menos de 20 cm de un botón: se achica al acercarse y se pone celeste dentro del botón (mientras se llena la barra). Lo agrega cada botón a su panel. | `PanelFingerCursor` |
| La tarjeta ya no tapa la mano | Las tarjetas se dibujan encima de todo (para que no las tapen paredes ni la tía) y por eso tapaban también la mano. Con una mano a menos de 25 cm del panel vuelven a usar la profundidad normal: el dedo se ve delante. | `OverlayPanel.handNearDistance` |
| Mano relajada | Los dedos se doblan un poco (más el meñique que el índice) y la muñeca cede hacia la palma. Antes quedaban rectos, como en la T-pose. Bajo el chorro la mano sigue plana. | `PatientBody.fingerCurlDegrees`, `wristRelaxDegrees`, `RelaxHand` |
| Menos contorsión | Torso: inclinación máxima 24° (antes 35°) más 10° si no llega (antes 15°), giro 15° (antes 25°). El brazo se estira hasta el 92 % (antes 97 %) y da un paso al 72 % (antes 85 %). Se suelta si se tira a más de 45 cm (antes 60). La mano no sube más de 25 cm sobre el hombro. Los campos tienen nombre nuevo para que no los pisen los valores guardados en la escena: **no volver a los nombres viejos**. | `PatientReach.armReach`, `stepAtFraction`, `leanLimit`, `leanReachExtra`, `twistLimit`; `PatientBody.maxAboveShoulder`; `PatientPhysics` |

Jugar **sin controles** ya era posible: el proyecto acepta manos y
controles (`OculusProjectConfig`, `handTrackingSupport`). Con las manos se
agarra con la pinza de pulgar e índice y se camina con la pinza del medio o
del anular (`VRLocomotion`).

**Probado en el editor** (Play con Quest Link, 2026-11-07): con la punta del
dedo simulada por código delante de "Modo guiado", a 8 cm aparece el
círculo, a 2 cm se pone celeste y se llena la barra, y al completarse elige
el modo guiado; con las manos reales (seguimiento de manos por Quest Link)
el círculo sigue a la punta del dedo. Con el accidente forzado y la llave
fría abierta, la tía sigue llegando al chorro (estado `Cooling`, torso a
34°) con la mano plana y los dedos algo doblados. **Falta en el Quest:**
ver el dedo delante de la tarjeta, apretar con controles y llevarla del
brazo con las manos.

### Probar

1. Menú de inicio: acercar el índice a "Modo guiado". Aparece el círculo,
   se achica y se pone celeste al tocar; el dedo se ve delante de la tarjeta.
2. Lo mismo en el resumen ("Repetir práctica", "Otro caso") y en la pausa.
3. Dejar los controles y repetir con las manos.
4. Llevar a la tía del brazo: dedos algo doblados, sin doblarse de cintura
   ni girar el torso de más; da un paso antes de estirar el brazo recto.
5. Llevarla al chorro: la mano debe seguir llegando bajo el agua.

### Valores para ajustar

- `SimpleGrab > Controller Fingertip Offset` (si con controles el círculo
  no coincide con la punta del dedo).
- `PanelFingerCursor > Show Distance / Near Diameter / Far Diameter`.
- `OverlayPanel > Hand Near Distance`.
- `PatientBody > Finger Curl Degrees / Wrist Relax Degrees / Max Above Shoulder`.
- `PatientReach > Arm Reach / Step At Fraction / Lean Limit / Lean Reach Extra / Twist Limit`.

## 16. Arreglos de la prueba en el visor (`mejoras-demo`, 2026-11-07)

Probado **en el editor** (Play en `Prueba_LuzHorneada`, sin visor, con las
manos simuladas por código). Falta probar con las manos en el Quest
(`docs/PENDIENTES.md`, sección G).

| Bug | Arreglo | Dónde |
|---|---|---|
| "Tazas infinitas": a la tía se le caía la taza y aparecía otra en el mesón | Si no había mesón a su izquierda, la soltaba en el aire a la altura del mesón; la taza caía y `FloorRecovery` la devolvía a donde estaba al inicio, como si fuera otra. Ahora busca mesón hasta 2 m y, si no hay, la deja donde estaba la taza. `FloorRecovery` usa el lugar de `Grabbable` (cambia con `SetHome`), no el primero que vio. | `BurnGuide.PutDownSpot`, `FloorRecovery`, `Grabbable.HomePosition` |
| La tía recibía la taza con la muñeca doblada | La mano va al costado de la taza, la recibe con la palma hacia arriba y la taza va derecha hasta el mesón; la mano se abre al soltar. | `PatientBody.GripPose`, `PatientBurnArm.Receive` |
| El jugador quedaba parado encima de la olla | La cápsula se subía a los objetos con Rigidbody. Ahora la olla (y los objetos del SDK de Meta) se **empujan al caminar**: la cápsula ya no choca con ellos y, si uno queda dentro de su radio, se corre hacia afuera en horizontal a la velocidad del jugador (0,3 a 0,8 m/s). Antes del accidente la olla está fija (cinemática) y no se mueve. Puertas y cajones siguen frenando. Si igual se apoya en otro objeto suelto, se desliza hacia afuera. Probado en Play: caminando contra la olla en el piso se corrió 27 cm y el jugador no se subió. El charco y el vapor no siguen a la olla. | `VRLocomotion.PushObjects`, `maxPushSpeed`, `minPushSpeed`, `SlideOffProp` |
| Caer al vacío junto al mesón | El piso y las paredes tienen colisión (revisado con una rejilla de rayos). Red de seguridad: si el jugador cae 0,8 m por debajo del último piso firme, vuelve ahí. | `VRLocomotion.GuardAgainstFalling` |
| No se podía sacar una tira de film | La lógica funcionaba (al tirar 24 cm se corta y queda otra lista), pero el borde era un collider invisible. Ahora hay una solapa de film visible en el borde, del lado de la mano libre, y en modo guiado brilla. Texto del paso 7 actualizado. | `FilmRoll` (solapa, `EdgeWaiting`), `BurnGuide` |
| Al estirar el film no se cortaba; además era casi invisible | Si la mano soltaba la tira un instante (con las dos manos juntas el seguimiento de manos pierde la pinza, o al tirar rápido la tira quedaba atrás y se soltaba sola), el borde volvía al rollo y se perdía lo estirado. Ahora, si ya mide 8 cm, se corta y cae (se puede volver a tomar); si es más corta, espera 0,5 s donde quedó. Se corta a los 15 cm (antes 20). Más opaco: tira 60 % (antes 32 %), capa sobre la mano 42 % (antes 16 %). Probado en Play: soltar a 10 cm corta, a 4 cm vuelve al rollo tras 0,5 s, a 16 cm corta en la mano. | `FilmRoll.StripLetGo`, `FilmStrip.LetGo`, `Film_Tira.mat`, `Film_Mano.mat` |
| El gesto de envolver la mano con el film no hacía nada | Con una tira cortada en la mano, dar unos 270° de vuelta alrededor de la mano quemada (a menos de 15 cm de su eje) la envuelve: banda de film visible alrededor de la mano (6 s), error `FilmWrapped` una vez, queja de la tía y corrección. Soltarla atravesada también la deja envuelta en vez de desaparecer. Probado en Play moviendo la tira por código en 55° por paso. | `FilmStrip.TrackWrapGesture`, `FilmCoverage.Wrap`/`WorldAxis` |
| La taza flotaba al costado de la mano de la tía | La taza se pone frente a su palma, tocándola, y pasa suave desde donde la soltó el jugador. | `PatientBody.PalmGripPoint`, `PatientBurnArm.HoldInPalm` |
| La tía recibía la taza sin dejar de cocinar, de forma robótica | Al acercarse el jugador con la taza en la mano (a menos de 2,2 m) deja de revolver (la cuchara queda apoyada en la olla y la mano baja junto al cuerpo) y se voltea hacia él; recibe la taza, vuelve a mirar la cocina, la deja en el mesón a su izquierda y sigue cocinando. Mientras se le pide mirar a un punto, `PatientReach` ya no la gira hacia su mano (las dos cosas se anulaban). Gira a 150°/s con aceleración suave y da pasos en el lugar mientras gira: se voltea en ~1,1 s (antes más de 3 s, a 48°/s). Probado en Play midiendo el ángulo cada 0,25 s. | `BurnGuide.attendDistance`, `PatientBurnArm.AttendPlayer`/`Receive`/`UpdateFacing`, `PatientReach` |
| Llevarla del brazo parecía arrastrar un objeto | Llevada del brazo camina a paso de persona (máx. 0,8 m/s, antes 1,5) y gira el cuerpo hacia donde camina, así las piernas dan pasos hacia adelante en vez de patinar de costado o de espaldas. **Solo compilado**: no se pudo probar en el editor sin manos reales. | `PatientReach.ledWalkSpeed`, `ledTurnSpeed`, `FaceWhereWalking` |
| **Vuelve la gasa como opción principal para cubrir** (decisión del equipo, 2026-11-07; revierte lo de la sección 14) | Paquete de gasa estéril junto al lavadero, con el film y la bolsa. Se apoya sobre la quemadura ya enfriada y se mantiene encima 1 s (círculo de progreso, `HoldHint`; ella ofrece la mano al acercarla); también se pone si se suelta sobre la mano. Queda una capa de gasa solo sobre la quemadura. Antes de terminar de enfriar es error (`CoveredTooEarly`). El film en tiras y la bolsa siguen como alternativa. Textos: paso 7 "Cubre con una gasa limpia", corrección del paño, resumen ("Cubrir con una gasa limpia (o film si no hay)") y subtítulo del operador. Voces de ElevenLabs recuperadas de `experiencia-inmersiva` (44b2fb2): `guia_cubrir`, `guia_err_pano`, `guia_err_envolver`; `op_centro` ya era la de la gasa. Materiales `Gasa_Mano` y `Gasa_Paquete` recuperados. Respaldo: la Cruz Roja Británica acepta un apósito estéril que no suelte pelusa. Probado en Play: la gasa aparece en el mesón; sobre la quemadura antes de enfriar, 1 error y no se pone; ya enfriada, se puso a los 1,3 s, quedó la capa y el estado pasó a Covered. | `BurnSceneProps.BuildGauze`, `CoverItem` (Gasa), `BurnGuide`, `BurnProtocol`, `EmergencyCall` |
| Al ofrecer la mano para sacar el anillo, la tía seguía al jugador | La mano ofrecida (a 33 cm del pecho) quedaba más lejos que el umbral para dar un paso (`stepAtFraction`, bajado a 0,72 en la sección 15): daba un paso, la mano se adelantaba con ella y volvía a dar otro, aunque el jugador retrocediera (unos 3,5 cm/s). Ahora la mano ofrecida queda dentro de lo que alcanza sin caminar. Probado en Play: con el jugador a 1 m, antes avanzaba 20 cm en 6 s; ahora 0 cm, y sigue ofreciendo la mano a la vista. | `PatientBurnArm.ShowGoal`, `WithinStep` |
| El jugador quedaba subido al mesón (al ponerle la chompa a la tía) | La cápsula del jugador chocaba con las 14 cápsulas del cuerpo de la tía (piernas hasta 0,65 m, muslos hasta 1,1 m) y, como son redondeadas, se subía por ellas y de ahí al mesón. Ahora no chocan (la cabeza sigue sin poder entrar en su cuerpo, `KeepHeadOutOfPatient`). Red de seguridad: si queda parado a más de 30 cm sobre el piso, vuelve al último lugar firme en el piso. Probado en Play: 0 de 14 cápsulas chocan; puesto sobre el mesón, vuelve al piso en menos de 0,5 s. | `VRLocomotion.IgnorePatientBody`, `maxStandAboveFloor`, `GuardAgainstFalling` |
| Los objetos agarrados flotaban a varios centímetros de la mano | El punto de agarre salía de `OVRSkeleton` (o del ancla de la mano si no estaba listo) o de un desfase fijo del control, y no coincidía con la mano dibujada. Ahora se agarra en la pinza (pulgar e índice) de la mano visible (`HandVisual` del SDK de Meta), con manos o con controles; si no se está dibujando, se usa el cálculo de antes. También la punta del índice para los botones sale de ahí. **Solo compilado**: sin visor no hay mano visible en el editor. | `SimpleGrab.TryVisiblePinch` |
| Una tarjeta "desaparecía" al acercarse a leerla | Con la mano a menos de 25 cm, la tarjeta vuelve a usar la prueba de profundidad para que el dedo se vea delante (sección 15); si estaba en parte dentro del mesón, el mesón la tapaba entera. Ahora solo usa la profundidad si nada de la escena la tapa desde los ojos (rayos al centro y a las esquinas). Probado en Play: al aire, no tapada; dentro del mesón, tapada (sigue dibujándose encima). | `OverlayPanel.BlockedFromEye` |
| Objetos que saltaban en el mesón, el celular que "desaparecía" al ir a agarrarlo y objetos del piso que frenaban al jugador | Los objetos sueltos (taza, celular, chompa, film, bolsa, remedios...) ya no chocan con el cuerpo del jugador, con las esferas de sus manos ni con el cuerpo de la tía: las manos los empujaban (el celular, delgado, salía disparado al acercar la mano) y la cápsula del jugador se trababa con ellos. Se siguen agarrando igual (la búsqueda de agarre no usa colisiones). La mano de la tía no entra en esto. | `SimpleGrab.loosePropsIgnorePlayer` |
| La llamada seguía bajito si se soltaba el celular | Se pone en espera: el operador se pausa, el celular brilla y suena cada 2,5 s, y un aviso "Recoge el celular y llévalo a tu oreja para seguir la llamada" sigue la vista hasta que se vuelve a llevar el celular a la oreja. | `EmergencyCall` (`EnterHold`, `FollowHoldPanel`) |

## 17. Animaciones de Mixamo para la tía (`mejoras-demo`, 2026-11-09)

Se evaluó cambiar la tía por un avatar de Ready Player Me: no es viable (el
servicio cerró el 31/01/2026 tras la compra de Netflix) y no arreglaría la
rigidez, que venía de que todo el cuerpo se calculaba por código. Se le
agregaron animaciones de Mixamo (mismo esqueleto `mixamorig8`, sin pasar a
Humanoid) en `Assets/Hito1/Personajes/Animaciones`.

| Qué | Cómo funciona | Dónde |
|---|---|---|
| Capa base | Quieta: respira (`Breathing`) antes del accidente y queda encorvada (`Sad Idle`) después (parámetro `Dolor`). Al moverse, camina (`Walking`, parámetro `Speed` = `PatientBody.walkSpeed`; al girar en el lugar también da pasos). La cadera no avanza por la animación: la mueve `PatientReach`. | `Resources/Hito1Anim/TiaAnimator.controller` |
| Gestos | Capa con máscara de columna, cuello y cabeza (`TiaGestosMascara`). Se disparan con la voz de la tía: golpe al quemarse (`Stomach Hit`), alivio (`relieved sigh`), gracias (`Thankful`), negar (`shaking head no`), asentir (`head nod yes`) y ansiedad (`Nervously Look Around`). | `PatientAnimator.VoiceGestures`, `BurnPatientReactions.Say` |
| Brazos | Siguen la animación mientras están en reposo y pasan suave (0,35 s) a la cinemática inversa cuando van a un objetivo: la mano quemada, la taza, el anillo, cuando el jugador la lleva del brazo. | `PatientBody.LateUpdateAnimated`, `BlendArmWithAnimation` |
| Lo que hace el código encima | Inclinarse, girar el torso, mirar y las reacciones de resorte siguen sumándose sobre la animación. Los pasos procedurales y la respiración del pecho se apagan (ya vienen en la animación). Sin controlador, todo sigue como antes. | `PatientBody.Animated` |

No se usan: `Left Turn` y `Right Turn` (giran la cadera además del giro de
`PatientReach`: se volteaba el doble), `Injured Jog` (es correr) y los demás
gestos del paquete (dependen de los brazos o no encajan). El muñeco de
referencia del paquete (`Ch07_nonPBR.fbx`, 50 MB) se dejó fuera de `Assets`
(`semn13/mixamo_referencia`).

**Probado en Play:** la tía respira cocinando con el brazo izquierdo colgando
de la animación y el derecho revolviendo; al quemarse hace el golpe (0,7 s) y
queda encorvada protegiendo la mano; con `voz_remedio_no` niega con la cabeza;
caminando, `Speed` llega a 0,62 y llega al destino. Sin errores en consola.
**Falta en el Quest:** revisar los 72 fps con el Animator y que la mano
quemada, la taza, el anillo, el film y la gasa se sigan viendo bien.

### Arreglos de la prueba en el Quest con las animaciones (2026-11-09)

| Bug | Arreglo | Dónde |
|---|---|---|
| La mano que sostiene la quemada atravesaba el antebrazo | Va 10,5 cm por debajo de la muñeca quemada y 4 cm hacia el codo (antes 3,5 cm y 7 cm): la sostiene desde abajo. Valores ajustables. | `PatientBurnArm.cradleBelow`, `cradleTowardElbow` |
| El anillo caía del aire una y otra vez | Al sacarlo, su lugar de origen era el punto en el aire donde estaba la mano: caía al piso y volvía ahí. Ahora se queda donde cae; `FloorRecovery` ya no devuelve los objetos con `returnFromFloor` apagado. | `PatientRing`, `FloorRecovery.Eligible` |
| La tarjeta "Está asustada. ¿Qué le dices?" salía mientras el narrador pedía llamar al 106 | Se quitó la pregunta del minuto 3 (`PatientDialog.worryQuestion`, apagada). La del minuto 9 (quiere sacar la mano del agua) sigue. | `PatientDialog` |
| La chompa se abría como alas o quedaba como un bloque delante del pecho (al ofrecer la mano, al recibir la gasa) | La tela se apartaba de las cápsulas del antebrazo y de la mano, que suben al ofrecer la mano. Ahora solo se aparta del torso, las piernas, el cuello y el brazo (hombro a codo); las mangas colgantes siguen apartándose de todo. La chompa es un poco más ancha para caer sobre los brazos. | `SweaterDrape`, `SweaterItem.BuildDrape` |

**Probado en Play** con capturas: la mano izquierda sostiene el antebrazo
desde abajo sin atravesarlo; con la chompa puesta y la tía ofreciendo la mano
(como al acercar la gasa), la chompa queda sobre los hombros y la espalda, sin
alas ni bloque. El anillo y la tarjeta quitada, solo compilados.


### Tarjeta de resultados: botones que no se alcanzaban y opciones que se marcaban solas (2026-11-09)

| Bug | Arreglo | Dónde |
|---|---|---|
| En el resumen final no se podía tocar ningún botón | La tarjeta iba a 1 m y seguía la cabeza: al acercarse se alejaba. Ahora va a 0,62 m (como las otras tarjetas) y al 72 % de su tamaño para que quepa en la vista. La detección de la mano junto al panel se mide en metros aunque el panel esté escalado. | `BurnSummaryPanel.Show`, `PanelPlacement` |
| Las opciones de las tarjetas se marcaban solas | Al bajar la mano hacia un botón, la esfera de la palma pasaba por los de arriba. Si esa mano tiene punta de dedo, ahora solo cuenta el dedo. | `TouchButton` |

**Solo compilado** en el editor (sin errores en consola). Falta probarlo en
el Quest: llegar al resumen y tocar cada botón, y que en las tarjetas con
varias opciones no se marque ninguna al pasar la mano.

## 18. Tía de Avaturn con gestos en la cara (`mejoras-demo`, 2026-11-09)

La tía de Mixamo tenía la cara fija. Se cambió por un avatar de Avaturn
**tipo T2** (cara animable), creado en hub.avaturn.me con fotos de alguien
que dio permiso, en GLB. Trae las 52 expresiones ARKit, 15 visemas para
hablar y huesos con los nombres de Mixamo sin prefijo.

| Parte | Cómo quedó | Dónde |
|---|---|---|
| Importar | Paquete glTFast 6.10.1. El GLB está en `Personajes/Avaturn/TiaAvaturn.glb` (por Git LFS). El original queda fuera del proyecto en `semn13/avaturn/model.glb`. | `Packages/manifest.json` |
| Preparar | Menú **Hito1 > Preparar tia de Avaturn**: crea el avatar Humanoid y el prefab `Resources/Hito1Avatar/TiaAvaturn`, pasa a Humanoid las 24 animaciones de Mixamo y el FBX de la tía anterior (con la raíz horneada: la animación no la mueve ni la gira), y la máscara de gestos a columna y cabeza Humanoid. | `Editor/AvaturnSetup.cs` |
| Materiales | Con los de glTFast los mapas de normales salían mal: media blusa, medio brazo y media cara oscuros (las zonas con la textura en espejo), y la piel se veía gris. Ahora las imágenes se sacan del GLB a `Avaturn/Texturas` (normales como Normal map; en el Quest, ASTC 4x4 para cara, cuerpo y ojos) y los materiales son Standard (`Avaturn/Materiales`). La piel y los ojos no son metálicos y la piel tiene poco brillo. El pelo, las pestañas y las cejas usan `Hito1/CabelloA2C`, con bordes suaves por el antialiasing del Quest (antes se veían pixelados). | `Editor/AvaturnMaterials.cs`, `Avaturn/CabelloA2C.shader` |
| Cambio de modelo | Al dar Play, `PatientBody.Awake` cambia las mallas y el esqueleto de Mixamo por los de Avaturn, pone su avatar en el Animator y deja `bonePrefix` vacío. Los componentes de la tía siguen iguales. La piel se llama `Body_Mesh` y las capas que buscan "Body" (quemadura, agua, gasa, film) la encuentran. Para volver a la de Mixamo: `PatientBody.useAvaturn` apagado. | `PatientModelSwap`, `PatientBody.useAvaturn` |
| Cara | Gesto de fondo según el estado: tranquila cocinando, dolor según `BurnPatientReactions.Pain`, alivio al enfriar o cubrir, agradecida al final. Con cada frase, un gesto corto: golpe de dolor, miedo, alivio, gracias, ceño al negar o si le aprietan la mano. Parpadea sola y los ojos miran al jugador (máximo 22°). | `PatientFace` (lo instala `Hito1RuntimeBootstrap` si el modelo tiene expresiones) |
| Boca | Mientras suena su voz, abre la mandíbula según el volumen del clip y va turnando visemas (aa, O, E, I, U). | `PatientFace.AddTalk` |

Arreglos tras la primera prueba en el visor (2026-11-09):

| Bug | Arreglo | Dónde |
|---|---|---|
| Sombras raras en el cuerpo, brazos con luz dispareja, cara gris y de baja calidad | Materiales Standard con texturas bien importadas (fila "Materiales"). | `AvaturnMaterials` |
| Ojos con demasiado blanco al mirar | Giran por separado en horizontal (máx. 22°) y en vertical (máx. 12°). | `PatientFace.eyeMaxPitch` |
| Al voltearse a recibir la taza, la mano derecha quedaba doblada contra la pierna | Mientras atiende al jugador (la cuchara queda en la olla), ese brazo cuelga como en la animación. Los dedos de Avaturn tienen 3 falanges sin hueso de punta: ahora también se dobla la última. | `PatientBurnArm.UpdateOwnArmRest`, `PatientBody.RelaxHand` |
| Chompa con las mangas paradas como cuernos y la capa abierta como alas | Las mangas solo se apartan del cuerpo hacia los lados o adelante, nunca hacia arriba. La parte baja de la capa ya no se aparta de los brazos. La capa llega hasta el borde de los hombros, con los brazos por fuera. El torso y la cadera de `PatientPhysics` toman su grosor de la malla (16 y 17 cm en la tía de Avaturn; antes 11,5 y 12 fijos, y la blusa atravesaba la capa). | `SweaterDrape`, `SweaterItem`, `PatientPhysics.FitRadius` |

Ojo: glTFast guarda el peso completo de cada expresión como 1, no como 100.
`PatientFace` usa el peso que trae cada forma; si se pasa de ahí, la cara se
deforma.

**Probado en Play** (escena `Prueba_LuzHorneada`), con capturas: la tía
revuelve la olla; la cara se ve tranquila y, al quemarse, pasa a dolor con la
boca abierta; la sonrisa del "gracias" y la boca con la voz funcionan; la
quemadura con ampollas queda en el dorso de la mano nueva, el anillo en el
anular y la otra mano la sostiene desde abajo. Sin errores en consola.
**Falta:** el resto del caso con el modelo nuevo y todo en el Quest (ver
`PENDIENTES.md`, sección F).

## 19. El anillo ya no se traba antes de tiempo (`mejoras-demo`, 2026-11-09)

En el visor casi nunca se podía sacar el anillo. Medido en Play con una
quemadura de segundo grado: sin enfriar, la hinchazón llegaba a 0,6 a los
42 s, y el primer intento lo trababa para siempre. La guía pide antes apagar
la hornilla y abrir el agua, así que casi nadie llegaba a tiempo. Además, al
trabarse, el punto dorado desaparecía sin explicación.

| Cambio | Dónde |
|---|---|
| La mano tarda 150 s sin agua en hincharse del todo (antes 70) y el anillo se traba con 0,9 de hinchazón (antes 0,6): unos 2 min 15 s sin enfriar, y bajo el agua 8 veces más. En primer grado nunca se traba (no pasa de 0,55). | `BurnInjury.swellSecondsUntreated`, `PatientRing.stuckSwelling` |
| Si se traba, en modo guiado aparece "¡Cuidado! Su mano ya se hinchó: no fuerces el anillo. Sigue con el agua fría; en el centro de salud se lo sacarán." (la voz `guia_err_anillo` falta grabarla: `VOCES.md`). La tía ya decía "¡No sale, ya se me hinchó el dedo!". | `BurnGuide.HandleEvent` |
| La explicación del resumen dice que hay que sacarlo apenas se empieza a atenderla, y que si ya no sale no se fuerza. | `BurnProtocol.Explain` |

**Probado en Play:** a los 111 s sin enfriar la hinchazón va en 0,74 y el
anillo todavía sale. Forzando el caso trabado en modo guiado, aparece la
tarjeta de aviso y el error queda en el resumen. **Falta** sacarlo con la mano
real en el Quest.

## 20. Voces espaciales, más vibración y anillo deslizable (`mejoras-demo`, 2026-11-09)

| Cambio | Cómo quedó | Dónde |
|---|---|---|
| Voces espaciales | Todas las fuentes 3D (la voz y la respiración de la tía, los efectos y el operador del 106 en el celular) usan el HRTF de Meta XR Audio, que ya era el espacializador del proyecto. Con audífonos se oye de dónde viene, también arriba, abajo y atrás. El narrador sigue en off (sin espacializar). | `Hito1FX.Spatialize` (en `CreateSource`), `EmergencyCall` |
| Más vibración | Un toque corto en la mano que agarra algo; vibración áspera en las dos manos al cortar una tira de film; suave y creciente en la mano que sostiene la gasa sobre la quemadura. | `BurnHaptics.HandleGrab`, `FilmStrip.Tear`, `CoverItem` |
| Anillo deslizable | Se pellizca y se desliza hacia la punta del dedo (3,5 cm). El anillo sigue a la mano, y el avance se mide sobre el dedo: si la tía mueve la mano, no cuenta. Vibra más al deslizarlo. Mantener el pellizco sin moverse también lo saca, pero en 3 s, por si el gesto cuesta con seguimiento de manos. Si se suelta antes, vuelve. Si la mano está muy hinchada, se traba al llegar al 25 %. | `PatientRing.slideDistance`, `holdSeconds`, `TipDirection` |

Los textos dicen "Pellízcalo y deslízalo hacia la punta" (junto al anillo) y
"Pellizca su anillo y deslízalo hacia la punta del dedo" (tarjeta de la
guía). La voz `guia_anillo` todavía explica el gesto viejo y hay que
regrabarla (`VOCES.md`).

**Probado en Play:** las 9 fuentes de audio de la escena usan Meta XR Audio;
al terminar de sacarlo, el anillo queda suelto en el piso y el paso se marca
como hecho; sin errores en consola. **Falta en el Quest:** el gesto de
deslizar con las manos y con los controles, cómo se oyen las voces con
audífonos y cómo se sienten las vibraciones nuevas.

## 21. Botiquín con sobres de gasa (`mejoras-demo`, 2026-11-09)

Antes la gasa estaba suelta en el mesón (un cubo amarillo). Ahora está en un
botiquín, y hay que abrirlo y rasgar el sobre como en la vida real. Todo se
arma por código (sin modelos importados).

| Parte | Cómo funciona | Dónde |
|---|---|---|
| Botiquín | Caja blanca con cruz roja y rótulo "PRIMEROS AUXILIOS" junto al lavadero, con el frente hacia la tía. La tapa se abre tomando el tirador gris (botón lateral o pinza) y levantándola; un toque corto en el tirador la abre o la cierra. Con la tapa cerrada no se puede sacar nada de adentro. | `FirstAidKit`, `BurnSceneProps.PlaceKit` |
| Contenido | 2 sobres de gasa estéril, una venda (de adorno) y una pomada. La pomada es un distractor: ponerla es un error ("Ninguna crema ni pomada va sobre una quemadura recién hecha…"). | `FirstAidKit.Fill`, `HomeRemedy.Remedy.Pomada` (al final del enum) |
| Sobre de gasa | Rótulo "GASA ESTÉRIL 10 × 10 cm" y una esquina roja. Con el sobre en una mano, se pellizca la esquina con la otra y se separan las manos 5 cm: se rasga (sonido y vibración), la gasa queda en la mano que tenía el sobre y el sobre roto cae. | `GauzePacket`, `SimpleGrab.ForceGrab` (nuevo) |
| Gasa | Cuadrado de algodón doblado, con textura de tejido y mapa de normales generados en el editor (menú Hito1 > Generar textura de la gasa). Sigue cubriendo igual que antes (mantenerla 1 s sobre la quemadura). | `BurnSceneProps.BuildGauze`, `Editor/FirstAidTextures.cs`, `Hito1FX/Gasa_Tejido` |
| Guía | El paso "Cubre con una gasa limpia" explica abrir el botiquín y rasgar el sobre, y marca el tirador (con la tapa cerrada) o los sobres (abierta). | `BurnGuide` |

El film y la bolsa siguen sueltos en el mesón: son la alternativa casera.

**Probado en Play:** el botiquín aparece sobre el mesón con todo adentro;
la tapa abierta por código se ve bien; al rasgar un sobre por código aparece
la gasa nueva y el rótulo se lee desde el frente. **Falta en el Quest:**
abrir la tapa y rasgar el sobre con las manos y con controles, y comprobar
que la gasa queda en la mano.

## 22. Boca con visemas sacados del texto (`mejoras-demo`, 2026-11-09)

Antes la boca se abría según el volumen, turnando vocales al azar. Ahora
cada frase de la tía tiene una pista de visemas sacada de su texto.

- **Cómo se arma** (menú **Hito1 > Generar visemas de las voces**): lee el
  texto de cada `voz_*` en `docs/VOCES.md` y lo pasa a los 15 visemas de
  Oculus que trae el avatar. El español se pronuncia casi como se escribe:
  vocales a aa, E, I, O, U; b, v, p y m a PP; f a FF; t y d a DD; n, ñ y l a
  nn; r y rr a RR; s, z y c (ante e, i) a SS; c, k, q, g y j a kk; ch, ll y
  y (ante vocal) a CH; la h es muda. Las vocales duran más que las
  consonantes. Después reparte la secuencia sobre los tramos del audio
  donde hay voz (por encima del 12 % del volumen máximo, en pasos de
  10 ms); en los silencios, la boca se cierra.
- **Resultado:** `Resources/Hito1Anim/VisemasVoces.json`, con 30 pistas (las
  frases que tienen texto y audio).
- **Al hablar** (`PatientFace`): toma el visema que toca en ese momento del
  audio, lo abre según el volumen y abre la mandíbula según la vocal (más
  con "a", poco con "i" y "u", nada con p, b y m). Las frases sin pista
  siguen con el método anterior.
- **Al cambiar un texto o un audio**, hay que volver a correr el menú.

**Probado en Play:** con "Ay, mi anillo… gracias…", a los 1,49 s la boca
estaba en "I", con la mandíbula poco abierta. Sin errores en consola.
**Falta en el Quest:** ver de cerca si la boca acompaña bien la voz.

## 23. Poner la gasa bien: bordes, sin apretar, centrada y con venda (`mejoras-demo`, 2026-11-09)

Antes bastaba acercar la gasa a la quemadura 1 s, y la capa aparecía de
golpe con la forma de la quemadura. Ahora se pide hacerlo como en la vida
real:

| Qué | Cómo funciona | Dónde |
|---|---|---|
| Tomarla de los bordes | Si se agarra la gasa por el centro (la parte que va sobre la herida: 5,6 × 5,6 cm al medio), es un error leve: "Toma la gasa de los bordes…". Se cuenta una vez. La gasa que sale del sobre ya queda en la mano y no cuenta. | `CoverItem.HandleGrab`, `GauzeTouchedCenter` |
| Plana y centrada | Solo se llena el círculo si la gasa está plana sobre el dorso (inclinada menos de 50°) y centrada (corrida menos de 4,5 cm). Si no, la indicación dice "Ponla plana…" o "Céntrala…". | `CoverItem.UpdateGauze`, `Placement` |
| Sin apretar | Si la mano del jugador se hunde más de 1,2 cm bajo la piel de la tía, no avanza, la indicación dice "Sin apretar: solo apóyala" y vibra seco. Si sigue apretando 0,35 s, ella se queja y es un error. | `pressDepth`, `GauzePressed` |
| Se amolda | Al completarse, la gasa baja, se pone plana sobre el dorso siguiendo la mano y se aplana en 0,45 s. Después queda una capa de gasa donde se apoyó, que aparece en 0,3 s, con el tejido a su tamaño real (coordenadas de textura planas) y casi opaca. | `SettleGauze`, `GauzeLayer`, `PlanarUV`, `Hito1FX/Gasa_Puesta` |
| Cubrir toda la quemadura | Se calcula cuánto de la quemadura (4 cm de radio) tapa la gasa: centrada o corrida hasta 2 cm, 97-100 %; a 3 cm, 84 %; a 4,5 cm, 60 %. Por debajo del 85 % es un error ("La gasa quedó corrida…") y la quemadura asoma bajo la capa. | `Coverage`, `coverageNeeded`, `GauzeOffCenter` |
| Venda suelta (opcional) | Con la gasa puesta, se toma la venda del botiquín y se da una vuelta alrededor de la mano de la tía (330°, medidos alrededor del eje de su mano). Si la vuelta va muy pegada (radio medio menor que 5,5 cm), queda apretada: ella se queja y es un error. Holgada, queda bien. Queda una banda de venda sobre la gasa. | `BandageItem`, `BandageApplied` (detalle Suelta o Apretada) |
| Lugares fijos | Cada cosa del botiquín tiene su lugar: si se suelta a menos de 12 cm de él con la tapa abierta, vuelve a acomodarse (con un clic). | `FirstAidKit.UpdateSlots` |

Los cuatro eventos nuevos van al final de `BurnEvent`. En modo guiado, el
narrador corrige cada error con texto (las voces `guia_err_apretar`,
`guia_err_centro_gasa`, `guia_err_gasa_corrida` y `guia_err_venda` faltan
grabarlas: `VOCES.md`). La tía se queja con `voz_dolor_4` (gasa apretada) y
`voz_dolor_3` (venda apretada).

**Probado en Play:** con la gasa puesta por código a 1 cm del centro, se
asentó sobre el dorso, cubrió el 100 % y quedó la capa de tejido sin
errores; la venda puesta por código dejó la banda encima. Al principio, con
la mano de la tía moviéndose entre dos pasos de la prueba, la gasa quedó a
15 cm y se registró bien el error de gasa corrida (0 %). **Falta en el
Quest:** todo con las manos y con controles, y ajustar los números al
probar (ver `PRUEBAS_QUEST.md`): `HandHalfThickness` (1,8 cm, grosor
supuesto de la mano), `pressDepth`, `centerTolerance`, `flatTolerance` y
`BandageItem.tightRadius`.

## 24. Manta de la sala en vez de la chompa (`mejoras-demo`, 2026-11-10)

La chompa se acoplaba mal a la tía de Avaturn: era una media capa rígida,
armada una vez con la postura del momento, con mangas de cápsula que
flotaban delante del pecho y un hueco en el hombro por donde se veía la
blusa. Se cambió por una **manta**: las guías (NHS, Cruz Roja) piden
mantener abrigada a la persona, no una prenda en particular. Además, una
manta en la cocina no es verosímil; está doblada en el **sofá de la sala** y
hay que ir a buscarla, dejando un momento a la tía con la mano bajo el agua.

| Qué | Cómo funciona | Dónde |
|---|---|---|
| Manta doblada | Tres capas con textura de cuadros sobre la parte delantera del asiento del sofá, hacia un extremo (sin tocar el cojín del brazo). Si cae al piso, se queda ahí (no vuelve sola al sofá). Si la escena no tiene sofá, va en la silla más cercana como antes. | `BurnSceneProps.PlaceOnSofa`, `BuildBlanket`, `BlanketItem` |
| Tela simulada | Al soltarla a menos de 45 cm del pecho o de un hombro, se convierte en una tela de 1,35 × 0,85 m (22 × 13 puntos, Verlet). Queda sujeta en la parte alta de la espalda (borde y dos filas más) y sobre los hombros; el resto cae por su peso y choca con las 14 cápsulas y la esfera de la cabeza (`PatientPhysics`). Los extremos cuelgan abiertos por delante de los brazos. | `BlanketDrape` |
| No se voltea | Si solo se sujetaba el borde de arriba, al girar la tía la manta pasaba por encima de los hombros y quedaba colgando por delante. Con las filas de la espalda sujetas, tras girarla 180° de golpe la columna central sigue de 18 a 30 cm detrás del pecho. | `backPinRows` |
| Rendimiento | Simularla cuesta unos 1,6 ms por cuadro (editor del PC). Si la tela se queda quieta 1 s, se "duerme": sigue al pecho sin simular. Despierta si un brazo, la cabeza o el pecho se mueven más de 2 cm. Estando quieta, se durmió en unos 3 s; al girarla 30°, despertó. | `sleepSpeed`, `sleepAfter`, `wakeDistance` |
| Ir a la sala | El jugador puede caminar por el camino de la cocina al sofá (7,3 m, buscado en una grilla del piso de 25 cm al dar Play), con medio metro de holgura y 1,4 m alrededor de la manta. La tía sigue limitada a la cocina. Las paredes las frena el `CharacterController`. | `PlayArea.AllowPlayerPathTo`, `ClipPlayer`, `VRLocomotion` |
| Guía | Paso "Abrígala con una manta": "Trae la manta del sofá de la sala y suéltala sobre sus hombros." El brillo marca la manta y, cuando ya la llevas, su espalda. La voz `guia_abrigo` todavía dice "chompa": mientras no se regrabe, solo se muestra el texto (`Hito1FX.TtsStandIns`). | `BurnGuide`, `docs/VOCES.md` |

Se borraron `SweaterItem` y `SweaterDrape` (ninguna escena los usaba). El
evento sigue siendo `SweaterOn` para no cambiar el enum; la tía sigue
diciendo "Gracias, me estaba dando frío".

**Probado en Play** (escena `Prueba_LuzHorneada`, sin visor), con capturas:
la manta doblada en el sofá; puesta por código, de espaldas, de costado y
de frente, en reposo y después del accidente (ofreciendo la mano); el giro
de 180°; el reposo; la cápsula del jugador movida por código desde la cocina
hasta el sofá, que llegó a 44 cm de la manta. **Falta en el Quest:**
agarrarla y soltarla sobre la tía con las manos y con controles, caminar
a la sala con el joystick, los 72 fps con la tela despierta, y si se ve bien
de cerca. Un giro instantáneo de 30° (forzado por código) dejó un brazo por
fuera de la manta; con los giros naturales de la tía no pasó.
