# Movilidad mejorada (rama `movilidad-mejorada`)

Guía para quien continúe esta rama, persona o sesión de Claude: qué cambió, en
qué estado está y cómo combinarla con `main` o con otra rama.

## Estado

- **Base:** `16cde33` de `guia-y-paciente-fisica` (2026-10-31). Esa base aún no
  está en `main`.
- **Verificado:** los 71 scripts compilan sin errores con Roslyn fuera del
  editor.
- **Sin verificar:** no se ha abierto en Unity ni se ha probado en el Quest.
  Los valores son un punto de partida y se pueden tener que ajustar.

## Por qué

Moverse en el visor se sentía tosco. Al leer `VRLocomotion` se vio que la
velocidad cambiaba de golpe, que el joystick arrancaba con un salto, que
1,8 m/s es rápido para la cocina a escala real y que el giro saltaba 45°.

## Qué cambió

Todo el código está en `Assets/Hito1/Scripts/VRLocomotion.cs`:

| Qué | Antes | Ahora | Campo |
|---|---|---|---|
| Velocidad al caminar | 1,8 m/s | 1,3 m/s | `walkSpeed` (reemplaza a `moveSpeed`) |
| Arranque y frenado | De golpe | Rampa de 0,12 s | `moveRampSeconds` (nuevo) |
| Zona muerta del joystick | Al salir ya iba al 15 % | Empieza desde 0 | `ApplyDeadZone` |
| Giro a saltos | 45° | 30° | `snapAngle` (reemplaza a `snapTurnAngle`) |

Al girar a saltos también se gira la velocidad guardada por la rampa, para no
derrapar hacia la dirección anterior.

### Por qué los campos tienen nombre nuevo

`Escenario_Quemadura.unity` guarda `moveSpeed: 1.8` y `snapTurnAngle: 45`. Si
solo se cambiara el valor en el script, la escena lo pisaría. Con otro nombre,
Unity usa el valor del script y la escena no se toca, lo que además evita
conflictos al unir ramas.

**No vuelvas a los nombres anteriores** ni restaures esos campos: el juego
volvería a 1,8 m/s y 45° sin ningún error.

### Cómo ajustar los valores

Cámbialos en el script. Si los cambias en el Inspector y guardas la escena, la
escena queda modificada y puede dar conflictos al unir. Para probar valores,
cámbialos en el Inspector durante Play, sin guardar.

## Qué probar en el visor

1. Arrancar y frenar con el joystick: debe sentirse suave, sin deslizarse al
   soltarlo.
2. Empujar el joystick apenas: debe avanzar muy lento, sin salto.
3. Moverse cerca del mesón y de la paciente: debe ser fácil ubicarse.
4. Girar a saltos, también mientras se camina: no debe derrapar.
5. Si se usan gestos de mano, avanzar con el pellizco: también pasa por la rampa.
6. Marearse es señal de que algo está mal: si pasa, bajar `moveRampSeconds` y
   anotarlo.

## Qué no se hizo todavía

Quedó fuera a propósito, a la espera de la prueba en el visor:

- Fundido breve al girar a saltos.
- Giro continuo opcional y caminar según la dirección del control.
- `Fixed Timestep` de 0,02 a 1/72, si los objetos agarrados tiemblan o se
  quedan atrás al caminar. Afecta a todo el proyecto y suma un 44 % de pasos de
  física: hay que medirlo en el Quest.
- Revisar los choques de la cápsula del jugador (`capsuleRadius` 0,2) en la
  cocina y alrededor de la paciente.
- Optimizar el rendimiento, si la imagen va entrecortada incluso con el jugador
  quieto.

## Cómo combinar esta rama

### Revisar antes de unir

```powershell
git fetch origin
git log --oneline 16cde33..origin/movilidad-mejorada
git diff --stat 16cde33 origin/movilidad-mejorada
```

Estos comandos muestran solo lo que aporta esta rama: `VRLocomotion.cs`, esta
guía y una línea en `CLAUDE.md`.

### A la rama en la que estés trabajando

```powershell
git checkout <tu-rama>
git pull
git merge origin/movilidad-mejorada
```

Si tu rama no contiene `16cde33`, el merge también trae los commits de
`guia-y-paciente-fisica` que te falten. Si quieres **solo la movilidad**, usa la
opción de `cherry-pick` de abajo.

### A `main`

Lo mejor es un pull request de `movilidad-mejorada` hacia `main`, después de
probarla en el visor. **Ese PR también lleva a `main` los commits de
`guia-y-paciente-fisica` que `main` aún no tenga**, como `a2c3911` y `16cde33`.
Comprueba que el equipo esté de acuerdo o une primero esa rama.

Si `guia-y-paciente-fisica` siguió avanzando, actualiza antes esta rama:

```powershell
git checkout movilidad-mejorada
git pull
git merge origin/guia-y-paciente-fisica
```

### Solo la movilidad, sin lo demás

```powershell
git checkout <rama-destino>
git cherry-pick <commit>
```

`<commit>` es el que muestra el `git log` de la sección "Revisar antes de unir".
Si hay más de uno, aplícalos del más antiguo al más nuevo.

### Si hay conflictos

- **`VRLocomotion.cs`:** conserva `walkSpeed`, `moveRampSeconds`, `snapAngle`,
  `horizontalVelocity` y el `ApplyDeadZone` nuevo, y agrega lo que aporte la
  otra rama.
- **`CLAUDE.md` y documentos:** conserva las líneas de las dos ramas.
- **Otros archivos:** esta rama no los cambia. Si aparece un conflicto en otro
  archivo, viene de otros commits, no de la movilidad.

### Después de unir

```powershell
git grep -n -E "moveSpeed|snapTurnAngle" -- Assets/Hito1/Scripts
```

Solo debe aparecer el comentario de `VRLocomotion.cs` que explica el cambio de
nombre. Luego compila, prueba con la lista de arriba e indica si se probó en el
editor, en el Quest o si solo se compiló.

Cuando la prueba sea correcta, agrega el resultado a `docs/PENDIENTES.md`. Ese
archivo no se tocó aquí para no chocar con los cambios que se están haciendo en
paralelo.
