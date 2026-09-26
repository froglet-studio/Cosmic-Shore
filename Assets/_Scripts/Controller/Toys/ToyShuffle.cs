using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// <b>Shuffle the Toy Box</b> - a new world, a new domain, a new hull, in one press.
    ///
    /// <para><b>It calls the toys, it does not reimplement them.</b> Every change is a live
    /// <see cref="ToyShellOption.Apply"/> - literally the call the ring makes - so a shuffle can
    /// never do something no toy can do, and a toy authored tomorrow joins the shuffle by
    /// implementing one interface. A menu-side table of "set the cell, set the domain, set the
    /// hull" would be the second authority on that state the single-writer rule exists to prevent
    /// (<c>Docs/HomeHub/ARCHITECTURE.md</c> §4.1.1).</para>
    ///
    /// <para><b>What it shuffles is a SETTING, and the toy already says which those are.</b> An
    /// option that <see cref="ToyShellOption.RequiresFreestyle"/> only means anything with the
    /// player at the stick - a painting, a wander, a voyage - so it is an ACTIVITY and belongs to
    /// the button beside this one (<c>DailyToyActivity</c>). Everything else is a setting and is
    /// fair game. That one flag is the whole scope rule: no list of toys here, no per-toy
    /// exception, and nothing to update when a toy is added.</para>
    ///
    /// <para><b>PLAN, then APPLY.</b> Every decision is taken against the registry as it stands
    /// before anything changes, because applying a cell swap tears the toybox down and
    /// unregisters every other surface - a shuffle that decided as it went would be deciding
    /// against destroyed toys halfway through. For the same reason the <see cref="ToyCategory.World"/>
    /// picks go LAST, after the ones that only change you.</para>
    ///
    /// <para><b>Nothing here is a cull or a clock.</b> A cell swap removes mass because a player
    /// asked for a new world - the same explicit, active event class as flying the station, which
    /// is what keeps this inside the conserved-mass law (<c>Docs/ECOSYSTEM.md</c> §19). There is no
    /// auto-shuffle and there must never be one.</para>
    /// </summary>
    public static class ToyShuffle
    {
        /// <summary>
        /// How deep a random descent will walk before giving up on a toy's tree. A descent does
        /// NOT backtrack: a branch that turns out to hold no setting costs that toy its turn in
        /// this shuffle rather than being retried. Deliberate - every shipped branch (the Lifeform
        /// Matrix's three kingdoms) leads to real leaves, and backtracking would expand exactly
        /// the subtrees the descent exists to avoid expanding.
        /// </summary>
        const int MaxDescent = 6;

        /// <summary>Beat between the settings that change YOU and the one that changes the WORLD,
        /// so a hull respawn has landed before a veiled cell build starts on top of it.</summary>
        const int WorldDelayMs = 700;

        /// <summary>One decision: the option, and whose it is.</summary>
        public readonly struct Pick
        {
            public readonly IToyShellSurface Surface;
            public readonly ToyShellOption Option;
            public readonly string ToyName;
            public readonly ToyCategory Category;

            public Pick(IToyShellSurface surface, ToyShellOption option, string toyName,
                        ToyCategory category)
            {
                Surface = surface;
                Option = option;
                ToyName = toyName ?? "";
                Category = category;
            }

            public bool IsValid => Surface != null && Option?.Apply != null;
        }

        static readonly List<ToyShellOption> _layer = new();
        static readonly List<int> _eligible = new();

        /// <summary>
        /// One random setting per live toy, in the order they must be applied: everything that
        /// changes YOU first, <see cref="ToyCategory.World"/> last. Pure - nothing is applied.
        /// </summary>
        public static List<Pick> Plan(System.Random rng = null)
        {
            rng ??= new System.Random();
            var picks = new List<Pick>();
            var surfaces = ToyShellRegistry.Surfaces;

            for (int i = 0; i < surfaces.Count; i++)
            {
                var surface = surfaces[i];
                if (surface == null || !surface.ShellAvailable) continue;

                var option = Descend(surface, rng);
                if (option == null) continue;

                var definition = surface.ShellDefinition;
                picks.Add(new Pick(surface, option,
                    definition ? definition.DisplayName : "",
                    definition ? definition.Category : ToyCategory.Pilot));
            }

            // A stable sort would do, but List.Sort is not stable - so the key carries the
            // original index and the order among equal categories is the registry's.
            var ordered = new List<Pick>(picks.Count);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < picks.Count; i++)
                {
                    bool world = picks[i].Category == ToyCategory.World;
                    if ((pass == 1) == world) ordered.Add(picks[i]);
                }
            return ordered;
        }

        /// <summary>
        /// Walk one random path down a toy's option tree and return the leaf, or null when the toy
        /// offers no setting to change.
        ///
        /// <para>A random DESCENT rather than an enumeration of every leaf: the Lifeform Matrix is
        /// three layers deep (kingdom, species, element), so enumerating it would expand every
        /// species of every kingdom to make one choice, where a descent expands one branch per
        /// layer. "Pick a random thing from this toy" is also what a descent actually means.</para>
        /// </summary>
        static ToyShellOption Descend(IToyShellSurface surface, System.Random rng)
        {
            _layer.Clear();
            try
            {
                surface.BuildShellOptions(_layer);
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[ToyShuffle] A toy threw while building its shell options, so it " +
                                 $"is not in this shuffle: {e}");
                return null;
            }

            for (int depth = 0; depth < MaxDescent; depth++)
            {
                // Rows that can lead somewhere: a branch to walk into, or a setting to apply. A
                // row with no Apply is there to be READ (the hull you fly, the domain you wear),
                // and an activity belongs to the other button.
                _eligible.Clear();
                for (int i = 0; i < _layer.Count; i++)
                {
                    var row = _layer[i];
                    if (row == null) continue;
                    if (row.IsBranch) { _eligible.Add(i); continue; }
                    if (row.Apply == null || row.RequiresFreestyle) continue;
                    _eligible.Add(i);
                }
                if (_eligible.Count == 0) return null;

                // Prefer a leaf that is NOT where the player already is - "a random NEW cell".
                // The cell selector DOES offer the current world (flying it is the freestyle
                // reset), so without this a shuffle could legitimately land on the cell you are
                // in and read as having done nothing.
                int chosen = ChooseIndex(rng, preferNotCurrent: true);
                var option = _layer[chosen];

                if (!option.IsBranch) return option;

                List<ToyShellOption> next;
                try
                {
                    next = option.Expand();
                }
                catch (Exception e)
                {
                    CSDebug.LogError($"[ToyShuffle] '{option.Label}' threw while expanding: {e}");
                    return null;
                }
                if (next is not { Count: > 0 }) return null;

                _layer.Clear();
                _layer.AddRange(next);
            }

            return null;
        }

        static int ChooseIndex(System.Random rng, bool preferNotCurrent)
        {
            if (preferNotCurrent)
            {
                int fresh = 0;
                for (int i = 0; i < _eligible.Count; i++)
                    if (!_layer[_eligible[i]].IsCurrent) fresh++;

                if (fresh > 0)
                {
                    int target = rng.Next(fresh);
                    for (int i = 0; i < _eligible.Count; i++)
                    {
                        if (_layer[_eligible[i]].IsCurrent) continue;
                        if (target-- == 0) return _eligible[i];
                    }
                }
            }
            return _eligible[rng.Next(_eligible.Count)];
        }

        /// <summary>
        /// Apply a plan. Returns how many picks were applied.
        ///
        /// <para>Awaits a beat before the <see cref="ToyCategory.World"/> picks so a hull respawn
        /// has settled before a veiled cell build begins - both toys refuse a second pass while
        /// their own swap is in flight, so the beat buys the READ (you see your hull change, then
        /// the world change) rather than correctness.</para>
        /// </summary>
        public static async UniTask<int> ApplyAsync(List<Pick> picks, CancellationToken ct)
        {
            if (picks == null) return 0;

            int applied = 0;
            bool delayed = false;

            for (int i = 0; i < picks.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                var pick = picks[i];
                if (!pick.IsValid) continue;

                if (pick.Category == ToyCategory.World && !delayed && applied > 0)
                {
                    delayed = true;
                    await UniTask.Delay(WorldDelayMs, ignoreTimeScale: true, cancellationToken: ct);
                    if (ct.IsCancellationRequested) break;
                }

                try
                {
                    pick.Option.Apply();
                    applied++;
                    CSDebug.LogVerbose(CSLogChannel.ToyBox,
                        $"[ToyShuffle] {pick.ToyName} -> {pick.Option.Label}.");
                }
                catch (Exception e)
                {
                    // One toy that throws must not cost the rest of the shuffle - the same reason
                    // ImpactorBase.RunEffectIsolated exists.
                    CSDebug.LogError($"[ToyShuffle] '{pick.ToyName} -> {pick.Option.Label}' threw; " +
                                     $"the rest of the shuffle still ran: {e}");
                }
            }

            return applied;
        }

        /// <summary>A one-line description of what a plan will do, for the button's own label.</summary>
        public static string Describe(List<Pick> picks)
        {
            if (picks is not { Count: > 0 }) return "";
            var parts = new List<string>(picks.Count);
            for (int i = 0; i < picks.Count; i++)
                if (picks[i].IsValid) parts.Add(picks[i].Option.Label);
            return string.Join(", ", parts);
        }
    }
}
