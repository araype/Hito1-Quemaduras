# Pruebas pendientes en el Meta Quest

Lista de lo que se hizo **solo en el editor** (sin visor) y hay que probar
en el Quest, en el lab. Marca cada casilla al probarla. Si algo falla,
anótalo debajo del punto (qué pasó y en qué momento) y pásalo a
`PENDIENTES.md`.

## Antes de empezar

- [ ] Hacer un APK nuevo desde la rama `mejoras-demo` (el último APK,
  `13APKPRUEBA8.apk`, es del 9 nov a las 18:14 y no trae nada de esta
  lista).
- [ ] Llevar **audífonos** para las voces espaciales.
- [ ] Probar una partida **con controles** y otra **solo con las manos**.
- [ ] Probar una en **modo guiado** y otra en **modo evaluación**.
- [ ] Tener a mano un medidor de fps (OVR Metrics Tool o el panel de
  rendimiento del visor).

## 1. Rendimiento

- [ ] 72 fps estables durante toda la partida, sobre todo: con la tía de
  Avaturn cerca y a la vista, con el agua corriendo y con la manta puesta.
- [ ] Sin tirones al empezar la partida (al cambiar el modelo de la tía).

## 2. Tía de Avaturn

- [ ] La cara se ve natural de cerca: sin manchas grises ni media cara
  oscura, con la piel de tono parejo.
- [ ] El pelo tiene bordes suaves, no pixelados (depende del antialiasing
  del Quest).
- [ ] Los ojos te miran sin que se vea mucho blanco; parpadea sola.
- [ ] Cambia la cara según el momento: tranquila cocinando, dolor al
  quemarse, alivio con el agua, sonrisa al agradecer, ceño si le aprietas la
  mano.
- [ ] La boca se mueve cuando habla.
- [ ] Brazos y manos con luz pareja; la quemadura (rojo y ampollas) se ve
  bien sobre la piel.
- [ ] Al voltearse para recibir la taza, la mano derecha cuelga natural (no
  doblada contra la pierna).
- [ ] No choca con la campana ni con los muebles (mide unos 11 cm más que la
  de Mixamo).

## 3. Manta (antes chompa)

- [ ] La manta está doblada en el sofá de la sala y se llega caminando con
  el joystick desde la cocina (no deja pasar a otros cuartos).
- [ ] Se alcanza desde el frente del sofá sin agacharse demasiado.
- [ ] Al soltarla sobre la espalda o los hombros, cae como tela: cubre la
  espalda y los extremos cuelgan por delante de los brazos.
- [ ] No se voltea hacia adelante cuando la tía se gira, y los brazos no la
  atraviesan al ofrecer la mano o recibir la gasa.
- [ ] 72 fps con la manta puesta mientras la tía se mueve (la tela se
  simula) y quieta (la tela se duerme).

## 4. Anillo

- [ ] Se puede sacar **pellizcándolo y deslizándolo** hacia la punta del
  dedo: el anillo sigue a la mano y vibra al moverlo.
- [ ] Con las manos (sin controles), el pellizco y el deslizamiento se
  reconocen bien. Si cuesta, mantener el pellizco sin moverse lo saca en
  3 s.
- [ ] Hay tiempo de sobra para sacarlo siguiendo la guía (apagar la
  hornilla, abrir el agua, anillo): no debería trabarse antes de unos
  2 min 15 s sin enfriar.
- [ ] Si se deja pasar mucho tiempo y se traba: ella dice "¡No sale, ya se
  me hinchó el dedo!" y, en modo guiado, aparece la tarjeta "Su mano ya se
  hinchó: no fuerces el anillo…".
- [ ] El anillo suelto queda en el piso (no vuelve a caer en bucle).

## 5. Tarjetas y botones

- [ ] La tarjeta de resultados final se alcanza y se pueden tocar todos sus
  botones.
- [ ] En las tarjetas con varias opciones, ninguna se marca sola al bajar la
  mano hacia otra.
- [ ] Las cápsulas más gruesas del torso (16 cm) y la cadera (17 cm) de la
  tía no estorban al acercar las manos (por ejemplo, al poner la gasa).

