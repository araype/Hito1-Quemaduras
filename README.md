# Simulador VR de primeros auxilios ante una quemadura doméstica

Proyecto del curso CS2H01 Interacción Humano-Computador (UTEC). En una cocina
virtual, la tía se quema la mano con una olla y el jugador tiene que aplicar
los primeros auxilios: apagar el fuego, enfriar con agua corriente 20
minutos, quitar el anillo, abrigarla, llamar al 106, cubrir la quemadura sin
apretar y decidir si debe ir al centro de salud. Tiene un modo guiado y un
modo evaluación, y al final muestra un resumen con puntaje y errores.

- **Motor:** Unity 2022.3.62f3, Meta XR SDK 201 (OpenXR)
- **Visor:** Meta Quest 2, 3 o Pro (Android, ARM64)
- **Escena principal:** `Assets/Hito1/Escenas/Escenario_Quemadura.unity`

## Instalación y despliegue

- **Manual completo, con capturas:** [docs/manuales/Manual_Instalacion_Despliegue.pdf](docs/manuales/Manual_Instalacion_Despliegue.pdf)
- **Guía rápida (2 páginas):** [docs/manuales/Guia_Rapida_Instalacion.pdf](docs/manuales/Guia_Rapida_Instalacion.pdf)

Resumen:

1. Instala Unity Hub, Unity 2022.3.62f3 con **Android Build Support**, y Git.
2. Descarga el proyecto (Git LFS es obligatorio):

   ```bash
   git lfs install
   git clone https://github.com/araype/Hito1-Quemaduras.git
   cd Hito1-Quemaduras
   git checkout mejoras-demo
   ```

3. Ábrelo desde Unity Hub y abre la escena `Escenario_Quemadura`.
4. Activa el modo desarrollador del visor y conéctalo por USB.
5. En **File → Build Settings** (Android), elige el visor en *Run Device* y
   pulsa **Build And Run**.
6. En el visor: **Biblioteca → Fuentes desconocidas → Pruebalaptop_agarrecubo**.

## Documentación

| Archivo | Contenido |
|---|---|
| `docs/manuales/Manual_Instalacion_Despliegue.pdf` | Instalación, compilación, uso y registros, con capturas |
| `docs/manuales/Guia_Rapida_Instalacion.pdf` | Lo mismo, resumido en 2 páginas |
| `docs/manuales/fuente/` | Fuente HTML de los dos manuales (se generan con Edge) |
| `docs/QUEMADURA_INMERSION.md` | Qué hace cada parte y cómo probarla |
| `docs/PENDIENTES.md` | Lo que falta, en orden |
| `docs/PRUEBAS_QUEST.md` | Lista de pruebas en el visor |
| `docs/VOCES.md` | Guion de voces |
| `Informe/main.pdf` | Informe del proyecto |
