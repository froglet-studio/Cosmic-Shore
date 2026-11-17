using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Kaiju at the membrane, and the raiders' leash (Shombith, 2026-11-17: "more attacks outside the cell, big kaiju level
    /// fauna attacking the cell").
    ///
    /// <para>On every crossing between two cells of the leg (Shombith, 2026-11-17: "when the Ark is between cells, the kaiju
    /// should be between cells, and it should be huge"; never during the 30 s stop), a KAIJU waits in the open water: a
    /// grazer of a leg cell held against the squad, born in the gap a little off the Ark's line, in its home cell's
    /// controlling colour through the platform's one fauna spawn seam (the
    /// colour rule holds), grown <see cref="VoyageToyDefinitionSO.KaijuScale"/> times by <see cref="Fauna.GrowBody"/> in
    /// the spawn frame, and put on the run's <see cref="ArkRaidLeash"/>. Its body prisms are its species' own (the same
    /// collider count, larger), it dies the ordinary way, and its heart drops as every heart does.</para>
    ///
    /// <para>The leash is the Angler's (<see cref="IFaunaLeash"/>), held by the run: it steers the creature at the Ark and
    /// orders the nearest hull plate as its next meal. Behaviour only, the leash contract's own fence: the creature
    /// re-checks every ordered plate with its own diet (shielded plates and the nucleus interior stay off the menu), eats
    /// with its own mouthful and keeps its starvation clock. The open-water raiders wear it too, so a raider born from a
    /// far cell swims for the Ark instead of home. Only a herbivore that a leash can hold is drafted (a predator eats
    /// creatures, never plates). Each machine's own sea runs its own, as the raiders do; nothing is replicated.</para>
    /// </summary>
    public sealed partial class VoyageRun
    {
        readonly List<Fauna> _kaiju = new();
        Cell _kaijuCell;
        int _kaijuThisCrossing;
        ArkRaidLeash _raidLeash;

        ArkRaidLeash RaidLeash => _raidLeash ??= new ArkRaidLeash();

        /// <summary>
        /// Every assault tick (from <see cref="TickAssaults"/>): a kaiju for each crossing. The crossing starts when the
        /// Ark is within <see cref="VoyageToyDefinitionSO.KaijuLeadDistance"/> of leaving the cell behind it (or is already
        /// in open water); the kaiju is born in the open water between that cell and the next, a little off the Ark's
        /// line, and waits there for it. Never while the Ark is anchored or deep inside a cell.
        /// </summary>
        void TickKaiju(Ark ark, Cell bound)
        {
            _kaiju.RemoveAll(f => !f || f.IsDying);
            RaidLeash.Ark = ark;
            if (!_def || _def.KaijuMax <= 0 || _def.KaijuPerCrossing <= 0 || _fallen || _docked
                || _state is not { AtSea: true } || _state.Current == null || AnchoredNow)
                return;

            // The crossing is keyed on the cell the Ark is bound for: one batch of kaiju per gap.
            if (bound != _kaijuCell)
            {
                _kaijuCell = bound;
                _kaijuThisCrossing = 0;
            }
            if (_kaijuThisCrossing >= _def.KaijuPerCrossing || _kaiju.Count >= _def.KaijuMax) return;
            if (!CrossingOpen(ark, bound, out var behind)) return;

            var host = GrazerCell(ark.Domain, bound, out var species);
            if (!host) return;

            Vector3 at = GapPoint(ark, behind, bound);
            var cfg = species[Random.Range(0, species.Count)];
            var kaiju = CellLifeSpawnerBase.SpawnFaunaWithDomain(host, cfg.FaunaPrefab, ark.Position,
                host.ControllingDomain, at, cfg);
            if (!kaiju) return;
            kaiju.GrowBody(_def.KaijuScale);
            RaidLeash.Hold(kaiju);
            _kaiju.Add(kaiju);
            if (_kaijuThisCrossing++ > 0 || Lines == null) return;   // one toast per crossing
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat, Lines.ToastKaiju, Lines.ToastKaijuSub);
        }

        /// <summary>True while the Ark anchors at a cell's core for a raid window.</summary>
        bool AnchoredNow => _sea && _sea.IsAnchored;

        /// <summary>Is the Ark between cells, or about to be? <paramref name="behind"/> is the leg cell it is leaving
        /// (null in open water before the first).</summary>
        bool CrossingOpen(Ark ark, Cell bound, out Cell behind)
        {
            behind = null;
            if (bound && ArkEscortRun.ArkInsideWorld(bound, ark.Position)) return false;   // already in the next cell
            foreach (var c in _legIndexOf.Keys)
            {
                if (!c || c == bound || !ArkEscortRun.ArkInsideWorld(c, ark.Position)) continue;
                float membrane = c.MembraneRadius > 1f ? c.MembraneRadius : CellConveyor.MembraneRadiusFloor;
                float left = membrane - Vector3.Distance(ark.Position, c.transform.position);
                // Inside the cell behind: only its last stretch toward the membrane counts as the crossing.
                if (left > _def.KaijuLeadDistance) return false;
                behind = c;
                return true;
            }
            return true;   // outside every cell: open water
        }

        /// <summary>A point in the open water between <paramref name="behind"/> and <paramref name="bound"/>, off the
        /// Ark's line by <see cref="VoyageToyDefinitionSO.KaijuSideOffset"/>; ahead of the Ark when either is missing.</summary>
        Vector3 GapPoint(Ark ark, Cell behind, Cell bound)
        {
            Vector3 mid;
            Vector3 along;
            if (behind && bound)
            {
                Vector3 a = behind.transform.position, b = bound.transform.position;
                along = (b - a).sqrMagnitude > 1f ? (b - a).normalized : ark.Forward;
                float ra = behind.MembraneRadius > 1f ? behind.MembraneRadius : CellConveyor.MembraneRadiusFloor;
                float rb = bound.MembraneRadius > 1f ? bound.MembraneRadius : CellConveyor.MembraneRadiusFloor;
                float gap = Mathf.Max(0f, Vector3.Distance(a, b) - ra - rb);
                mid = a + along * (ra + gap * 0.5f);
            }
            else
            {
                along = ark.Forward;
                mid = ark.Position + along * _def.KaijuSideOffset;
            }
            Vector3 side = Vector3.ProjectOnPlane(Random.onUnitSphere, along);
            side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.Cross(along, Vector3.up).normalized;
            return mid + side * _def.KaijuSideOffset;
        }

        /// <summary>The kaiju and raider pens: forget them (from <see cref="EndAssaults"/>). Creatures already born live on
        /// as ordinary creatures; the leash lets them go.</summary>
        void EndKaiju()
        {
            _kaiju.Clear();
            RaidLeash.ReleaseAll();
            RaidLeash.Ark = null;
            _kaijuCell = null;
            _kaijuThisCrossing = 0;
        }

        static readonly List<FaunaConfigurationSO> s_grazerSpecies = new();

        /// <summary>A leg cell held by a colour other than the squad's (the Ark's own cell first) and its grazers a leash
        /// can hold: a kaiju's home. Null when there is none.</summary>
        Cell GrazerCell(Domains squad, Cell prefer, out List<FaunaConfigurationSO> species)
        {
            species = s_grazerSpecies;
            if (TakeGrazers(prefer, squad, species)) return prefer;
            foreach (var c in _legIndexOf.Keys)
                if (c != prefer && TakeGrazers(c, squad, species)) return c;
            return null;
        }

        /// <summary>
        /// Can this species be a kaiju? A leashable herbivore that swims alone: a <see cref="LightFauna"/> (today the
        /// Brittlestar and the QuadFish of the voyage worlds). A <see cref="Boid"/> (the TadPole) is a flock member whose
        /// steering reads its neighbours, so one grown 12x is a tadpole the size of a cell wall that still flocks; the
        /// colony and swarm bodies take a leash's meal order but not its steering. 2026-11-17 perf pass.
        /// </summary>
        public static bool IsKaijuSpecies(FaunaConfigurationSO f) =>
            f && f.FaunaPrefab is LightFauna { Diet: FaunaDiet.Herbivore, Leashable: true };

        static bool TakeGrazers(Cell c, Domains squad, List<FaunaConfigurationSO> into)
        {
            into.Clear();
            if (!c || c.ControllingDomain == squad) return false;
            var profile = c.Config ? c.Config.SpawnProfile : null;
            if (!profile || profile.SupportedFaunas is not { Count: > 0 }) return false;
            foreach (var f in profile.SupportedFaunas)
                if (IsKaijuSpecies(f)) into.Add(f);
            return into.Count > 0;
        }

        /// <summary>
        /// The run's leash on its kaiju and raiders: swim at the Ark, eat its nearest plate. Steering and the meal order
        /// only (<see cref="IFaunaLeash"/>); no mark, and it sustains nothing.
        /// </summary>
        sealed class ArkRaidLeash : IFaunaLeash
        {
            const float MealRefreshSeconds = 0.5f;

            public Ark Ark;

            sealed class Order
            {
                public Prism Meal;
                public float Next;
            }

            readonly Dictionary<Fauna, Order> _orders = new();
            readonly HashSet<Fauna> _held = new();

            /// <summary>Put <paramref name="creature"/> on this leash; false when it cannot be held (a predator, a
            /// puppet, another holder's).</summary>
            public bool Hold(Fauna creature)
            {
                if (!creature || !creature.TryLeash(this)) return false;
                _held.Add(creature);
                return true;
            }
            readonly List<Prism> _hull = new();

            public bool TrySteer(Fauna creature, out Vector3 goal, out float speed, out float turnRate)
            {
                goal = default;
                speed = 0f;
                turnRate = 0f;
                if (!Ark || Ark.IsHullLost || !creature) return false;
                var meal = OrderedMeal(creature);
                goal = meal ? meal.transform.position : Ark.Position;
                speed = creature.LeashTopSpeed;
                return speed > 0f;
            }

            public Prism OrderedMeal(Fauna creature)
            {
                if (!Ark || Ark.IsHullLost || !creature) return null;
                if (!_orders.TryGetValue(creature, out var order))
                {
                    order = new Order();
                    _orders[creature] = order;
                }
                bool stale = !order.Meal || order.Meal.destroyed || !order.Meal.gameObject.activeInHierarchy;
                if (!stale && Time.time < order.Next) return order.Meal;
                order.Next = Time.time + MealRefreshSeconds;
                order.Meal = null;
                Ark.GetLiveHullPrisms(creature.transform.position, _hull);
                foreach (var plate in _hull)
                {
                    if (!creature.LeashCanEat(plate)) continue;
                    order.Meal = plate;
                    break;
                }
                _hull.Clear();
                return order.Meal;
            }

            public void Released(Fauna creature)
            {
                if (!creature) return;
                _orders.Remove(creature);
                _held.Remove(creature);
            }

            public bool Sustains(Fauna creature) => false;

            public bool TryGetLeashMark(out Domains domain, out float lean, out float glow)
            {
                domain = default;
                lean = glow = 0f;
                return false;
            }

            public void ReleaseAll()
            {
                foreach (var creature in _held)
                    if (creature) creature.Unleash(this);
                _held.Clear();
                _orders.Clear();
            }
        }
    }
}
