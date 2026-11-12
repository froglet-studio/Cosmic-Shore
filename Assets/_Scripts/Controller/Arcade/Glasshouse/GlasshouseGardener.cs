using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Glasshouse: a GARDENER that fetches seeds, plants and tends them with the Angler's lure, and
    /// raids a rival's garden once its school is big enough. Server-only. Plan step: <c>mass-arcade</c>
    /// (Docs/AI_SYSTEM/AI_PLAN.md - each vessel's Mass AI, on its Mass arcade game).
    ///
    /// <para><b>Input-only - the Angler's kit is untouched.</b> The gardener steers through
    /// <see cref="AIPilot.SetExternalTargetProvider"/> and presses ONE thing: the lure (the shared map's
    /// <see cref="InputEvents.Button1Action"/>, bound to <c>AnglerLure</c> on Angler.prefab - the generator asserts it),
    /// through <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/> so every peer's executor sees
    /// the same hold. Holding it stops the hull, grows every plant in reach and tames the grazers that wander into the
    /// light; letting go plants every seed the Angler carries (<c>AnglerCommandExecutor.PlantSeeds</c>). The paint and
    /// the throw are the hull's own autopilot (<c>AnglerLeapfrogAIPolicySO</c>), which paints along the way to whatever
    /// this gardener steers at - so steering at a rival's plant IS the raid.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Holding the lure</b>: stay on the bed until the hold runs out, then let go (the seeds are planted).</item>
    /// <item><b>Raiding</b> (the school holds <see cref="GlasshouseSettingsSO.aiRaidAtSchool"/> creatures and a rival
    /// plant stands): steer at the nearest rival plant for <see cref="GlasshouseSettingsSO.aiRaidSeconds"/>.</item>
    /// <item><b>Carrying a seed</b>, or due a tending visit: fly to the bed and press the lure on arrival.</item>
    /// <item><b>Otherwise</b>: the nearest omni crystal within reach (a seed and a creature), else back to the bed.</item>
    /// </list>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class GlasshouseGardener
    {
        /// <summary>The lure's input on the Angler's shared map (Angler.prefab: Button1Action -> AnglerLure).</summary>
        public const InputEvents LureInput = InputEvents.Button1Action;

        sealed class Gardener
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public AnglerCommandExecutor Angler;
            public Vector3 Bed;
            public Vector3 Steer;

            public bool Luring;
            public float LureUntil;
            public float NextTendAt;
            public float RaidUntil;
        }

        readonly List<Gardener> _gardeners = new();
        GlasshouseSettingsSO _settings;

        public bool IsArmed => _gardeners.Count > 0;

        /// <summary>
        /// Where seat <paramref name="seat"/> plants: a point <paramref name="bandFraction"/> of the way from the
        /// nucleus's surface to the membrane, on a direction of its own (a golden-angle spiral over the sphere), so
        /// two gardeners never share a bed.
        /// </summary>
        public static Vector3 BedFor(int seat, Vector3 centre, float nucleusRadius, float membraneRadius,
                                     float bandFraction)
        {
            const float golden = 2.39996323f;
            float y = 1f - 2f * ((seat % 8) + 0.5f) / 8f;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float th = seat * golden;
            var dir = new Vector3(ring * Mathf.Cos(th), y, ring * Mathf.Sin(th));
            float r = nucleusRadius + Mathf.Clamp01(bandFraction) * Mathf.Max(0f, membraneRadius - nucleusRadius);
            return centre + dir * r;
        }

        /// <summary>Arms every AI Angler on the roster, each on its own bed.</summary>
        public void Arm(GameDataSO gameData, GlasshouseSettingsSO settings, Vector3 centre, float nucleusRadius,
                        float membraneRadius)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _settings = settings;

            var players = gameData.Players;
            int seat = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status == null || !status.Transform) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                if (autopilot == null) continue;
                var angler = status.Transform.GetComponentInChildren<AnglerCommandExecutor>(true);
                if (!angler) continue;   // the card seats only Anglers; anything else flies the platform's steering

                var gardener = new Gardener
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Angler = angler,
                    Bed = BedFor(seat++, centre, nucleusRadius, membraneRadius, settings.aiGardenBandFraction),
                };
                gardener.Steer = gardener.Bed;
                gardener.NextTendAt = Time.time + settings.aiTendIntervalSeconds;
                _gardeners.Add(gardener);
                var captured = gardener;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Lets go of every lure and hands every autopilot back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _gardeners.Count; i++)
            {
                var g = _gardeners[i];
                SetLure(g, false);
                if (g.Autopilot) g.Autopilot.ClearExternalTargetProvider();
            }
            _gardeners.Clear();
        }

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _gardeners.Count; i++)
            {
                var g = _gardeners[i];
                var self = g.Player?.Vessel?.Transform;
                if (!self || !g.Autopilot || !g.Autopilot.AutoPilotEnabled || !g.Angler)
                {
                    SetLure(g, false);
                    continue;
                }

                if (g.Luring)
                {
                    g.Steer = g.Bed;
                    if (now < g.LureUntil) continue;
                    SetLure(g, false);   // letting go plants every seed the Angler carries
                    g.NextTendAt = now + _settings.aiTendIntervalSeconds;
                    continue;
                }

                if (now < g.RaidUntil && NearestRivalPlant(self.position, g.Player.Domain) is Vector3 raid)
                {
                    g.Steer = raid;
                    continue;
                }

                if (g.Angler.TamedCount >= GlasshouseSettingsSO.Rung(_settings.aiRaidAtSchool, g.Level)
                    && NearestRivalPlant(self.position, g.Player.Domain) is Vector3 target)
                {
                    g.RaidUntil = now + GlasshouseSettingsSO.Rung(_settings.aiRaidSeconds, g.Level);
                    g.Steer = target;
                    continue;
                }

                bool toBed = g.Angler.FloraSeedCount > 0 || now >= g.NextTendAt;
                if (!toBed)
                {
                    float reach = GlasshouseSettingsSO.Rung(_settings.aiSeedReach, g.Level);
                    if (NearestOmniCrystal(self.position, reach) is Vector3 crystal)
                    {
                        g.Steer = crystal;
                        continue;
                    }
                }

                g.Steer = g.Bed;
                if ((self.position - g.Bed).sqrMagnitude <= _settings.aiArriveDistance * _settings.aiArriveDistance)
                {
                    SetLure(g, true);
                    g.LureUntil = now + GlasshouseSettingsSO.Rung(_settings.aiLureHoldSeconds, g.Level);
                }
            }
        }

        /// <summary>The lure: the replicated press and release of the shared map's lure button.</summary>
        static void SetLure(Gardener g, bool held)
        {
            if (held == g.Luring) return;
            g.Luring = held;
            var handler = g.Status?.ActionHandler;
            if (handler == null) return;
            if (held) handler.PerformShipControllerActionsReplicated(LureInput);
            else handler.StopShipControllerActionsReplicated(LureInput);
        }

        /// <summary>The heart of the nearest plant standing in a playable domain other than <paramref name="own"/>,
        /// or null.</summary>
        static Vector3? NearestRivalPlant(Vector3 from, Domains own)
        {
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            var live = FloraHeartRegistry.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var f = live[i];
                if (!f || f.IsDying || f.Domain == own || GlasshouseGarden.Slot(f.Domain) < 0) continue;
                Vector3 at = f.HeartTransform.position;
                float d = (at - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = at;
            }
            return best;
        }

        /// <summary>The nearest live OMNI crystal within <paramref name="reach"/> (a seed and a creature for an
        /// Angler), or null. Elemental crystals - every lifeform's heart included - are skipped.</summary>
        static Vector3? NearestOmniCrystal(Vector3 from, float reach)
        {
            Vector3? best = null;
            float bestSqr = reach * reach;
            var active = Crystal.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                if (!c || c.IsEmbedded || c.crystalProperties.IsElemental) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = c.transform.position;
            }
            return best;
        }
    }
}
