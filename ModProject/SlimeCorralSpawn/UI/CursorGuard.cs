using UnityEngine;

namespace SlimeCorralSpawn.UI
{
    /// <summary>
    /// Punto ÚNICO por el que el mod toca el cursor.
    ///
    /// ★ El bug que arregla ★ En la 2.0 el juego quedaba inusable: al volver al MENÚ PRINCIPAL el cursor
    /// aparecía bloqueado, como si estuvieras jugando, y no se podía clickear nada. Causa: varias herramientas
    /// hacen `Cursor.lockState = Locked` al cerrarse para devolverle la mira al jugador, y ModEntry las cierra
    /// justo cuando salís al menú. Con el rancho ya descargado, ese "devolver la mira" bloqueaba el cursor
    /// del menú.
    ///
    /// Regla: LIBERAR el cursor siempre es seguro; BLOQUEARLO solo tiene sentido dentro de la partida.
    /// Fuera de ella el mod no toca nada y deja mandar al juego.
    /// </summary>
    internal static class CursorGuard
    {
        /// <summary>¿Estamos dentro de la partida? Fuera de ella (menú principal, pantalla de carga, cambio de
        /// escena) el mod NO debe bloquear el cursor.</summary>
        public static bool InGameplay
        {
            get { try { return Placement.RealPlotFactory.ContextReady(); } catch { return false; } }
        }

        /// <summary>Bloquea el cursor (mira de juego). NO hace nada fuera de la partida.</summary>
        public static void Lock()
        {
            if (!InGameplay) return;
            try { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } catch { }
        }

        /// <summary>Libera el cursor. Siempre seguro: si algo salió mal, que el jugador pueda clickear.</summary>
        public static void Free()
        {
            try { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } catch { }
        }

        public static void Set(bool free) { if (free) Free(); else Lock(); }

        /// <summary>Red de seguridad para el cambio de escena: si el mod había bloqueado el cursor y ya no
        /// estamos en partida, se libera. Se llama desde OnSceneWasLoaded.</summary>
        public static void ReleaseIfOutsideGameplay()
        {
            if (InGameplay) return;
            try { if (Cursor.lockState != CursorLockMode.None) Free(); } catch { }
        }
    }
}