## 6. Sonido espacial (con audífonos)

- [ ] La voz de la tía se oye desde su cabeza: al girar la cabeza, cambia de
  lado.
- [ ] Al llamar al 106, el operador se oye desde el celular (en la oreja
  donde lo llevas).
- [ ] El narrador se oye "en off" (igual desde cualquier lado).
- [ ] Los efectos (olla, agua, anillo) suenan desde donde ocurren.

## 7. Vibración (con controles)

- [ ] Toque corto al agarrar cualquier objeto.
- [ ] Vibración áspera en las dos manos al cortar una tira de film.
- [ ] Vibración suave que crece mientras sostienes la gasa sobre la
  quemadura.
- [ ] Las que ya había siguen bien: agua, cerca del fuego, errores,
  aciertos, botones, anillo y al tocar a la tía.

## 8. Botiquín y gasa

- [ ] El botiquín está junto al lavadero, con el rótulo y el tirador hacia
  la cocina.
- [ ] La tapa se abre tomando el tirador y levantándola (con controles y con
  las manos). Un toque corto en el tirador también la abre.
- [ ] Con la tapa cerrada no se puede sacar nada de adentro.
- [ ] El rótulo del sobre se lee bien ("GASA ESTÉRIL 10 × 10 cm").
- [ ] Con el sobre en una mano, pellizcar la esquina roja con la otra y
  separar las manos lo rasga: suena, vibra y la gasa queda en la mano que
  tenía el sobre.
- [ ] La gasa nueva se ve como tela (textura de tejido) y cubre la
  quemadura igual que antes (mantenerla 1 s encima).
- [ ] Poner la pomada en la quemadura cuenta como error, con su
  explicación.
- [ ] En modo guiado, el paso de cubrir marca el tirador y después los
  sobres.

## 9. Manos al agarrar (para decidir las poses de mano)

- [ ] Con controles: ¿los dedos se ven cerrados a través del objeto (la taza,
  el celular, el sobre, el rollo de film)? Anotar en cuáles se ve peor.
- [ ] Con las manos: los dedos reales ya rodean el objeto; anotar si algo se
  ve mal.

## 10. Boca al hablar

- [ ] De cerca, la boca de la tía acompaña lo que dice: se cierra en p, b y
  m, se abre más en la "a" y casi nada en los silencios.
- [ ] No queda desfasada (adelantada o atrasada) respecto a la voz.
- [ ] Al regrabar `guia_anillo` y `guia_err_anillo` no hace falta nada
  (son del narrador). Si se regraba una voz de la tía, volver a correr el
  menú Hito1 > Generar visemas de las voces.

## 11. Poner la gasa (sección 23 de QUEMADURA_INMERSION)

- [ ] Tomar la gasa del centro da el aviso "Tómala de los bordes"; de los
  bordes, no.
- [ ] Inclinada o corrida, la indicación lo dice ("Ponla plana…",
  "Céntrala…") y el círculo no se llena.
- [ ] Apoyarla sin apretar llena el círculo. Empujarla contra la mano de la
  tía dice "Sin apretar", vibra seco y, si sigues, ella se queja.
  **Ajustar** si salta muy fácil o nunca: `CoverItem.pressDepth` (1,2 cm) y
  `HandHalfThickness` (1,8 cm).
- [ ] Al completarse, la gasa se asienta sobre el dorso (no aparece de
  golpe) y queda como tela con trama.
- [ ] Puesta un poco corrida (más de 3 cm), la quemadura asoma y el
  resumen dice que quedó corrida.
- [ ] Con la gasa puesta, dar una vuelta holgada con la venda alrededor de
  su mano la fija; muy pegada, ella se queja y es error. **Ajustar**
  `BandageItem.tightRadius` (5,5 cm) si cuesta distinguir.
- [ ] Soltar un sobre o la venda cerca de su lugar en el botiquín (tapa
  abierta) lo vuelve a acomodar.

## 12. Lo que se agregue después

Las mejoras nuevas (poses de mano) se agregan aquí al terminarlas.
