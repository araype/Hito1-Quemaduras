# Pendiente: revisar los manuales antes de entregarlos

Los dos manuales (`Manual_Instalacion_Despliegue.pdf` y
`Guia_Rapida_Instalacion.pdf`) se escribieron el 2026-11-14 a partir de la
configuración del proyecto, **sin probar la instalación en otra PC ni en el
visor**. Antes de entregarlos hay que revisar:

## 1. Problemas comunes (sección 9 del manual y "Si algo falla" de la guía)

Ninguna de estas soluciones se reprodujo de verdad. Probar cada una y
corregir el texto si no funciona así:

- [ ] **Escena vacía sin Git LFS:** clonar sin LFS en otra carpeta y
  comprobar que `git lfs install` + `git lfs pull` la arregla.
- [ ] **El visor no aparece en Run Device:** confirmar los pasos con el
  visor del lab (modo desarrollador, cable, permiso de depuración, Refresh).
- [ ] **Error de Android SDK o JDK:** confirmar que reinstalar Android Build
  Support desde Unity Hub lo resuelve.
- [ ] **Error con `com.coplaydev.unity-mcp`:** abrir el proyecto en una PC
  sin Git o sin internet y ver qué pasa; confirmar que quitar la línea de
  `Packages/manifest.json` no rompe nada.
- [ ] **Play sin imagen en el visor:** confirmar con Meta Horizon Link.
- [ ] **La app no aparece en la biblioteca:** confirmar el nombre del filtro
  "Fuentes desconocidas" en el idioma del visor.
- [ ] **Va lento:** medir fps en el APK vs. Play con Link.
- [ ] Agregar los problemas reales que aparezcan al instalar en otra PC.

## 2. Otros datos a confirmar

- [ ] Pasos del modo desarrollador en la app Meta Horizon (los menús pueden
  tener otro nombre).
- [ ] Tiempos de importación (15–30 min) y de compilación (10–25 min).
- [ ] Nombre de la app en el visor: hoy es `Pruebalaptop_agarrecubo`. Si se
  cambia en Player Settings, cambian también el paquete Android y la ruta de
  los registros en los dos manuales.
- [ ] Que `adb pull` copie bien la carpeta `sesiones_quemadura`.

## Cómo regenerar los PDF

Editar `fuente/manual.html` o `fuente/resumen.html` (estilo en
`fuente/estilo.css`) y generar con Edge:

```bash
msedge --headless=new --no-pdf-header-footer --print-to-pdf="Manual_Instalacion_Despliegue.pdf" "fuente/manual.html"
msedge --headless=new --no-pdf-header-footer --print-to-pdf="Guia_Rapida_Instalacion.pdf" "fuente/resumen.html"
```

Al terminar la revisión, borrar este archivo.
