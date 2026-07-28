# OMEGA PLAN — cerrar TODOS los problemas del mod

Estado al 2026-07-27 (`VIEJO+TODO-15`). Método que funcionó siempre: **medir primero con un diagnóstico, después
arreglar**. Los parches a ciegas costaron 10+ iteraciones; los diagnósticos cerraron cada tema en 1.

---

## A. MODELOS — casi cerrado ✅

| Problema | Causa REAL (medida) | Estado |
|---|---|---|
| Se veían peor que el vivo | `MaxTexSize=512` → texturas a MITAD de resolución (`[MatCmp]` lo probó: shader y keywords IGUALES, solo el tamaño difería) | ✅ 1024 |
| Pierden el look al cambiar de zona | Muchos shaders leen la textura **GLOBAL que setea la zona**, no la del material | ✅ se captura la global y se guarda como propia |
| Magenta | `NewOwnedMaterial` devolvía **null** (Unity pinta magenta) porque `Shader.Find` falla en SR2 | ✅ fallback robusto |
| Se rompen al descargar la zona | Clonaban mallas/materiales del juego, que SR2 destruye | ✅ rescate → reconstruye de disco |
| Faltaban ~44 en disco | Tope de 250k vértices descartaba los props GRANDES | ✅ 1.5M + siempre la 1ª malla |
| Solo funcionaba forzando "Actualizar texturas" | `LastSpawnOwned` quedó como stub en `false` → el swap disco→vivo **nunca corría** | ✅ valor real |

### Pendiente A1 — "Actualizar texturas" refresca TODO
Hoy `RefreshTexturesLoaded` re-captura **todos los modelos con Sample vivo** (varias zonas cargadas a la vez) y
`ApplyTextureRefresh` re-spawnea 1500+. **Debe limitarse a la zona donde está el jugador.**
- Filtrar por `ZoneGroupId(zona del jugador)`; usar `SceneBuilderTool.GetActiveZone()`.
- Idem para las miniaturas: invalidar solo las de esa zona.

### Pendiente A2 — verificar tras re-hornear
Con 1024 + globales, correr una zona y confirmar por `[MatCmp]`: `tamanoDistinto=0` y `faltantes=0`.

---

## B. JARDINES — el bloqueo real

**Hecho:** volcado REAL del assembly (`ApiCheck/GardenApiDump.cs` → `garden_api_dump.txt`), sin abrir el juego.

Hallazgos del assembly:
- `SpawnResource._allowSpawningInFastForwarding` → **si es false NO produce al dormir** ✅ ya se fuerza a true
- `SpawnResource._totalSpawnsRemaining` → si llega a 0, se apaga para siempre ✅ ya se repone
- `SpawnResourceModel.nextSpawnTime` es **propiedad pública** ✅ ya se usa directo (sin reflexión)
- `GardenCatcher.Plant(cropId, isReplacement)` → método vanilla de plantar
- `GardenCatcher.CanAccept(id)` consulta `_plantableDict`, que arma `GardenCatcher.Awake()`
- `LandPlot.Attach(go, immediate, isReplacement, cue)` → así se adjunta el cultivo (y con él su SpawnResource)

**Diagnóstico medido:** `patchGarden(Clone)` con `gardenCatcher=True`, pero `attached=null` y `cultivo='sin cultivo'`
→ **el jardín está VACÍO**: nunca se le adjuntó cultivo. Sin cultivo no hay SpawnResource ni cosecha (vanilla).

### B1 — ¿por qué no se planta? (siguiente medición, ya instrumentado)
El log ahora imprime SIEMPRE:
```
[Garden] CATCHER 'patchGarden(Clone)': plantableDict=N Plantable[]=M awakeInvocado=x listo=x activator=x enabled=x acceptFX=x fruitGroup=x
```
- `plantableDict=0` o `-1` → `CanAccept` siempre false → **el juego rechaza el cultivo** → fix: construir el dict
  (invocar `Awake` vanilla, ya se intenta; si no alcanza, poblarlo a mano desde `Plantable`).
- `Plantable[]=-1/0` → el prefab del plot vino sin la lista → hay que copiarla de un jardín VANILLA del rancho.
- `activator=False` → `OnTriggerEnter` no sabe a qué plot plantar.

### B2 — Awake de TODOS los plots
Verificar que el `Awake()` vanilla corrió en **cada** componente clave de los plots que creamos
(`GardenCatcher`, `SpawnResource`, `PlortCollector`, `SlimeFeeder`, corrales). Patrón ya usado:
`CorralRegistrationHelper.InvokeVanillaAwake`. Extenderlo a todos y loguear cuáles fallan.

### B3 — Criaderos de gallinas (chickadoo)
No tocado aún. Mismo método: volcar del assembly (`Chickadoo`, `RanchCoop`, `SpawnResource` de gallinas) con
`GardenApiDump` y aplicar el mismo tratamiento (refs base + Awake + fast-forward).

---

## C. Referencias a revisar
- **SlimeRancher2Multiplayer (pyeight)** y **Starlight/SR2E (ThatFinn)**: ver cómo instancian y "despiertan"
  objetos del juego (registro en `SceneContext`/`GameModel`, participantes, Awake vanilla).
- **dnSpy**: para leer el CUERPO de `GardenCatcher.Awake`, `CanAccept` y `LandPlot.Attach` (el volcado da firmas,
  no el código). Eso diría exactamente qué necesita el dict y qué valida `CanAccept`.

## D. Reglas duras (no repetir errores)
1. Nunca `MaterialPropertyBlock` en SR2/HDRP (SRP Batcher lo ignora) → usar `renderer.materials`.
2. Nunca devolver material null → magenta.
3. `Shader.Find` falla seguido → `FindShaderByName` (escanea los cargados).
4. `Graphics.Blit` SÍ sirve con texturas no legibles (lo roto era `ImageConversion`/PNG).
5. No tocar el formato de carga viejo (`.scsm` v4).
6. **Medir antes de parchear.**
