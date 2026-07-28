# MEGA PLAN — Fidelidad 1:1 de los modelos guardados en disco

Objetivo del usuario: **guardar TODO (shader + material + textura + geometría), que cargue INSTANTÁNEO, que lo
colocado cargue primero, y que en el menú aparezcan TODAS las detectadas.** Sin tocar el motor de carga viejo
más de lo necesario.

## Diagnóstico REAL (medido, no supuesto) — `[Verify]` 2026-07-26

```
COLOCADOS=422 | geometria .scsm=378/422 | materiales .scmat=69/69 | texturas .scstex=195/195
```

- Materiales y texturas: **100% guardados**. El problema NO es guardar.
- Geometría: faltaban 44 (bakes que fallaron y nunca se reintentaban).
- Síntoma del usuario: *"las texturas si no las reloadeo con las vivas no funcionan bien"* → la copia de disco se
  ve peor que la viva, y solo mejora al forzar "Actualizar texturas" (que re-spawnea desde la instancia VIVA).

**Causa raíz encontrada (bug introducido en el port):** `SceneModelLibrary.LastSpawnOwned` quedó como stub fijo en
`false` → `PlacedSceneModel.BuiltFromDisk` siempre false → el swap automático **disco → material VIVO** del
manager **nunca corría**. Por eso solo se veía bien forzándolo a mano.

## Por qué la copia de disco NUNCA será idéntica por sí sola

La reconstrucción es una *aproximación* por 3 razones estructurales de SR2/HDRP:
1. Las texturas del juego **no son legibles** → se capturan **fotografiándolas** con una cámara (ImageConversion
   está roto en este juego). Una foto es buena para el albedo, pero **falsea** máscaras/normales y arrastra error
   de exposición/espacio de color.
2. Los shaders de terreno usan **propiedades GLOBALES** que setea la zona (no viven en el material) → fuera de su
   zona el mismo material se ve distinto.
3. Si el shader real no está cargado al reconstruir, cae a **Unlit** (plano).

**Conclusión estratégica:** el disco es la RED DE SEGURIDAD (para que nada desaparezca y todo sea colocable en
cualquier lado); **la instancia VIVA es la fuente de verdad visual**. El sistema debe usar disco para aparecer
instantáneo y **cambiar solo** a vivo apenas la zona esté cargada.

---

## FASES

### FASE 1 — El material VIVO siempre gana (✅ HECHO, `VIEJO+TODO-6`)
- `LastSpawnOwned` devuelve el valor REAL (true si salió de disco).
- El manager marca `BuiltFromDisk` y hace el swap disco→vivo solo.
- **Resultado esperado:** ya no hay que apretar "Actualizar texturas".

### FASE 2 — Cero pérdidas de geometría (✅ HECHO, `VIEJO+TODO-6`)
- Los bakes vacíos ya no quedan marcados para siempre: **4 reintentos** espaciados (`_bakeFails`).
- El auto-guardado recorre todo el catálogo y encola lo que falte (40 cada 0.25 s).

### FASE 3 — El shader real SIEMPRE (pendiente)
Síntoma: al reconstruir temprano, `FindShaderByName` falla → Unlit plano; `UpgradeTick` lo re-arma después, pero
puede tardar o no correr.
- Implementar `PreloadShadersFor(keys)` de verdad: leer los nombres de shader de los `.scmat` de lo COLOCADO y
  cachearlos vía `ScanLoadedShaders` **antes** de spawnear.
- Loguear cuántos quedaron pendientes; forzar `UpgradeTick` hasta que no quede ninguno.

### FASE 4 — Texturas más fieles (pendiente, riesgo medio)
- Intentar `Graphics.Blit(src, rt)` con `RenderTextureReadWrite.Linear` **antes** de la foto por cámara; validar
  el resultado (varianza > 0, no negro) y caer a la foto si falla.
- Guardar por textura un flag `linear` (normales/máscaras) y crear el `Texture2D` con `linear:true` al cargar.
- Bump de versión del `.scstex` (STX2) conservando lectura de STX1.

### FASE 5 — Propiedades globales por zona — ❌ **DESCARTADA (investigado, NO es viable)**
Investigación (docs de Unity + discusiones oficiales sobre SRP Batcher): **una propiedad declarada como GLOBAL
queda FUERA del CBUFFER `UnityPerMaterial`**. El shader la lee del estado global, no del material → escribirla en
el material (o en un MaterialPropertyBlock) **no tiene ningún efecto**. El único lever real es
`Shader.SetGlobalX(...)`, que es **global a TODO el juego** → cambiaría el aspecto del mundo entero. Inaceptable.

**Además, el comportamiento actual probablemente ya es el correcto:** un prop colocado en otra zona toma los
globales de la zona ACTUAL → se integra con la luz/niebla del lugar donde lo pusiste, que es lo que uno quiere.
**Decisión: no se implementa.** Si alguna vez molesta, la vía sería hornear una variante del shader, no tocar
globales.

Fuentes: [Shader.SetGlobalFloat](https://docs.unity3d.com/ScriptReference/Shader.SetGlobalFloat.html) ·
[Global shader properties with SRP batcher](https://discussions.unity.com/t/global-shader-properties-with-srp-batcher/866061) ·
[Per Material Properties with SRP batcher](https://discussions.unity.com/t/per-material-properties-with-srp-batcher/866661)

### FASE 6 — Carga instantánea (parcial)
- Índice por manifiesto: ✅ instantáneo.
- `PreloadTextureFor(keys)`: descomprimir los `.scstex` de lo COLOCADO en 2do plano ANTES de spawnear.
- Front-load (10 ms/frame los primeros 8 s): ✅ ya activo.
- Prioridad: lo colocado primero, después el auto-guardado del resto: ✅ ya activo (`PendingSpawns`).

### FASE 7 — El menú muestra TODO lo guardado (verificar)
- `SeedFromDisk` siembra el catálogo desde el manifiesto → los modelos guardados aparecen aunque su zona no esté
  cargada. Verificar que el contador del menú == `BakedCount`.
- Tarjetas no disponibles: atenuadas + aviso (✅ hecho).

---

## Reglas duras aprendidas (no repetir errores)

1. **NUNCA `MaterialPropertyBlock`** para props de material en SR2/HDRP: el SRP Batcher las ignora. Usar
   `renderer.materials` (instancia) y escribir directo.
2. **NUNCA devolver un material null** → Unity lo pinta MAGENTA. Siempre un shader de respaldo válido.
3. **`Shader.Find` falla seguido** en este juego → usar `FindShaderByName` (que escanea los shaders cargados).
4. **No tocar el formato de carga viejo** (`.scsm` v4): el usuario lo pidió explícitamente.
5. **Medir antes de parchear**: los diagnósticos `[Verify]`, `[CMP]`, `[Garden]` resolvieron en 1 iteración lo que
   10 parches a ciegas no.
