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
        // Máximo de luces del mod ENCENDIDAS a la vez (HDRP cobra caro por cada luz en tiempo real).
        private const int MAX_ACTIVE_LIGHTS = 8;
        private const float CULL_INTERVAL = 0.5f;

        private static readonly List<Light> _lights = new List<Light>();
        private static float _lastCull = -999f;

        /// <summary>Adjunta una luz puntual HDRP barata al objeto dado y la registra para culling.</summary>
        internal static void AttachPointLight(GameObject host, Color color, float range, float intensity)
        {
            if (host == null) return;
            try
            {
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

        /// <summary>Periódico: deja encendidas solo las MAX_ACTIVE_LIGHTS luces más cercanas a la cámara.</summary>
        internal static void Update()
        {
            try
            {
                if (Time.realtimeSinceStartup - _lastCull < CULL_INTERVAL) return;
                _lastCull = Time.realtimeSinceStartup;
                if (_lights.Count == 0) return;

                // Limpiar luces destruidas (estructuras borradas).
                for (int i = _lights.Count - 1; i >= 0; i--)
                    if (_lights[i] == null) _lights.RemoveAt(i);
                if (_lights.Count == 0) return;

                Camera cam = Camera.main;
                Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;

                // Si hay pocas, encender todas; si hay muchas, solo las más cercanas.
                if (_lights.Count <= MAX_ACTIVE_LIGHTS || cam == null)
                {
                    foreach (var l in _lights) if (l != null && !l.enabled) l.enabled = true;
                    return;
                }

                // Ordenar por distancia (cuadrada) a la cámara y encender solo las primeras N.
                _lights.Sort((a, b) =>
                {
                    float da = a == null ? float.MaxValue : (a.transform.position - camPos).sqrMagnitude;
                    float db = b == null ? float.MaxValue : (b.transform.position - camPos).sqrMagnitude;
                    return da.CompareTo(db);
                });
                for (int i = 0; i < _lights.Count; i++)
                {
                    var l = _lights[i];
                    if (l == null) continue;
                    bool on = i < MAX_ACTIVE_LIGHTS;
                    if (l.enabled != on) l.enabled = on;
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("StructureLightHelper.Update", ex); }
        }

        /// <summary>Limpia el registro al cambiar de escena (los GameObjects ya no son válidos).</summary>
        internal static void Reset() { _lights.Clear(); }
    }
}
