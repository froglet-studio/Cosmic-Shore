using System;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A finished Toybox painting pays wallet crystals (P16, making pays; Loop Atlas retention build, 2026-11-21), so
    /// the painter's sittings feed the fleet. Paid for the first <see cref="RetentionTuningSO.PaintingsPaidPerDay"/>
    /// paintings finished each local day (<see cref="RetentionPay.PaintingCrystals"/>); the server's "painting" source
    /// holds the same cap (Tools/UGS/CloudCode/economy.js).
    /// </summary>
    public static class PaintingPay
    {
        const string KeyPrefix = "retention.painting.paid.";

        /// <summary>Pay the painting <paramref name="paintingId"/> just finished. Returns the crystals paid (0 past the day's cap).</summary>
        public static int PayFinished(string paintingId)
        {
            var tuning = RetentionTuningSO.Load();
            string key = KeyPrefix + DateTime.Now.ToString("yyyy-MM-dd");
            int paidToday = PlayerPrefs.GetInt(key, 0);
            int pay = RetentionPay.PaintingCrystals(paidToday, tuning.CrystalsPerPainting, tuning.PaintingsPaidPerDay);
            if (pay <= 0 || PlayerDataService.Instance == null) return 0;
            try
            {
                PlayerDataService.Instance.AddCrystals(pay, "painting");
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[Painting] Painting pay failed ({pay} crystals for {paintingId}): {e}");
                return 0;
            }
            PlayerPrefs.SetInt(key, paidToday + 1);
            PlayerPrefs.Save();
            return pay;
        }
    }
}
