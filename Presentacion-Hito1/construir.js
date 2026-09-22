const { crearDeck } = require('C:/Users/SEBASTIAN SANCHEZ/.claude/skills/presentaciones-pro/scripts/deck.js');

const d = crearDeck({
  tema: 'academico',
  titulo: 'Primeros auxilios ante una quemadura — Hito 1',
  autor: 'Sebastian Sanchez, Aracely Ayala, Jose Osnayo',
  densidad: 'hablado',
});

// 1. Portada con foto a sangre.
d.portada({
  contexto: 'CS2H01 · Human-Computer Interaction',
  titulo: 'Aprender a actuar ante una quemadura, sin miedo a equivocarse',
  subtitulo: 'Hito 1 · Prototipo de una experiencia en realidad virtual',
  autor: 'Sebastian Sanchez, Aracely Ayala y Jose Osnayo',
  fecha: 'Septiembre 2026',
  imagen: 'imagenes_descargadas/slide01_oculus-quest-headset-vr.jpg',
  imagenAlt: 'Persona con un visor de realidad virtual, en un ambiente oscuro',
});

// 2. El problema, con foto.
d.contenido({
  composicion: 'clasica',
  titulo: 'Una quemadura de cocina exige actuar rápido, y no todos aprendimos cómo',
  puntos: [
    'Pasa al tocar una olla o un recipiente caliente, y exige responder de inmediato.',
    'Armamos una experiencia en VR donde se practica, sin que nadie salga lastimado de verdad.',
  ],
  imagen: 'imagenes_descargadas/slide02_kitchen-stove-cooking-pot.jpg',
  imagenAlt: 'Ollas de metal sobre una hornilla con fuego',
  notas: 'El dato de cuánta gente no tiene formación está pendiente de una encuesta propia. Las revisiones de VR que encontramos son de RCP, no de quemaduras, así que no las citamos como resultado sobre nuestro tema. En el curso trabajamos con el Interaction SDK de Meta XR: agarre con las manos, agarre a distancia y poses de la mano; esa es la base de las interacciones.',
});

// 3. El objetivo, como separador de color.
d.seccion({
  fondo: 'sec',
  titulo: 'Que alguien sin formación practique, sin miedo a equivocarse, los pasos ante una quemadura',
  texto: 'Sin reemplazar un curso certificado.',
});

// 4. Audiencia y roles, en tarjetas.
d.tarjetas({
  titulo: 'La experiencia tiene tres roles: quien aprende, quien se quema y quien guía',
  items: [
    { titulo: 'El aprendiz', texto: 'El usuario. Vive la escena en primera persona.' },
    { titulo: 'El familiar afectado', texto: 'Se quema con la olla; no es jugable.' },
    { titulo: 'La guía', texto: 'La voz que instruye, nunca un comando.' },
  ],
  notas: 'La persona final (nombre, edad, ocupación) todavía está pendiente de completar con la encuesta; hoy la definimos como alguien joven o adulto, sin formación en primeros auxilios.',
});

// 5. Arco de la historia, como proceso.
d.proceso({
  fondo: 'acc',
  titulo: 'El usuario vive el accidente y decide cómo responder, paso a paso',
  pasos: [
    { titulo: 'Planteamiento', texto: 'Tutorial corto de agarrar y moverse.' },
    { titulo: 'Desarrollo', texto: 'El familiar se quema con la olla.' },
    { titulo: 'Clímax', texto: 'Lleva la mano bajo el agua.' },
    { titulo: 'Resolución', texto: 'Cubre la quemadura y ve el resumen.' },
  ],
});

// 6. Requisitos funcionales, resumidos en tarjetas.
d.tarjetas({
  titulo: 'El usuario aprende viendo, actúa, y sabe cómo le fue',
  items: [
    { titulo: 'Aprende viendo', texto: 'Pasos guiados por voz y manos fantasma.' },
    { titulo: 'Enfría y cubre', texto: 'Mano bajo el agua y un paño limpio.' },
    { titulo: 'Ve cuánto falta', texto: 'Indicador del tiempo bajo el agua.' },
    { titulo: 'Sabe cómo le fue', texto: 'Resumen final de aciertos y errores.' },
  ],
  notas: 'Resume los cinco requisitos funcionales del documento (RF1 a RF5); RF2 y RF3 se juntaron en una sola tarjeta porque son un mismo paso.',
});

// 7. Elección de inputs, en comparación.
d.comparacion({
  fondo: 'pri',
  titulo: 'Las manos actúan; la voz guía, no ordena',
  izq: { titulo: 'Manos (input principal)', puntos: ['Agarran la olla y el paño', 'Mano bajo el agua', 'Acciones cortas'] },
  der: { titulo: 'Voz (solo salida)', puntos: ['Instrucciones breves', 'Con subtítulos', 'Nunca es un comando'] },
  sintesis: 'La mirada guía la atención; el movimiento es corto, dentro de la cocina.',
});

