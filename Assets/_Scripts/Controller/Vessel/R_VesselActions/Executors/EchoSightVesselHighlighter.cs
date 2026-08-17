using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>Charge level-5</b> half of the Dolphin's Echo Sight: while the trigger is held, every
    /// VESSEL standing inside the next crystal blast's volume brightens in its own domain colour.
    ///
    /// <para><b>Why brightening, not tinting.</b> A vessel already wears its domain — that is what
    /// <c>ShipHelper.SetShipProperties</c> paints. So "highlight it in its domain colour" is
    /// literally a gain on the colours it is already wearing: this drives <c>_ColorMultiplier</c>,
    /// the brightness lever <c>VesselGraph.shadergraph</c> exposes and <c>VesselAnimation</c>
    /// already uses for its boost glow. Nothing is recoloured, so the upgrade can never make a Ruby
    /// pilot read as Jade — it makes them read as LIT, and which team they are stays their own
    /// palette's job (<c>Docs/PALETTE.md</c>).</para>
    ///
    /// <para><b>Why per-vessel CPU is fine here and would not be on prisms.</b> The prism half of
    /// this sight is a global uniform precisely because there are tens of thousands of prisms
    /// (<c>Docs/PRISM_ANIMATION.md</c> §4.7). There are at most a dozen vessels, they are already
    /// individually simulated, and this runs only while a trigger is held — so a
    /// MaterialPropertyBlock per renderer is the ordinary tool, not a law violation. The material
    /// is never cloned (<c>renderer.material</c> is never touched) and the block is written per
    /// material INDEX, so an engine material that authored its own multiplier is restored exactly
    /// when the highlight lets go.</para>
    ///
    /// <para><b>Continuity of existence applies.</b> Nothing pops: a vessel entering the volume
    /// blooms up over <see cref="_fadeSeconds"/> and a vessel leaving it fades back down, so
    /// sweeping the nose across a target reads as a beam passing over it.</para>
    ///
    /// Owned by <see cref="EchoSightActionExecutor"/> — a plain C# object with the executor's
    /// lifetime, so it holds no statics and can never retain a destroyed vessel across a scene load.
    /// </summary>
    public sealed class EchoSightVesselHighlighter
    {
        static readonly int ColorMultiplierId = Shader.PropertyToID("_ColorMultiplier");

        /// <summary>One renderer sub-mesh we are allowed to brighten, and the value it rests at.</summary>
        readonly struct Target
        {
            public readonly Renderer Renderer;
            public readonly int MaterialIndex;
            public readonly float RestMultiplier;

            public Target(Renderer renderer, int materialIndex, float restMultiplier)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
                RestMultiplier = restMultiplier;
            }
        }

        sealed class Entry
        {
            public IVessel Vessel;
            public readonly List<Target> Targets = new();
            public float Blend;          // 0 = resting, 1 = fully lit
            public bool SeenThisFrame;
            public bool BlockApplied;    // so a released highlight clears exactly once
        }

        readonly Dictionary<int, Entry> _entries = new();
        readonly List<int> _scratchRemove = new();

        // Vessels already reported as un-lightable. Survives the entry being retired and rebuilt as
        // a target flies in and out of the cone, so a mis-painted hull is named ONCE, not on every
        // pass of the beam.
        readonly HashSet<int> _warned = new();
        MaterialPropertyBlock _block;

        readonly float _fadeSeconds;
        readonly float _gain;

        /// <param name="fadeSeconds">Seconds a vessel takes to bloom in / fade out of the highlight.</param>
        /// <param name="gain">Multiplier applied to a vessel's resting brightness at full highlight.</param>
        public EchoSightVesselHighlighter(float fadeSeconds, float gain)
        {
            _fadeSeconds = Mathf.Max(0.01f, fadeSeconds);
            _gain = Mathf.Max(1f, gain);
        }

        /// <summary>
        /// Drive one frame of the highlight. <paramref name="strength01"/> is the sight's own fade,
        /// so the vessel highlight and the prism highlight come up together.
        ///
        /// Pass <c>players == null</c> or an invalid volume to fade everything back down — the fade
        /// still runs, which is what keeps a released trigger from snapping the world dark.
        /// </summary>
        public void Tick(IReadOnlyList<IPlayer> players, IVessel self, in BlastVolume volume,
                         float strength01, float deltaTime)
        {
            bool live = players != null && volume.IsValid && strength01 > 0.001f;

            foreach (var kv in _entries) kv.Value.SeenThisFrame = false;

            if (live)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var vessel = players[i]?.Vessel;
                    if (vessel == null || !vessel.Transform) continue;

                    // The apex sits on our own hull, so we are trivially inside our own blast.
                    // Lighting the ship the pilot is flying tells them nothing.
                    if (ReferenceEquals(vessel, self)) continue;

                    if (!volume.Contains(vessel.Transform.position, out float fill)) continue;

                    var entry = Resolve(vessel);
                    entry.SeenThisFrame = true;
                    entry.Blend = Mathf.MoveTowards(entry.Blend, fill * strength01, deltaTime / _fadeSeconds);
                }
            }

            _scratchRemove.Clear();
            foreach (var kv in _entries)
            {
                var entry = kv.Value;

                if (!entry.SeenThisFrame)
                    entry.Blend = Mathf.MoveTowards(entry.Blend, 0f, deltaTime / _fadeSeconds);

                // A vessel destroyed mid-highlight takes its renderers with it; drop the entry
                // rather than writing into a dead reference.
                if (entry.Vessel == null || !entry.Vessel.Transform)
                {
                    _scratchRemove.Add(kv.Key);
                    continue;
                }

                Apply(entry);

                // Fully faded AND already restored: nothing left to drive.
                if (entry.Blend <= 0f && !entry.BlockApplied)
                    _scratchRemove.Add(kv.Key);
            }

            for (int i = 0; i < _scratchRemove.Count; i++)
                _entries.Remove(_scratchRemove[i]);
        }

        /// <summary>
        /// Drop every highlight IMMEDIATELY, restoring each renderer's authored brightness. Called
        /// on release-to-zero, on disable and on a vessel swap — a faded highlight left behind would
        /// otherwise be a permanently over-bright vessel with nothing in the game to restore it.
        /// </summary>
        public void ClearAll()
        {
            foreach (var kv in _entries)
            {
                var entry = kv.Value;
                entry.Blend = 0f;
                if (entry.Vessel != null && entry.Vessel.Transform) Restore(entry);
            }
            _entries.Clear();
        }

        // ---------------- Internals ----------------

        Entry Resolve(IVessel vessel)
        {
            int id = vessel.Transform.GetInstanceID();
            if (_entries.TryGetValue(id, out var existing)) return existing;

            var entry = new Entry { Vessel = vessel };
            CollectTargets(vessel, entry.Targets);

            _entries[id] = entry;

            if (entry.Targets.Count == 0 && _warned.Add(id))
                CSDebug.LogWarning(
                    $"[EchoSightVesselHighlighter] '{vessel.Transform.name}' has no renderer material " +
                    "exposing _ColorMultiplier, so the Charge-5 pilot highlight cannot light it. Its " +
                    "hull is not painted with a VesselGraph material - check ShipHelper.ApplyShipMaterial " +
                    "reached it.");

            return entry;
        }

        static void CollectTargets(IVessel vessel, List<Target> into)
        {
            var renderers = vessel.Transform.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (!renderer) continue;

                // sharedMaterials, never .materials — reading the live instances the vessel is
                // already drawing with, without cloning a single one of them.
                var materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    var material = materials[m];
                    if (!material || !material.HasFloat(ColorMultiplierId)) continue;
                    into.Add(new Target(renderer, m, material.GetFloat(ColorMultiplierId)));
                }
            }
        }

        void Apply(Entry entry)
        {
            if (entry.Blend <= 0f)
            {
                if (entry.BlockApplied) Restore(entry);
                return;
            }

            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < entry.Targets.Count; i++)
            {
                var target = entry.Targets[i];
                if (!target.Renderer) continue;

                // Lerp from the material's OWN authored brightness, so an engine that already sits
                // at 5 brightens from 5 and a hull that sits at 1 brightens from 1 — the highlight
                // is a gain on what the vessel looks like, not a value it is forced to.
                float value = Mathf.LerpUnclamped(target.RestMultiplier,
                                                  target.RestMultiplier * _gain, entry.Blend);

                target.Renderer.GetPropertyBlock(_block, target.MaterialIndex);
                _block.SetFloat(ColorMultiplierId, value);
                target.Renderer.SetPropertyBlock(_block, target.MaterialIndex);
            }

            entry.BlockApplied = true;
        }

        void Restore(Entry entry)
        {
            for (int i = 0; i < entry.Targets.Count; i++)
            {
                var target = entry.Targets[i];
                if (!target.Renderer) continue;
                target.Renderer.SetPropertyBlock(null, target.MaterialIndex);
            }
            entry.BlockApplied = false;
        }
    }
}
