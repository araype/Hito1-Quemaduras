# Quemadura: aspecto, física y sonido

Rama: `quemaduras`. Cambios para que la escena de la cocina (`Escenario_Hito1`)
se vea y suene real. **No se tocó ninguna escena**: todo se instala solo al dar
Play (`Hito1RuntimeBootstrap`, solo si la escena tiene `BurnSequenceManager`).
Para ajustar un valor, agrega el componente a mano en el objeto indicado y
cambia sus campos en el Inspector; si ya está, no se duplica.

> Se verificó que todo compila contra Unity 2022.3.62 y el Meta XR SDK 201, y
> se simuló la tela fuera de Unity. **No se ha visto en el editor ni en el
> Quest todavía.**

## 1. Qué cambió

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
2. Abrir `Escenario_Hito1` y dar Play con Quest Link.
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
