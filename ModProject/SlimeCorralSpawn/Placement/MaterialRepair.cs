using System.Collections.Generic;
using UnityEngine;

namespace SlimeCorralSpawn.Placement
{
    /// <summary>
    /// AUTO-REPARADOR DE MATERIALES VIOLETA. A veces un renderer de estructura queda con un material sin
    /// shader válido (violeta/magenta) — pasa cuando se construye antes de que el template Lit del juego esté
    /// listo (el fallback Unlit puede no existir en un build HDRP → shader null = violeta) y queda así para
    /// siempre. Cada renderer de estructura se registra con sus parámetros (kind/color/emisivo) al construirse;
    /// este pase, throttleado y por presupuesto, detecta los que quedaron rotos y les RE-ASIGNA el material
    /// correcto en cuanto hay un shader válido disponible. No toca los que ya están bien (cero costo visible).
    /// </summary>
    internal static class MaterialRepair
    {
        private struct Entry
        {
            public MeshRenderer R;
            public Themes.MatKind Kind;
            public Color Color;
            public bool Emissive;
            public float Intensity;
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static int _cursor;
        private static float _next;
        private const float Interval = 0.4f;   // chequeo cada 0.4s (responsivo y barato)
        private const int Budget = 256;        // renderers revisados por pase (cubre cientos en ~1-2s)

        internal static void Track(MeshRenderer mr, Themes.MatKind kind, Color color, bool emissive, float intensity)
        {
            if (mr == null) return;
            _entries.Add(new Entry { R = mr, Kind = kind, Color = color, Emissive = emissive, Intensity = intensity });
        }

        internal static void Update()
        {
            if (_entries.Count == 0) return;
            if (Time.time < _next) return;
            _next = Time.time + Interval;

            int budget = Budget;
            int safety = _entries.Count;   // no dar más de una vuelta completa por pase
            while (budget > 0 && safety > 0)
            {
                if (_entries.Count == 0) break;
                if (_cursor >= _entries.Count) _cursor = 0;

                var e = _entries[_cursor];
                if (e.R == null)
                {
                    _entries.RemoveAt(_cursor);   // destruido → quitar (no avanzar cursor)
                    safety--;
                    continue;
                }

                Material m = null;
                try { m = e.R.sharedMaterial; } catch { }
                if (!PlacementManager.MaterialIsValid(m))
                    PlacementManager.ApplyStructureMaterial(e.R, e.Kind, e.Color, e.Emissive, e.Intensity);

                _cursor++;
                budget--;
                safety--;
            }
        }

        internal static void Reset()
        {
            _entries.Clear();
            _cursor = 0;
            _next = 0f;
        }
    }
}
