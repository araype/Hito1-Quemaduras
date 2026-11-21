# RNF-01: contenido para la diapositiva

Fuente: `com.DefaultCompany.Pruebalaptop_agarrecubo#UnityPlayerActivity-20261121_095012.csv`
(OVR Metrics Tool, APK en el Quest sin Quest Link, 1 partida de 265 s, 1 muestra por segundo).
Gráfico: `docs/evidencia/rnf01_fps_quest.png` (el mismo está en `Informe/images/`).

## Título

El juego corre estable a 48 fps en el Quest: falta llegar a 72

## Puntos

- Medido en el visor con OVR Metrics Tool, sin Quest Link: 1 partida, 265 s.
- Promedio de 48 fps, entre 34 y 59 durante el juego; nunca llega a 72.
- El límite es la GPU: tarda 18,3 ms por cuadro y para 72 fps tiene 13,9 ms.
- Siguiente paso: renderizado foveado, SpaceWarp y aligerar el agua, el vapor y la tela.

## Tabla de requisitos no funcionales

| RNF | Criterio | Cómo se midió | Resultado | Evidencia |
|---|---|---|---|---|
| RNF-01 | ≥ 72 fps | OVR Metrics Tool, 1 partida en el Quest | 48 fps promedio: no cumple | CSV y gráfico |
| RNF-02 | < 100 ms | Video a 240 fps, 10 agarres | Pendiente | Video |
| RNF-03 | Aviso, pausa y reinicio | Lista de verificación | ✅ | Capturas |
| RNF-04 | Una sola persona | Prueba con usuarios (n = 6) | ✅ | HEART |

## Notas del orador

Medimos el APK directamente en el visor, porque en el simulador o con Quest Link
dibuja la PC y el número no dice nada del Quest. La experiencia no se traba: se
mantiene pareja alrededor de 48 fps. Pero no llega a los 72 que pedimos para
evitar el mareo. La CPU va al 43 % y la GPU al 97 %, así que sabemos dónde está
el problema: lo que se dibuja (agua, vapor, tela y el modelo de la tía). El
renderizado foveado y el SpaceWarp estaban apagados, y son lo primero a probar.
