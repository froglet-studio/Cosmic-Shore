using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One vessel's way of defending the Ark (<see cref="ArkEscortPilot"/> dispatches to it by hull): where to fly
    /// (<see cref="Steer"/>, fed to the vessel's own <see cref="AIPilot"/> as its target), what to press and when
    /// (<see cref="Tick"/>), how far it may roam (<see cref="Leash"/>). The roles and the reasoning behind each are in
    /// D:\Voyage\briefs\vessel_kits_ai.md and Docs/Voyages/ARK_ESCORT.md.
    /// </summary>
    public interface IArkEscortRole
    {
        string Name { get; }
        void Arm(ArkEscortPilot pilot);
        void Disarm();
        /// <summary>Where the hull should fly this frame.</summary>
        Vector3 Steer(ArkEscortContext ctx);
        /// <summary>False lets the vessel's own pilot steer (its crystal seeking); true feeds <see cref="Steer"/>.</summary>
        bool WantsSteering(ArkEscortContext ctx);
        void Tick(ArkEscortContext ctx);
        /// <summary>The wingmate leash for this seat, given the squad's.</summary>
        float Leash(ArkEscortContext ctx, float leash);
        void OnLegChanged(Cell cell);
        /// <summary>What this role did, one line for the report (rockets fired, jousts, crystals taken...), or null.</summary>
        string Notes();
    }

    /// <summary>Shared plumbing for the roles: the seat, the hull, its pilot, pressing abilities and inputs.</summary>
    public abstract class ArkEscortRoleBase : IArkEscortRole
    {
        protected ArkEscortPilot Pilot;
        protected AIPilot Ai;
        protected IVesselStatus Status;
        protected Transform Self;

        readonly List<(InputEvents input, float releaseAt)> _taps = new();

        public abstract string Name { get; }

        protected int Seat => Pilot ? Pilot.Seat : 0;
        protected Vector3 Position => Self ? Self.position : default;
        protected float Speed => Status != null ? Mathf.Abs(Status.Speed) : 0f;

        /// <summary>The nose: where a gun's round or a joust goes.</summary>
        protected Vector3 Forward
        {
            get
            {
                if (Status?.ShipTransform) return Status.ShipTransform.forward;
                return Self ? Self.forward : Vector3.forward;
            }
        }

        /// <summary>The direction of travel (the course while drifting, else the nose).</summary>
        protected Vector3 Heading
        {
            get
            {
                if (Status != null && Status.Course.sqrMagnitude > 1e-4f) return Status.Course.normalized;
                return Forward;
            }
        }

        public virtual void Arm(ArkEscortPilot pilot)
        {
            Pilot = pilot;
            Ai = pilot ? pilot.Ai : null;
            Status = pilot ? pilot.Status : null;
            Self = pilot ? pilot.transform : null;
            _taps.Clear();
        }

        public virtual void Disarm()
        {
            foreach (var tap in _taps) Release(tap.input);
            _taps.Clear();
        }

        public abstract Vector3 Steer(ArkEscortContext ctx);
        public virtual bool WantsSteering(ArkEscortContext ctx) => true;

        public void Tick(ArkEscortContext ctx)
        {
            ReleaseDueTaps();
            OnTick(ctx);
        }

        protected virtual void OnTick(ArkEscortContext ctx) { }
        public virtual float Leash(ArkEscortContext ctx, float leash) => leash;
        public virtual void OnLegChanged(Cell cell) { }
        public virtual string Notes() => null;

        /// <summary>"seat N Vessel(name)" for the report lines.</summary>
        protected string Who => $"seat {Seat + 1} {(Status != null ? Status.VesselType.ToString() : "?")}({(Status != null ? Status.PlayerName : "?")})";

        // ── Helpers ──────────────────────────────────────────────────────────

        protected Vector3 Station(ArkEscortContext ctx) => ctx.Station(Seat, EscortGeneRegistry.StationRadius);

        protected static float AngleFrom(Vector3 forward, Vector3 to) =>
            to.sqrMagnitude < 1e-6f ? 0f : Vector3.Angle(forward, to);

        /// <summary>This pilot's instance of an ability in its cycler list (the prefab's <c>abilities</c>).</summary>
        protected bool TryAbility<T>(out T ability) where T : ShipActionSO
        {
            ability = null;
            return Ai && Ai.TryFindAbility(out ability);
        }

        /// <summary>Take an ability off the pilot's blind timer: this role owns its timing from now on.</summary>
        protected void Suppress(ShipActionSO ability)
        {
            if (Ai && ability) Ai.SetAbilityCycleSuppressed(ability, true);
        }

        protected void Press(ShipActionSO ability, bool down)
        {
            if (Ai && ability) Ai.PressAbility(ability, down);
        }

        /// <summary>The control a hull puts an action of type <typeparamref name="T"/> on, if it binds one.</summary>
        protected bool TryInputFor<T>(out InputEvents input) where T : class
        {
            input = default;
            var handler = Status?.ActionHandler;
            return handler != null && handler.TryGetInputForAction<T>(out input);
        }

        /// <summary>Press a control and let go a beat later, replicated (the Broadside pattern).</summary>
        protected void Tap(InputEvents input, float holdSeconds = 0.15f)
        {
            var handler = Status?.ActionHandler;
            if (handler == null) return;
            handler.PerformShipControllerActionsReplicated(input);
            _taps.Add((input, Time.time + holdSeconds));
        }

        void ReleaseDueTaps()
        {
            for (int i = _taps.Count - 1; i >= 0; i--)
            {
                if (Time.time < _taps[i].releaseAt) continue;
                Release(_taps[i].input);
                _taps.RemoveAt(i);
            }
        }

        void Release(InputEvents input)
        {
            var handler = Status?.ActionHandler;
            if (handler != null) handler.StopShipControllerActionsReplicated(input);
        }

        protected float Ammo(int index)
        {
            var resources = Status?.ResourceSystem;
            if (!resources || resources.Resources == null || (uint)index >= (uint)resources.Resources.Count) return 0f;
            var res = resources.Resources[index];
            return res != null ? res.CurrentAmount : 0f;
        }
    }

    /// <summary>
    /// The fallback for a hull without a role of its own (Manta, Rhino, Stoat...): fly at the claimed creature's heart
    /// (a joust kills only on the hulls whose container withers lifeforms by crystal, but the hull's own cycled
    /// abilities go off pointed at the creature), hold the station with nothing in reach.
    /// </summary>
    public sealed class HeartChaserRole : ArkEscortRoleBase
    {
        Fauna _target;
        public override string Name => "HeartChaser";

        public override Vector3 Steer(ArkEscortContext ctx)
        {
            _target = ctx.Claim(Seat, _target);
            return _target ? ArkEscortContext.HeartOf(_target) : Station(ctx);
        }
    }

    /// <summary>Which role each hull flies, and the element the sim seats it in.</summary>
    public static class ArkEscortRoles
    {
        public static IArkEscortRole Create(VesselClassType vessel) => vessel switch
        {
            VesselClassType.Sparrow => new SparrowDestroyerRole(),
            VesselClassType.Squirrel => new SquirrelRole(),
            VesselClassType.Dolphin => new DolphinRole(),
            VesselClassType.Serpent => new SerpentWallRole(),
            VesselClassType.Scarab => new ScarabRingRole(),
            _ => new HeartChaserRole(),
        };

        /// <summary>
        /// The element an unmanned sim seat is levelled in. Picked for the level-5 safety each kit has (the toy seeds
        /// seats at 0.5, level 5): the Sparrow's Domain-Safe Skybursts (Charge), the Dolphin's Clean Blast (Space),
        /// the Serpent's Shield Steal (Space); the rest in the element their escort job leans on.
        /// </summary>
        public static Element DefaultElement(VesselClassType vessel) => vessel switch
        {
            VesselClassType.Sparrow => Element.Charge,
            VesselClassType.Dolphin => Element.Space,
            VesselClassType.Serpent => Element.Space,
            VesselClassType.Squirrel => Element.Charge,
            VesselClassType.Scarab => Element.Charge,
            _ => Element.Charge,
        };
    }
}
