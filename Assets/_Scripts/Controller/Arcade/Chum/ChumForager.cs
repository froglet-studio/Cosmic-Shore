using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Chum: a FORAGER that builds a school (omni crystals, then grazers tamed under the lure) and takes
    /// it to the reef. Server-only. Plan step: <c>space-arcade</c> (Docs/AI_SYSTEM/AI_PLAN.md - each vessel's Space AI,
    /// on its Space arcade game).
    ///
    /// <para><b>Input-only - the Angler's kit is untouched.</b> The forager steers through
    /// <see cref="AIPilot.SetExternalTargetProvider"/> and presses ONE thing: the lure
    /// (<see cref="InputEvents.Button1Action"/>, bound to <c>AnglerLure</c> on Angler.prefab - the generator asserts it),
    /// through <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/>, Glasshouse's gardener's shape.
    /// The paint and the throw are the hull's own autopilot (<c>AnglerLeapfrogAIPolicySO</c>): lined up on whatever this
    /// forager steers at, it holds the brush along the nose and lets go - so steering at a shoal IS the attack.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Holding the lure</b>: stay put until the hold runs out, then let go.</item>
    /// <item><b>School under target</b> (<see cref="ChumSettingsSO.aiSchoolTarget"/>): the nearest omni crystal within
    /// reach, else the nearest wild grazer - fly to it and hold the lure on arrival.</item>
    /// <item><b>Otherwise</b>: the shoal it picked (the nearest with prisms left, re-picked every
    /// <see cref="ChumSettingsSO.aiRetargetSeconds"/>), approached to a stand-off so the throw has room.</item>
    /// </list>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class ChumForager
    {
        /// <summary>The lure's input on the Angler's shared map (Angler.prefab: Button1Action -> AnglerLure).</summary>
        public const InputEvents LureInput = InputEvents.Button1Action;

        /// <summary>Answers where shoal <c>i</c>'s centre is, and how many of its prisms still stand.</summary>
        public interface IReefView
        {
            int ShoalCount { get; }
            Vector3 ShoalCentre(int shoal);
            int Standing(int shoal);
        }

        sealed class Forager
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public AnglerCommandExecutor Angler;
            public Vector3 Steer;

            public bool Luring;
            public float LureUntil;
            public int Shoal = -1;
            public float RetargetAt;
        }

        readonly List<Forager> _foragers = new();
        ChumSettingsSO _settings;
        IReefView _reef;
        Cell _cell;

        public bool IsArmed => _foragers.Count > 0;

        /// <summary>Arms every AI Angler on the roster.</summary>
        public void Arm(GameDataSO gameData, ChumSettingsSO settings, IReefView reef, Cell cell)
        {
            Disarm();
            if (gameData == null || settings == null || reef == null) return;
            _settings = settings;
            _reef = reef;
            _cell = cell;

            var players = gameData.Players;
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
                if (!angler) continue;   // the card seats only Anglers

                var forager = new Forager
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Angler = angler,
                    Steer = status.Transform.position + status.Transform.forward * 200f,
                };
                _foragers.Add(forager);
                var captured = forager;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Lets go of every lure and hands every autopilot back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _foragers.Count; i++)
            {
                var f = _foragers[i];
                SetLure(f, false);
                if (f.Autopilot) f.Autopilot.ClearExternalTargetProvider();
            }
            _foragers.Clear();
        }

        public void Tick()
        {
            if (_settings == null || _reef == null) return;
            float now = Time.time;
            for (int i = 0; i < _foragers.Count; i++)
            {
                var f = _foragers[i];
                var self = f.Player?.Vessel?.Transform;
                if (!self || !f.Autopilot || !f.Autopilot.AutoPilotEnabled || !f.Angler)
                {
                    SetLure(f, false);
                    continue;
                }

                if (f.Luring)
                {
                    if (now < f.LureUntil) continue;
                    SetLure(f, false);
                    continue;
                }

                Vector3 at = self.position;
                if (f.Angler.TamedCount < ChumSettingsSO.Rung(_settings.aiSchoolTarget, f.Level))
                {
                    float reach = ChumSettingsSO.Rung(_settings.aiCrystalReach, f.Level);
                    if (NearestOmniCrystal(at, reach) is Vector3 crystal)
                    {
                        f.Steer = crystal;
                        continue;
                    }
                    if (NearestWildGrazer(at) is Vector3 grazer)
                    {
                        f.Steer = grazer;
                        if ((grazer - at).sqrMagnitude <= _settings.aiLureDistance * _settings.aiLureDistance)
                        {
                            SetLure(f, true);
                            f.LureUntil = now + ChumSettingsSO.Rung(_settings.aiLureHoldSeconds, f.Level);
                        }
                        continue;
                    }
                }

                if (f.Shoal < 0 || now >= f.RetargetAt || _reef.Standing(f.Shoal) <= 0)
                {
                    f.Shoal = NearestStandingShoal(at);
                    f.RetargetAt = now + ChumSettingsSO.Rung(_settings.aiRetargetSeconds, f.Level);
                }
                if (f.Shoal < 0) continue;

                // Steer AT the shoal (the leapfrog policy paints along the nose when it is lined up), but hold off at
                // the stand-off: past it, aim through the shoal's far side so the hull circles rather than ploughs in.
                Vector3 centre = _reef.ShoalCentre(f.Shoal);
                Vector3 to = centre - at;
                f.Steer = to.magnitude > _settings.aiStandOff
                    ? centre
                    : centre + Vector3.Cross(to.normalized, self.up) * _settings.aiStandOff;
            }
        }

        static void SetLure(Forager f, bool held)
        {
            if (held == f.Luring) return;
            f.Luring = held;
            var handler = f.Status?.ActionHandler;
            if (handler == null) return;
            if (held) handler.PerformShipControllerActionsReplicated(LureInput);
            else handler.StopShipControllerActionsReplicated(LureInput);
        }

        int NearestStandingShoal(Vector3 from)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int s = 0; s < _reef.ShoalCount; s++)
            {
                if (_reef.Standing(s) <= 0) continue;
                float d = (_reef.ShoalCentre(s) - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = s;
            }
            return best;
        }

        /// <summary>The nearest wild creature an Angler's lure could tame (leashable, not on anyone's leash), or null.</summary>
        Vector3? NearestWildGrazer(Vector3 from)
        {
            if (!_cell) return null;
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            var live = _cell.LiveFauna;
            for (int i = 0; i < live.Count; i++)
            {
                var fauna = live[i];
                if (!fauna || fauna.IsLeashed || !fauna.Leashable) continue;
                float d = (fauna.transform.position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = fauna.transform.position;
            }
            return best;
        }

        /// <summary>The nearest live OMNI crystal within <paramref name="reach"/>, or null. Elemental crystals - every
        /// lifeform's heart included - are skipped.</summary>
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