// 8. Principios de diseño aplicados.
d.contenido({
  composicion: 'clasica',
  titulo: 'Cada acción responde al instante, y equivocarse no cuesta caro',
  puntos: [
    'Los objetos usables se distinguen; una mano fantasma muestra cómo agarrarlos.',
    'Cada acción responde al instante: tiempo, sonido y resumen final.',
    'Si se equivoca, puede repetir el paso sin problema.',
    'El tutorial es corto y solo muestra lo necesario.',
  ],
  notas: 'Estos son los principios de la sección 4.2 del documento: visibilidad y affordances, feedback, error humano asumido, y facilidad de aprendizaje con divulgación progresiva.',
});

// 9. Separador de transición hacia el estado real del prototipo.
d.seccion({
  fondo: 'oscuro',
  titulo: 'Esto ya se puede probar en el visor',
  texto: 'La escena de la cocina, con el agarre y el movimiento, funciona en Meta Quest.',
});

// 10. Estado actual del prototipo, con foto.
d.contenido({
  composicion: 'invertida',
  titulo: 'El jugador ya camina por la cocina y agarra la olla con el control',
  puntos: [
    'Choca contra paredes y muebles en vez de atravesarlos.',
    'Acerca la mano, aprieta el gatillo y sostiene el objeto.',
    'Falta el agua, el indicador de tiempo, el sonido y el resumen final.',
  ],
  imagen: 'imagenes_descargadas/slide10_virtual-reality-headset-gaming.jpg',
  imagenAlt: 'Hombre con un visor de realidad virtual',
  notas: 'Esto corresponde al Entregable 1 del cronograma; lo que falta ya está planificado para los siguientes entregables.',
});

// 11. Alcance del proyecto, en comparación.
d.comparacion({
  fondo: 'sec',
  titulo: 'El proyecto se enfoca solo en la quemadura, por ahora',
  izq: { titulo: 'Sí incluye', puntos: ['La quemadura, en una cocina', 'Orden correcto de los pasos', 'Práctica repetible y segura'] },
  der: { titulo: 'No incluye (todavía)', puntos: ['Atragantamiento, RCP, DEA', 'Fuerza o temperatura real', 'Un curso certificado'] },
  sintesis: 'Quedan como ampliación futura, según indicó el profesor.',
});

// 12. Cronograma, como gráfico nativo.
d.grafico({
  titulo: 'Estamos en el primer entregable, con la cocina armada',
  tipo: 'columnas',
  etiquetas: ['Entregable 1', 'Entregable 2', 'Entregable 3', 'Entregable 4', 'Entregable 5'],
  series: [{ nombre: 'Avance planificado (%)', valores: [15, 30, 50, 70, 100] }],
  destacar: 0,
  mensaje: 'El primer entregable es la escena de la cocina, el personaje y el tutorial de agarre; los siguientes suman el agua, el tiempo, el sonido y el resumen final.',
  fuente: 'Fuente: cronograma del proyecto (semanas 10 a 15)',
});

// 13. Cierre con los puntos pendientes reales.
d.cierre({
  fondo: 'pri',
  titulo: 'Antes del próximo entregable, cerramos lo que quedó pendiente',
  puntos: [
    'Definir la persona final con una encuesta propia.',
    'Citar una guía oficial, como Cruz Roja u OMS, para los pasos.',
    'Ponerle nombre definitivo al proyecto.',
  ],
  contacto: 'Sebastian Sanchez, Aracely Ayala y Jose Osnayo',
});

// 14. Referencias reales del documento.
d.referencias({
  lista: [
    "Alcázar Artero, P. M., Pardo Ríos, M., Greif, R., Ocampo Cervantes, A. B., Gijón-Nogueron, G., Barcala-Furelos, R., Aranda-García, S., & Ramos Petersen, L. (2023). Efficiency of virtual reality for cardiopulmonary resuscitation training of adult laypersons: a systematic review. Medicine, 102(4), e32736.",
    'Bowman, D. A., Kruijff, E., LaViola, J. J., & Poupyrev, I. (2005). 3D User Interfaces: Theory and Practice. Addison-Wesley.',
    'Norman, D. A. (2013). The Design of Everyday Things: Revised and Expanded Edition. Basic Books.',
    "Rodden, K., Hutchinson, H., & Fu, X. (2010). Measuring the user experience on a large scale: User-centered metrics for web applications. Proceedings of the SIGCHI Conference on Human Factors in Computing Systems (CHI '10).",
    'Sun, R., Wang, Y., Wu, Q., Wang, S., Liu, X., Wang, P., He, Y., & Zheng, H. (2024). Effectiveness of virtual and augmented reality for cardiopulmonary resuscitation training: a systematic review and meta-analysis. BMC Medical Education, 24(1).',
    'Witmer, B. G., & Singer, M. J. (1998). Measuring presence in virtual environments: A presence questionnaire. Presence: Teleoperators and Virtual Environments, 7(3), 225–240.',
  ],
});

d.guardar('Hito1-Sustentacion.pptx');
