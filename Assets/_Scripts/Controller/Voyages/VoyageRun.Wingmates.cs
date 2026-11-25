using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// AI wingmates in a Crystal Run (Shombith, 2026-11-17: "The AI doesnt try to collect crystals and bank to the ark that
    /// much dont tune it much just a little bit I have a plan for that"). A light nudge over the escort roles, all of it
    /// on Voyage.asset ("Voyage - AI wingmates in a Crystal Run") and off at 0:
    /// <list type="bullet">
    /// <item>A wingmate carrying <c>wingmateBankAt</c> crystals flies to the Ark until it banks them.</item>
    /// <item>For <c>wingmateSeekSeconds</c> of every <c>wingmateSeekPeriod</c>, while no creature threatens the Ark, a
    /// wingmate lets its hull's own pilot hunt crystals. Seats are a third of the period apart, so at the defaults (8 s of
    /// 25 s) two wingmates never hunt at once.</item>
    /// </list>
    /// Outside a Crystal Run, after a fall, and otherwise, the role decides as before (<see cref="ArkEscortPilot.Errand"/>).
    /// </summary>
    public sealed partial class VoyageRun
    {
        ArkEscortErrand WingmateErrand(ArkEscortPilot pilot)
        {
            if (!pilot || !_active || !_underway || _fallen || !_def || !Raiding) return ArkEscortErrand.None;
            var status = pilot.Status;
            if (status == null || string.IsNullOrEmpty(status.PlayerName)) return ArkEscortErrand.None;

            int bankAt = _def.WingmateBankAt;
            if (bankAt > 0 && _raid.CarriedBy(status.PlayerName) >= bankAt) return ArkEscortErrand.Bank;

            float seek = _def.WingmateSeekSeconds, period = Mathf.Max(1f, _def.WingmateSeekPeriod);
            if (seek <= 0f || _assaultThreatsNear > 0) return ArkEscortErrand.None;
            float phase = Mathf.Repeat(Time.time + pilot.Seat * period / 3f, period);
            return phase < seek ? ArkEscortErrand.Seek : ArkEscortErrand.None;
        }
    }
}
