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
| Chompa | Doblada en la silla más cercana. Se suelta sobre la espalda o los hombros y queda puesta. | `SweaterItem` |
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
5. **Mientras tanto:** sacar el anillo y abrigarla con la chompa.
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
| Cubrir | Film en tiras y bolsa plástica (`corregir-bugs`, sección 11). Sin gasa: una gasa seca se pega a la herida y el NHS pide film o bolsa. | La gasa como opción principal y la lámina de film estirada entre las manos de `experiencia-inmersiva` (`CoverItem` de Araype). |
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
| Al ofrecer la mano para sacar el anillo, la tía seguía al jugador | La mano ofrecida (a 33 cm del pecho) quedaba más lejos que el umbral para dar un paso (`stepAtFraction`, bajado a 0,72 en la sección 15): daba un paso, la mano se adelantaba con ella y volvía a dar otro, aunque el jugador retrocediera (unos 3,5 cm/s). Ahora la mano ofrecida queda dentro de lo que alcanza sin caminar. Probado en Play: con el jugador a 1 m, antes avanzaba 20 cm en 6 s; ahora 0 cm, y sigue ofreciendo la mano a la vista. | `PatientBurnArm.ShowGoal`, `WithinStep` |
| El jugador quedaba subido al mesón (al ponerle la chompa a la tía) | La cápsula del jugador chocaba con las 14 cápsulas del cuerpo de la tía (piernas hasta 0,65 m, muslos hasta 1,1 m) y, como son redondeadas, se subía por ellas y de ahí al mesón. Ahora no chocan (la cabeza sigue sin poder entrar en su cuerpo, `KeepHeadOutOfPatient`). Red de seguridad: si queda parado a más de 30 cm sobre el piso, vuelve al último lugar firme en el piso. Probado en Play: 0 de 14 cápsulas chocan; puesto sobre el mesón, vuelve al piso en menos de 0,5 s. | `VRLocomotion.IgnorePatientBody`, `maxStandAboveFloor`, `GuardAgainstFalling` |
| Los objetos agarrados flotaban a varios centímetros de la mano | El punto de agarre salía de `OVRSkeleton` (o del ancla de la mano si no estaba listo) o de un desfase fijo del control, y no coincidía con la mano dibujada. Ahora se agarra en la pinza (pulgar e índice) de la mano visible (`HandVisual` del SDK de Meta), con manos o con controles; si no se está dibujando, se usa el cálculo de antes. También la punta del índice para los botones sale de ahí. **Solo compilado**: sin visor no hay mano visible en el editor. | `SimpleGrab.TryVisiblePinch` |
| Una tarjeta "desaparecía" al acercarse a leerla | Con la mano a menos de 25 cm, la tarjeta vuelve a usar la prueba de profundidad para que el dedo se vea delante (sección 15); si estaba en parte dentro del mesón, el mesón la tapaba entera. Ahora solo usa la profundidad si nada de la escena la tapa desde los ojos (rayos al centro y a las esquinas). Probado en Play: al aire, no tapada; dentro del mesón, tapada (sigue dibujándose encima). | `OverlayPanel.BlockedFromEye` |
| Objetos que saltaban en el mesón, el celular que "desaparecía" al ir a agarrarlo y objetos del piso que frenaban al jugador | Los objetos sueltos (taza, celular, chompa, film, bolsa, remedios...) ya no chocan con el cuerpo del jugador, con las esferas de sus manos ni con el cuerpo de la tía: las manos los empujaban (el celular, delgado, salía disparado al acercar la mano) y la cápsula del jugador se trababa con ellos. Se siguen agarrando igual (la búsqueda de agarre no usa colisiones). La mano de la tía no entra en esto. | `SimpleGrab.loosePropsIgnorePlayer` |
| La llamada seguía bajito si se soltaba el celular | Se pone en espera: el operador se pausa, el celular brilla y suena cada 2,5 s, y un aviso "Recoge el celular y llévalo a tu oreja para seguir la llamada" sigue la vista hasta que se vuelve a llevar el celular a la oreja. | `EmergencyCall` (`EnterHold`, `FollowHoldPanel`) |
