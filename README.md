# Simulación VR de primeros auxilios ante una quemadura doméstica

Experiencia de realidad virtual para **Meta Quest** en la que el jugador atiende a su tía después de que se vuelca una olla de agua hirviendo y se quema la mano en la cocina. El jugador debe seguir el protocolo correcto en este orden:

1. Llevarla al caño y enfriar la quemadura con agua fría durante 20 minutos.
2. Quitarle el anillo antes de que la mano se hinche.
3. Cubrir la quemadura con film o con una bolsa limpia.
4. Llamar al 106 (SAMU).

También debe evitar los remedios caseros (pasta dental, mantequilla, hielo).

Proyecto final del curso **CS2H01 Interacción Humano-Computador**, UTEC (2026-2).

| | |
|---|---|
| Motor | Unity **2022.3.62f3** (LTS) |
| VR | Meta XR SDK 201 sobre OpenXR |
| Visor | Meta Quest 2, 3 o Pro (con controles o con las manos) |
| Escena principal | `Assets/Hito1/Escenas/Escenario_Quemadura.unity` |
| Nombre en el visor | `Pruebalaptop_agarrecubo` |

**Entregables**

- 🎥 [Video de demostración (versión final)](https://drive.google.com/drive/folders/1LthRBOaIAnPZPQVPgEad9M71Vj7n-8rp?usp=sharing)
- 🧪 [Videos y evidencias de las pruebas con usuarios](https://drive.google.com/drive/folders/1MNOp0vc79394Pmh6ESpHtdbq6dn6XHvg?usp=sharing)
- 📄 [Informe final (PDF)](docs/informe/main.pdf)
- 📘 [Manual de instalación y despliegue](docs/manuales/Manual_Instalacion_Despliegue.pdf) · [Guía rápida](docs/manuales/Guia_Rapida_Instalacion.pdf)

---

## Contenido

1. [Requisitos](#1-requisitos)
2. [Descargar el proyecto](#2-descargar-el-proyecto)
3. [Abrir el proyecto en Unity](#3-abrir-el-proyecto-en-unity)
4. [Probar en la PC con el visor (opcional)](#4-probar-en-la-pc-con-el-visor-opcional)
5. [Preparar el visor Meta Quest](#5-preparar-el-visor-meta-quest)
6. [Compilar e instalar en el visor](#6-compilar-e-instalar-en-el-visor)
7. [Cómo se juega](#7-cómo-se-juega)
8. [Registros de sesión](#8-registros-de-sesión)
9. [Problemas comunes](#9-problemas-comunes)
10. [Estructura del repositorio](#10-estructura-del-repositorio)
11. [Créditos y licencias](#11-créditos-y-licencias)

---

## 1. Requisitos

| Elemento | Detalle |
|---|---|
| PC | Windows 10 u 11, 16 GB de RAM y unos 15 GB libres. Unity genera unos 10 GB al importar el proyecto. |
| Unity Hub | Se descarga de [unity.com/download](https://unity.com/download). |
| Unity 2022.3.62f3 | Se instala desde Unity Hub con el módulo **Android Build Support**, que incluye OpenJDK y el Android SDK y NDK. |
| Git y Git LFS | [git-scm.com](https://git-scm.com/) (Git LFS viene incluido). **Es obligatorio:** los modelos, las texturas y los audios se guardan con LFS. |
| Visor | Meta Quest 2, 3 o Pro, con un cable USB-C de datos. |
| Cuenta de Meta | Con una organización de desarrollador (es gratis), para activar el modo desarrollador. |
| Opcional | [Meta Horizon Link](https://www.meta.com/help/quest/articles/headsets-and-accessories/oculus-link/), para probar desde Unity con el visor puesto. [Meta Quest Developer Hub](https://developers.meta.com/horizon/downloads/package/oculus-developer-hub-win/), para instalar el APK y ver los archivos del visor. |

## 2. Descargar el proyecto

> [!IMPORTANT]
> Instala Git LFS **antes** de clonar. Sin LFS, los archivos `.fbx`, `.glb`, `.png` y `.wav` se descargan como textos de pocas líneas y la escena aparece vacía, sin la tía ni sonidos.

```bash
git lfs install
git clone https://github.com/araype/Hito1-Quemaduras.git
cd Hito1-Quemaduras
git lfs pull
```

La versión final está en la rama `main`.

## 3. Abrir el proyecto en Unity

1. En Unity Hub, ve a **Installs → Install Editor** e instala la versión **2022.3.62f3** con **Android Build Support** y sus dos subopciones (OpenJDK y Android SDK & NDK Tools).
2. Ve a **Projects → Add → Add project from disk** y elige la carpeta `Hito1-Quemaduras`.
3. Abre el proyecto. La primera vez, Unity importa todo y puede tardar **de 15 a 30 minutos**.
4. En la ventana *Project*, haz doble clic en la escena **`Assets/Hito1/Escenas/Escenario_Quemadura.unity`**.

> [!NOTE]
> Es normal no ver todos los objetos en el editor. El film, la bolsa, los remedios, el celular y el reloj los crea el código al dar Play.

## 4. Probar en la PC con el visor (opcional)

Sirve para revisar cambios sin compilar.

1. Abre **Meta Horizon Link** en la PC y conecta el visor por cable o por Air Link. Luego activa Link dentro del visor.
2. En Unity, pulsa **Play**. La experiencia se ve en el visor y en la ventana *Game*.
3. Para detenerla, pulsa **Play** otra vez.

Sin visor puedes usar el **Meta XR Simulator**: menú *Meta → Meta XR Simulator → Activate* y luego Play.

> Con Link el rendimiento es menor que en el APK. Para medir fps o hacer pruebas con usuarios, usa la versión compilada.

## 5. Preparar el visor Meta Quest

Este paso se hace una sola vez por visor.

1. Crea una organización de desarrollador en [developers.meta.com](https://developers.meta.com/horizon/) con tu cuenta de Meta.
2. En la app **Meta Horizon** del celular, entra a *Dispositivos → tu visor → Ajustes del visor* y activa **Modo desarrollador**.
3. Reinicia el visor.
4. Conecta el visor a la PC por USB-C. Dentro del visor aparece **¿Permitir depuración USB?**: marca *Permitir siempre desde esta computadora* y acepta.

## 6. Compilar e instalar en el visor

El proyecto ya viene configurado. Antes de compilar, comprueba que esté así:

| Dónde | Valor esperado |
|---|---|
| *File → Build Settings → Platform* | **Android**, con el logo de Unity al lado. Si no lo tiene, selecciónalo y pulsa *Switch Platform*. |
| *Scenes In Build* | Solo `Escenario_Quemadura` marcada |
| *Project Settings → XR Plug-in Management*, pestaña Android | **OpenXR** con el *Meta XR feature group* |
| *Project Settings → Player → Other Settings* | IL2CPP, ARM64, Vulkan, espacio de color Linear |

### Opción A: compilar e instalar de una vez (recomendada)

1. Con el visor conectado por cable, abre *File → Build Settings*.
2. En **Run Device**, pulsa *Refresh* y elige tu visor.
3. Pulsa **Build And Run** y elige una carpeta para el APK, por ejemplo `Build/`.
4. La primera compilación tarda **de 10 a 25 minutos**. Al terminar, la app se abre sola en el visor.

### Opción B: generar el APK e instalarlo después

1. En *Build Settings*, pulsa **Build** y guarda el APK.
2. Arrastra el APK a Meta Quest Developer Hub (*Device Manager → Apps*) o instálalo desde la terminal:

```bash
adb install -r Build/QuemaduraVR.apk
```

`adb` viene con Unity, en `C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\`.

### Abrir la app en el visor

Ve a **Biblioteca de apps**, cambia el filtro a **Fuentes desconocidas** y abre **`Pruebalaptop_agarrecubo`**.

## 7. Cómo se juega

Al iniciar aparece un aviso de contenido (hay una quemadura y una persona con dolor) y luego dos modos:

- **Modo guiado:** una narradora, tarjetas con flechas y brillos indican cada paso. Si te equivocas, la respuesta correcta se marca en verde con una explicación.
- **Modo evaluación:** no hay ayudas. Lo haces solo y se mide.

Primero hay un **tutorial** breve: la tía te pide una taza y aprendes a moverte, agarrar objetos y tocar botones. Después se vuelca la olla y empieza la emergencia. El reloj de pared cuenta los 20 minutos de enfriamiento en 90 segundos reales. Al final aparece un **resumen** con el puntaje, los pasos hechos, los errores y el tiempo.

### Controles

| Acción | Con controles | Con las manos |
|---|---|---|
| Moverse | Joystick izquierdo, o caminar en el espacio real | Caminar en el espacio real, o teletransportarse: mano en forma de "L" (índice y pulgar estirados) para apuntar al piso y cerrar el pulgar contra el índice para saltar |
| Girar | Joystick derecho (giros de 45°) | Girar el cuerpo |
| Agarrar y soltar | Botón lateral (*grip*) o gatillo | Pinza de pulgar e índice |
| Botones, llave del caño y celular | Tocar con la punta del control | Tocar con la punta del dedo |
| Pausa | Girar la palma izquierda hacia la cara y tocar «Pausa» en la muñeca con la punta del otro control. También: botón de menú izquierdo, o mantener B o Y un segundo | Girar la palma izquierda hacia la cara y tocar «Pausa» en la muñeca con un dedo de la otra mano, o el gesto de menú de la mano |

En el editor, `Esc` o `P` también pausan.

## 8. Registros de sesión

Cada partida se guarda en el visor como un archivo **JSON** con el modo, el puntaje, los errores, los tiempos de cada paso y la decisión médica. Para copiarlos a la PC con el visor conectado:

```bash
adb pull /sdcard/Android/data/com.DefaultCompany.Pruebalaptop_agarrecubo/files/sesiones_quemadura/ ./sesiones
```

También se ven en Meta Quest Developer Hub, en *Device Manager → File Manager*. Las partidas jugadas en el editor se guardan en `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Pruebalaptop_agarrecubo\sesiones_quemadura\`.

## 9. Problemas comunes

| Problema | Solución |
|---|---|
| La escena está vacía o no aparece la tía | Faltan los archivos de Git LFS. Ejecuta `git lfs install` y luego `git lfs pull` dentro de la carpeta. |
| Error al resolver `com.coplaydev.unity-mcp` | Es una herramienta de desarrollo que se descarga con Git y la app no la necesita. Instala Git o quita esa línea de `Packages/manifest.json`. |
| Error de Android SDK o JDK al compilar | Reinstala el módulo Android Build Support, con sus subopciones, desde Unity Hub. |
| El visor no aparece en *Run Device* | Revisa que el modo desarrollador esté activo y que el cable sea de datos. Acepta la depuración USB dentro del visor y pulsa *Refresh*. |
| Al dar Play no se ve nada en el visor | Abre Meta Horizon Link y activa Link dentro del visor **antes** de dar Play. |
| La app no aparece en la biblioteca | Cambia el filtro a **Fuentes desconocidas**. |
| La app no responde o no arranca | Cierra las demás apps abiertas en el visor y vuelve a abrirla. |
| Va lento o con tirones | Usa el APK en lugar de Play con Link. |

## 10. Estructura del repositorio

```
Assets/
  Hito1/
    Escenas/        Escenario_Quemadura.unity (escena del build)
    Scripts/        Lógica de la experiencia (ver abajo)
    Resources/      Audios, voces, materiales, la tía de Avaturn y animaciones
    Personajes/     La tía (Avaturn y Mixamo)
    Kits/           Cocina (Apartment Kit) y modelos para cubrir (film, bolsa)
    Editor/         Herramientas del editor (menú Hito1)
  Oculus/, XR/            Configuración de Meta XR y OpenXR
docs/             Todos los entregables escritos
  informe/        Informe final (main.pdf, main.tex e images/)
  evidencias/     Pruebas con usuarios (PDF), gráfico de fps y datos de OVR Metrics Tool
                  (rnf01_ovr_metrics.csv). El informe toma de aquí sus dos figuras
  manuales/       Manual de instalación y guía rápida (PDF, y sus fuentes HTML en fuente/)
  presentacion/   Pitch final (PPTX) y su guion
  VOCES.md        Textos de todas las voces (los usa Editor/VisemeTracks.cs)
README.md         Este documento
CLAUDE.md         Notas para trabajar el código con Claude Code
```

Piezas principales del código (`Assets/Hito1/Scripts`):

- **`BurnSequenceManager`**: estados de la emergencia (*Idle → Burned → Cooling → Cooled → Covered → Success*) y el canal de eventos al que avisan todas las interacciones.
- **`BurnProtocol`**: pasos, errores, puntaje, resumen y guardado de la sesión en JSON.
- **`BurnGuide`**: tutorial, narradora y tarjetas del modo guiado.
- **`BurnPatientReactions`**, **`PatientFace`** y **`PatientBody`**: reacciones, cara y brazos de la tía.
- **`Hito1RuntimeBootstrap`**: al dar Play, instala en la escena todo lo que se crea por código.

## 11. Créditos y licencias

| Recurso | Origen |
|---|---|
| Cocina | [Apartment Kit](https://assetstore.unity.com/packages/3d/environments/apartment-kit-124055), Brick Project Studio (Unity Asset Store) |
| La tía (modelo) | Creada con [Avaturn](https://avaturn.me) |
| Animaciones | [Mixamo](https://www.mixamo.com) (Adobe) |
| Caja de film | [“Aluminum Foil Box - Low Poly”](https://sketchfab.com/3d-models/aluminum-foil-box-low-poly-697c4ce533834df59b54d3ac861b4380), Ian Valeta (CC BY 4.0) |
| Bolsa plástica | [“Ziplock bag”](https://sketchfab.com/3d-models/ziplock-bag-6428bacd2c3c46bb8987db060e4fea45), Phoenix_Connor (CC BY 4.0) |
| Voces | Generadas con [ElevenLabs](https://elevenlabs.io) |
| VR | Meta XR SDK 201 (licencia del SDK de Meta) |
| Quemadura, agua, objetos y efectos de sonido | Hechos por el equipo |

**Equipo:** Aracely Ayala, Sebastián Sánchez y Jose Osnayo. **Profesor:** Teófilo Chambilla Aquino.

> El contenido sigue las recomendaciones de primeros auxilios para quemaduras térmicas. Es material educativo y no reemplaza la atención médica ni una capacitación certificada.
