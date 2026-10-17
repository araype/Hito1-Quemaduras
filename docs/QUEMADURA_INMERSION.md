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
| Ambiente | Silencio; el acero reflejaba el cielo | Sonido de cocina y reflejos reales de la cocina | `KitchenAmbience` (`BurnSequenceManager`) |

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
- **Volver a ver la esfera del marcador** (para depurar):
  `BurnSkinOverlay > Hide Marker` = falso.

## 4. Rendimiento en el Quest

- Las texturas del paciente bajan a 2048/1024 en Android.
- La sonda de reflejos se renderiza **una sola vez** al empezar.
- La tela tiene 13 × 9 partículas y 10 iteraciones a 90 Hz.
- No se agregaron luces en tiempo real.
