using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppHDAdditionalLightData = UnityEngine.Rendering.HighDefinition.HDAdditionalLightData;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// Añade LUCES PUNTUALES HDRP reales a las partes luminosas de las estructuras (antorchas,
    /// farolas, pebeteros), pero MUY BARATAS: sin sombras, sin volumetría, y con un TOPE global de
    /// luces encendidas a la vez + culling por distancia a la cámara. Así no lagea aunque haya muchas.
    /// </summary>
    internal static class StructureLightHelper
    {
        // SIN CULLING: HDRP maneja 64 luces puntuales sin sombras ni volumetría sin problema.
        // Togglear enabled dispara el recálculo de light culling de HDRP = stutter. Así todas quedan
        // siempre prendidas y HDRP no recalcula nunca. (Máximo 64 luces registradas.)
        private const int MAX_TOTAL_LIGHTS = 64;
        private const float DEDUP_DIST_SQR = 0.6f * 0.6f;

        private static readonly List<Light> _lights = new List<Light>();
        private static float _lastClean;

        /// <summary>Adjunta una luz puntual HDRP barata al objeto dado y la registra para culling.</summary>
        internal static void AttachPointLight(GameObject host, Color color, float range, float intensity)
        {
            if (host == null) return;
            try
            {
                // Dedupe 1: si el host YA tiene una luz nuestra, no duplicar.
                if (host.transform.Find("SCS_Light") != null) return;
                // Dedupe 2 (CLAVE anti-muerte): al morir, el juego recarga la zona y las estructuras
                // se re-crean como objetos NUEVOS (el check por hijo falla) → se acumulaban luces cada
                // muerte = flickering + lag creciente. Dedup por POSICIÓN mundial: si ya hay una luz
                // nuestra casi en el mismo lugar, no agregar otra.
                Vector3 hostPos = host.transform.position;
                for (int i = _lights.Count - 1; i >= 0; i--)
                {
                    var ex = _lights[i];
                    if (ex == null) { _lights.RemoveAt(i); continue; }
                    if ((ex.transform.position - hostPos).sqrMagnitude < DEDUP_DIST_SQR) return;
                }
                // Tope duro: nunca pasar de MAX_TOTAL_LIGHTS registradas (si algo leakea, queda acotado).
                if (_lights.Count >= MAX_TOTAL_LIGHTS) return;

                var go = new GameObject("SCS_Light");
                go.transform.SetParent(host.transform, false);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.range = range;
                light.shadows = LightShadows.None;        // sin sombras: barato

                var hd = go.AddComponent<Il2CppHDAdditionalLightData>();
                try { hd.color = color; } catch { }
                try { hd.range = range; } catch { }
                try { hd.intensity = intensity; } catch { }
                // CLAVE anti-lag: NADA de volumetría ni sombras (es lo que hacía colapsar los FPS).
                try { hd.affectsVolumetric = false; } catch { }
                try { hd.EnableShadows(false); } catch { }
                try { hd.affectSpecular = false; } catch { }   // menos coste, casi imperceptible

                _lights.Add(light);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("StructureLightHelper.AttachPointLight", ex); }
        }

        /// <summary>Limpia luces destruidas cada ~3s. SIN culling — HDRP no recalcula = sin stutter.</summary>
        internal static void Update()
        {
            try
            {
                if (_lights.Count == 0) return;
                if (Time.realtimeSinceStartup - _lastClean < 3f) return;
                _lastClean = Time.realtimeSinceStartup;
                for (int i = _lights.Count - 1; i >= 0; i--)
                    if (_lights[i] == null) _lights.RemoveAt(i);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("StructureLightHelper.Update", ex); }
        }

        /// <summary>Limpia el registro al cambiar de escena (los GameObjects ya no son válidos).</summary>
        internal static void Reset() { _lights.Clear(); }
    }
}
