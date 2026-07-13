# Cómo escanear objetos del juego (SceneBuilder)

El catálogo de modelos se arma **solo** mientras jugás: `SceneModelLibrary.Tick()` corre en
`ModEntry.OnUpdate` (solo en el rancho) y recorre la jerarquía con presupuesto por frame. **El menú
NO depende de ningún dump ni tecla.** Esto queda documentado por si en el futuro hace falta volcar el
catálogo a mano para depurar.

## La idea del escaneo (`SceneModelLibrary.cs`)

1. `SceneManager.sceneCount` / `GetSceneAt(i).GetRootGameObjects()` → recorre TODAS las escenas cargadas.
2. Toma solo las raíces que empiezan con `zone` (y no contienen `Proxy`): son las zonas del juego.
3. BFS resumible (N nodos/frame). Por cada nodo:
   - Poda subárboles dinámicos/no-visuales (`(Clone)`, `Weather`, `FX`, `Loot`, `Resources`, `Drone`, …).
   - Si tiene `LODGroup` → captura el prop COMPLETO (con todos sus LOD) y no desciende.
   - Si tiene `MeshRenderer` y no es "ruido" → captura ese objeto.
   - Si no, desciende a los hijos.
4. Dedup por `zona + "/" + BaseKey(nombre)`. `BaseKey` quita el sufijo de instancia ` (N)` y `_LOD`,
   pero MANTIENE el número de variante (rockFields04 ≠ rockFields09).
5. Guarda un `Sample` (Transform vivo) para clonar; `Spawn()` clona bajo una raíz inactiva, le saca
   toda la lógica (MonoBehaviours) y lo activa.

## Volcar el catálogo a log (si hace falta, para depurar)

`SceneModelLibrary.DumpToLog()` sigue existiendo (dump incremental: solo lo nuevo desde el último llamado).
Para reactivarlo temporalmente, agregá en `ModEntry.OnUpdate`:

```csharp
if (InputHelper.GetKeyDown(UnityEngine.KeyCode.F9))
    SceneBuilder.SceneModelLibrary.DumpToLog();
```

## Inspeccionar tipos del juego sin abrir el juego

`ApiCheck/ZoneInspect.cs` (+ `.csproj`) carga los `Il2CppAssemblies` por reflexión y vuelca
propiedades/campos/métodos de cualquier tipo. Correr: `dotnet run --project ApiCheck/ZoneInspect.csproj`.
