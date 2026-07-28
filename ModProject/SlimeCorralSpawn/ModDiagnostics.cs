using UnityEngine;

namespace SlimeCorralSpawn
{
    /// <summary>
    /// Interruptor de los DIAGNÓSTICOS pesados del mod.
    ///
    /// Por qué existe: las verificaciones que se fueron agregando para depurar (`[Verify]`, `[MatCmp]`,
    /// `[GardenState]`, `[Produce]`…) corren AL ENTRAR A LA PARTIDA, que es justo el momento de más carga.
    /// `VerifyPlacedAssets` sola abre y parsea el .scsm de CADA modelo colocado (473 en la partida del usuario),
    /// después cada .scmat, y hace un File.Exists por textura → cientos de operaciones de disco en el hilo
    /// principal mientras el juego todavía está cargando. Eso es una parte medible de los ~15 s de tirón.
    ///
    /// Ahora están APAGADOS por defecto y se encienden desde Config cuando hace falta depurar algo.
    /// </summary>
    internal static class ModDiagnostics
    {
        private const string Key = "scs_diag";
        private static int _on = -1;

        public static bool Enabled
        {
            get { if (_on < 0) { try { _on = PlayerPrefs.GetInt(Key, 0); } catch { _on = 0; } } return _on != 0; }
            set { _on = value ? 1 : 0; try { PlayerPrefs.SetInt(Key, _on); PlayerPrefs.Save(); } catch { } }
        }

        /// <summary>Log que SOLO sale con los diagnósticos encendidos.</summary>
        public static void Log(string msg) { if (Enabled) ModEntry.LogInfo(msg); }
    }
}
