using System;
using UnityEngine;

namespace SlimeCorralSpawn.UI
{
    /// <summary>
    /// Bloquea el input del JUEGO mientras hay un menú del mod abierto.
    ///
    /// Sin esto, con un menú abierto seguías moviendo la cámara, aspirando con el vac y —lo peor— TIRANDO cosas
    /// del inventario con el click. Es exactamente lo que no querés mientras estás configurando algo.
    /// El mecanismo es el de Starlight: desactivar los action maps del InputDirector (mainGame + paused).
    /// Desactivar el SRCameraController solo no alcanza: la cámara igual se movía.
    ///
    /// Es un CONTADOR, no un booleano: si dos menús lo piden, el input se restaura recién cuando ambos cierran.
    /// </summary>
    internal static class GameInputBlock
    {
        private static int _holders;
        private static bool _applied;

        public static bool Blocked => _holders > 0;

        /// <summary>Cada menú llama a esto CADA FRAME mientras está abierto (want=true) o al cerrarse (false).</summary>
        public static void Want(string who, bool want)
        {
            if (want) { if (_wanters.Add(who)) Refresh(); }
            else { if (_wanters.Remove(who)) Refresh(); }
        }
        private static readonly System.Collections.Generic.HashSet<string> _wanters =
            new System.Collections.Generic.HashSet<string>();

        private static void Refresh()
        {
            _holders = _wanters.Count;
            bool want = _holders > 0;
            if (want == _applied) return;
            _applied = want;
            Apply(want);
        }

        private static void Apply(bool block)
        {
            try
            {
                var gc = Il2Cpp.GameContext.Instance;
                if (gc == null || gc.InputDirector == null) return;
                var id = gc.InputDirector;
                if (block)
                {
                    try { id._mainGame.Map.Disable(); } catch { }
                    try { id._paused.Map.Disable(); } catch { }
                }
                else
                {
                    try { id._mainGame.Map.Enable(); } catch { }
                    try { id._paused.Map.Enable(); } catch { }
                }
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("GameInputBlock.Apply", ex); }
        }

        /// <summary>Red de seguridad: al cambiar de escena/partida se suelta todo (si no, el input podría quedar
        /// bloqueado para siempre si un menú se destruye sin cerrarse).</summary>
        public static void ReleaseAll()
        {
            if (_wanters.Count == 0 && !_applied) return;
            _wanters.Clear();
            _holders = 0;
            if (_applied) { _applied = false; Apply(false); }
        }
    }
}
