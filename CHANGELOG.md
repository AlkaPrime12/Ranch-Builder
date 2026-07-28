# Custom Ranch Builder v2.0

The mod is now called **Custom Ranch Builder** (it used to be "Slime Corral Spawn").

## New features
- **Scene Tool redesigned** — panel izquierdo con grid de modelos, free cam abajo, modos Continuo/Borrar con F/D/R
- **Continuous Mode** (F) — coloca modelos sin cancelar el ghost
- **Delete Mode** (D) — clickea modelos colocados para borrarlos al instante
- **Cursor Unlock** (R) — suelta el cursor para interactuar con el panel de modelos
- **Zone names** — traducidas y agrupadas (Conservatorio, Campos Arcoíris, Costa Estelar, Valle Ember, Transiciones)
- **OpenEditor/CloseEditor** — API pública para abrir/cerrar el Scene Tool

## Fixes
- Luces clonadas ahora fuerzan enabled + intensidad HDRP
- Materiales multitextura (montañas) ya no pierden el blend roca+pasto
- Crash del buscador por GUI.SetNextControlName roto en Il2Cpp — reemplazado por input manual
- Modelos con nombres duplicados ya no rompen el catálogo

## Known issues
- Pequeño hitch al abrir el menú F5 la primera vez
- Cercas y algunos props sin malla legible no pueden colocarse si su zona no está cargada

— AlkaPrime12
