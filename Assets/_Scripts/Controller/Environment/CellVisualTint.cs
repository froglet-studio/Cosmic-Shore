using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The colours a <see cref="CellVisualTint"/> paints the cell's own visuals in.</summary>
    [System.Serializable]
    public struct CellPalette
    {
        [Tooltip("The membrane capsules' bright colour (SpindleGraph _BrightColor). HDR: above 1 glows.")]
        [ColorUsage(true, true)] public Color MembraneBright;
        [Tooltip("The membrane capsules' dull colour (SpindleGraph _DullColor).")]
        [ColorUsage(true, true)] public Color MembraneDull;
        [Tooltip("The nucleus cage's edge colour (CageGraph _Edge_Color). Alpha is the cage's own transparency.")]
        [ColorUsage(true, true)] public Color NucleusEdge;
        [Tooltip("The nucleus cage's straight-line colour (CageGraph _Straight_Color).")]
        [ColorUsage(true, true)] public Color NucleusStraight;
        [Tooltip("The cytoplasm motes' colour (SnowGraph _Color).")]
        [ColorUsage(true, true)] public Color Cytoplasm;

        public static CellPalette Lerp(in CellPalette a, in CellPalette b, float t) => new()
        {
            MembraneBright = Color.LerpUnclamped(a.MembraneBright, b.MembraneBright, t),
            MembraneDull = Color.LerpUnclamped(a.MembraneDull, b.MembraneDull, t),
            NucleusEdge = Color.LerpUnclamped(a.NucleusEdge, b.NucleusEdge, t),
            NucleusStraight = Color.LerpUnclamped(a.NucleusStraight, b.NucleusStraight, t),
            Cytoplasm = Color.LerpUnclamped(a.Cytoplasm, b.Cytoplasm, t),
        };
    }

    /// <summary>
    /// A mode-side VISUAL on a <see cref="Cell"/>: eases the cell's OWN spawned membrane, nucleus and cytoplasm from the
    /// colours they wear to a <see cref="CellPalette"/>, optionally through a brief bloom at the moment it starts, and
    /// back. Tandava (Assets/_Scripts/Controller/Arcade/TANDAVA.md §5) changes the cell with each form the swarm takes.
    ///
    /// It recolours DRAWS, never assets: the membrane through a property block on its instanced draw
    /// (<see cref="CapsuleMembrane.SetColourOverride"/> - its SpindleMaterial is shared with every spindle), the nucleus
    /// through property blocks on its renderers, the cytoplasm through ONE per-cell material clone
    /// (<see cref="SnowChanger.SetColourOverride"/>). Nothing about the cell's gameplay - its boundary, its nucleus
    /// control radius, its phase - is read or written. Continuity of existence: a change always EASES over seconds.
    /// Idle (no transition running) it costs nothing: <c>Update</c> returns at once.
    /// </summary>
    public sealed class CellVisualTint : MonoBehaviour
    {
        static readonly int EdgeColorId = Shader.PropertyToID("_Edge_Color");
        static readonly int StraightColorId = Shader.PropertyToID("_Straight_Color");

        Cell _cell;
        CellPalette _base, _from, _to, _shown;
        bool _hasBase, _active, _overriding;
        float _t0, _seconds = 1f, _flash;
        MaterialPropertyBlock _block;

        /// <summary>The tint of <paramref name="cell"/>, added on first use.</summary>
        public static CellVisualTint For(Cell cell)
        {
            if (!cell) return null;
            if (!cell.TryGetComponent(out CellVisualTint tint)) tint = cell.gameObject.AddComponent<CellVisualTint>();
            tint._cell = cell;
            return tint;
        }

        /// <summary>The colours the cell's visuals are authored in - what <see cref="Restore"/> eases back to.</summary>
        public CellPalette BasePalette { get { CaptureBase(); return _base; } }

        /// <summary>Do the one-off work a tint needs (the cytoplasm's material clone, the base colours) NOW, so the first
        /// transition never pays it. Call it while nothing is happening yet (a mode's ready screen).</summary>
        public void Prepare()
        {
            CaptureBase();
            if (_cell && _cell.CytoplasmVisual) _cell.CytoplasmVisual.PrepareColourOverride();
        }

        /// <summary>
        /// Ease from what the cell shows now to <paramref name="target"/> over <paramref name="seconds"/>.
        /// <paramref name="flash"/> &gt; 0 blooms every colour by up to (1 + flash) early in the ease - the moment of the change.
        /// </summary>
        public void TransitionTo(in CellPalette target, float seconds, float flash)
        {
            CaptureBase();
            _from = _overriding ? _shown : _base;
            _to = target;
            _seconds = Mathf.Max(0.05f, seconds);
            _flash = Mathf.Max(0f, flash);
            _t0 = Time.time;
            _active = true;
            _overriding = true;
        }

        /// <summary>Ease back to the cell's own colours, then drop every override.</summary>
        public void Restore(float seconds) => TransitionTo(BasePalette, seconds, 0f);

        void CaptureBase()
        {
            if (_hasBase || !_cell) return;
            _base = new CellPalette
            {
                MembraneBright = Color.white, MembraneDull = Color.white,
                NucleusEdge = Color.white, NucleusStraight = Color.white, Cytoplasm = Color.white,
            };
            var membrane = _cell.MembraneVisual;
            if (membrane) membrane.TryGetMaterialColours(out _base.MembraneBright, out _base.MembraneDull);
            var nucleus = _cell.NucleusVisual;
            var nr = nucleus ? nucleus.GetComponentInChildren<Renderer>() : null;
            var nm = nr ? nr.sharedMaterial : null;
            if (nm)
            {
                if (nm.HasProperty(EdgeColorId)) _base.NucleusEdge = nm.GetColor(EdgeColorId);
                if (nm.HasProperty(StraightColorId)) _base.NucleusStraight = nm.GetColor(StraightColorId);
            }
            var cytoplasm = _cell.CytoplasmVisual;
            if (cytoplasm) cytoplasm.TryGetMaterialColour(out _base.Cytoplasm);
            _shown = _base;
            _hasBase = true;
        }

        void Update()
        {
            if (!_active || !_cell) return;
            float t = Mathf.Clamp01((Time.time - _t0) / _seconds);
            float eased = t * t * (3f - 2f * t);
            // the bloom peaks a fifth of the way in and is gone by the half: the change READS as a moment, then settles
            float bloom = _flash > 0f ? _flash * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 2f)) * (t < 0.2f ? t / 0.2f : 1f) : 0f;
            _shown = CellPalette.Lerp(_from, _to, eased);
            Apply(_shown, 1f + bloom);
            if (t >= 1f)
            {
                _active = false;
                if (SameAsBase(_to)) ClearOverrides();
            }
        }

        bool SameAsBase(in CellPalette p) =>
            p.MembraneBright == _base.MembraneBright && p.MembraneDull == _base.MembraneDull &&
            p.NucleusEdge == _base.NucleusEdge && p.NucleusStraight == _base.NucleusStraight && p.Cytoplasm == _base.Cytoplasm;

        void Apply(in CellPalette p, float boost)
        {
            var membrane = _cell.MembraneVisual;
            if (membrane) membrane.SetColourOverride(Boost(p.MembraneBright, boost), Boost(p.MembraneDull, boost));

            var nucleus = _cell.NucleusVisual;
            if (nucleus)
            {
                _block ??= new MaterialPropertyBlock();
                var renderers = nucleus.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].GetPropertyBlock(_block);
                    _block.SetColor(EdgeColorId, Boost(p.NucleusEdge, boost));
                    _block.SetColor(StraightColorId, Boost(p.NucleusStraight, boost));
                    renderers[i].SetPropertyBlock(_block);
                }
            }

            var cytoplasm = _cell.CytoplasmVisual;
            if (cytoplasm) cytoplasm.SetColourOverride(Boost(p.Cytoplasm, boost));
        }

        void ClearOverrides()
        {
            _overriding = false;
            var membrane = _cell.MembraneVisual;
            if (membrane) membrane.ClearColourOverride();
            var nucleus = _cell.NucleusVisual;
            if (nucleus)
            {
                var renderers = nucleus.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(null);
            }
            var cytoplasm = _cell.CytoplasmVisual;
            if (cytoplasm) cytoplasm.ClearColourOverride();
        }

        /// <summary>HDR brighten: the colour's RGB times <paramref name="k"/>, its alpha (a cage's transparency) kept.</summary>
        static Color Boost(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);
    }
}
