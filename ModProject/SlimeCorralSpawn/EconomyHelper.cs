using System;
using UnityEngine;
using Il2CppMonomiPark.SlimeRancher.Economy;   // CurrencyUtility, ICurrency (interop, prefijo Il2Cpp)
using PlayerState = Il2Cpp.PlayerState;          // tipo global -> namespace Il2Cpp

namespace SlimeCorralSpawn
{
    /// <summary>
    /// Puente a la economía REAL del juego (Newbucks).
    ///   - Moneda: CurrencyUtility.DefaultCurrency (el ICurrency de Newbucks).
    ///   - Saldo:  PlayerState.GetCurrency(ICurrency).
    ///   - Gasto:  PlayerState.SpendCurrency(ICurrency, int, IUIDisplayData).
    /// Todas las llamadas son defensivas: si la economía no es alcanzable (p.ej. en el
    /// menú principal, sin partida cargada), en modo dev se permite la compra para no
    /// bloquear el desarrollo. Nunca lanza: cualquier fallo de interop se loguea una vez.
    /// </summary>
    public static class EconomyHelper
    {
        private static ICurrency Newbucks()
        {
            try { return CurrencyUtility.DefaultCurrency; }
            catch (Exception ex) { ModEntry.LogErrorOnce("EconomyHelper.Newbucks", ex); return null; }
        }

        private static PlayerState FindPlayerState()
        {
            try { return UnityEngine.Object.FindObjectOfType<PlayerState>(); }
            catch (Exception ex) { ModEntry.LogErrorOnce("EconomyHelper.FindPlayerState", ex); return null; }
        }

        /// <summary>Saldo actual de Newbucks, o -1 si la economía no es alcanzable.</summary>
        public static int GetNewbucks()
        {
            try
            {
                var ps = FindPlayerState();
                var cur = Newbucks();
                if (ps == null || cur == null) return -1;
                return ps.GetCurrency(cur);
            }
            catch (Exception ex) { ModEntry.LogErrorOnce("EconomyHelper.GetNewbucks", ex); return -1; }
        }

        /// <summary>¿Puede pagar? No cobra. Si la economía no es alcanzable, permite (dev).</summary>
        public static bool CanAfford(int amount)
        {
            if (amount <= 0) return true;
            int bal = GetNewbucks();
            if (bal < 0) return true; // sin partida / economía no alcanzable -> permitir en dev
            return bal >= amount;
        }

        /// <summary>
        /// Intenta cobrar <paramref name="amount"/> Newbucks. Devuelve true si la compra
        /// debe proceder. Sólo bloquea (false) cuando la economía ES alcanzable y el
        /// jugador realmente no puede pagar.
        /// </summary>
        public static bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            try
            {
                var ps = FindPlayerState();
                var cur = Newbucks();
                if (ps == null || cur == null)
                {
                    ModEntry.Instance?.LoggerInstance.Warning(
                        $"Economía no alcanzable (PlayerState={(ps == null ? "null" : "ok")}, " +
                        $"Currency={(cur == null ? "null" : "ok")}). Permitiendo compra (dev).");
                    return true;
                }

                int have = ps.GetCurrency(cur);
                if (have < amount)
                {
                    ModEntry.Instance?.LoggerInstance.Msg($"Newbucks insuficientes: tienes {have}, cuesta {amount}.");
                    return false;
                }

                ps.SpendCurrency(cur, amount, null);
                return true;
            }
            catch (Exception ex)
            {
                ModEntry.LogErrorOnce("EconomyHelper.TrySpend", ex);
                return true; // dev: nunca bloquear el desarrollo por un fallo de interop
            }
        }
    }
}
