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
    /// <para>While the Ark is inside a hostile cell of its leg (any cell on the way home, calm ones too when
    /// <see cref="VoyageToyDefinitionSO.KaijuInCalmCells"/>), every <see cref="VoyageToyDefinitionSO.KaijuSeconds"/> a
    /// KAIJU comes: a grazer of a leg cell held against the squad, born just outside the current cell's membrane on the
    /// side the Ark is heading, in its home cell's controlling colour through the platform's one fauna spawn seam (the
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
        float _nextKaiju = -1f;
        bool _kaijuToasted;
        ArkRaidLeash _raidLeash;

        ArkRaidLeash RaidLeash => _raidLeash ??= new ArkRaidLeash();

        /// <summary>Every assault tick (from <see cref="TickAssaults"/>): the next kaiju, when one is due.</summary>
        void TickKaiju(Ark ark, Cell cell, bool inside, bool hostile, bool homeward)
        {
            _kaiju.RemoveAll(f => !f || f.IsDying);
            RaidLeash.Ark = ark;
            if (!_def || _def.KaijuSeconds <= 0f || _def.KaijuMax <= 0 || _fallen || _docked
                || _state is not { AtSea: true } || _state.Current == null)
            {
                _nextKaiju = -1f;
                return;
            }

            // A new cell: its first kaiju comes a breath after the Ark crosses in.
            if (cell != _kaijuCell)
            {
                _kaijuCell = cell;
                _nextKaiju = -1f;
                _kaijuToasted = false;
            }
            bool due = cell && inside && (hostile || homeward || _def.KaijuInCalmCells);
            if (!due)
            {
                _nextKaiju = -1f;
                return;
            }
            if (_nextKaiju < 0f)
            {
                _nextKaiju = Time.time + _def.KaijuFirstSeconds;
                return;
            }
            if (Time.time < _nextKaiju || _kaiju.Count >= _def.KaijuMax) return;
            _nextKaiju = Time.time + _def.KaijuSeconds;

            var host = GrazerCell(ark.Domain, cell, out var species);
            if (!host) return;

            // Outside the membrane, toward where the Ark is heading, a little to one side: it meets the Ark in the cell.
            Vector3 centre = cell.transform.position;
            Vector3 ahead = ark.Position + ark.Forward * 600f - centre;
            Vector3 dir = ahead.sqrMagnitude > 1f ? ahead.normalized : ark.Forward;
            dir = (dir + Random.insideUnitSphere * 0.5f).normalized;
            float membrane = cell.MembraneRadius > 1f ? cell.MembraneRadius : CellConveyor.MembraneRadiusFloor;
            Vector3 at = centre + dir * (membrane + _def.KaijuOutsideMembrane);

            var cfg = species[Random.Range(0, species.Count)];
            var kaiju = CellLifeSpawnerBase.SpawnFaunaWithDomain(host, cfg.FaunaPrefab, ark.Position,
                host.ControllingDomain, at, cfg);
            if (!kaiju) return;
            kaiju.GrowBody(_def.KaijuScale);
            RaidLeash.Hold(kaiju);
            _kaiju.Add(kaiju);
            // One toast per cell: the first kaiju at its membrane.
            if (_kaijuToasted || Lines == null) return;
            _kaijuToasted = true;
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat, Lines.ToastKaiju, Lines.ToastKaijuSub);
        }

        /// <summary>The kaiju and raider pens: forget them (from <see cref="EndAssaults"/>). Creatures already born live on
        /// as ordinary creatures; the leash lets them go.</summary>
        void EndKaiju()
        {
            _kaiju.Clear();
            RaidLeash.ReleaseAll();
            RaidLeash.Ark = null;
            _kaijuCell = null;
            _nextKaiju = -1f;
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

        static bool TakeGrazers(Cell c, Domains squad, List<FaunaConfigurationSO> into)
        {
            into.Clear();
            if (!c || c.ControllingDomain == squad) return false;
            var profile = c.Config ? c.Config.SpawnProfile : null;
            if (!profile || profile.SupportedFaunas is not { Count: > 0 }) return false;
            foreach (var f in profile.SupportedFaunas)
                if (f && f.FaunaPrefab && f.FaunaPrefab.Diet == FaunaDiet.Herbivore && f.FaunaPrefab.Leashable)
                    into.Add(f);
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
