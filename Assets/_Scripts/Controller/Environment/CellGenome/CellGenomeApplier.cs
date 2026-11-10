using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Applies a <see cref="CellGenome"/> to one <see cref="Cell"/>'s visuals and puts them back on disable: the
    /// membrane's wobble (<see cref="CapsuleMembrane.ApplyWobble"/>) and tint, the cytoplasm's flow
    /// (<see cref="SnowChanger.SetFlow"/>) and tint. VISUAL ONLY: the cell's boundary, volume, spawns, phase and every
    /// LOCKED ecology rule are untouched (Docs/GENOME_CELL_GENOME.md). With no genome asset it derives one from the
    /// cell's name (<see cref="CellGenome.FromSeed"/>), so a scene-level applier gives every cell its own look.
    /// The cytoplasm is spawned by the cell later than Start (it waits for a crystal), so the cytoplasm genes are
    /// applied the first frame it exists; until then Update only checks one reference.
    /// </summary>
    public sealed class CellGenomeApplier : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The cell to dress. Empty: this object's Cell, or the nearest one above it.")]
        [SerializeField] Cell cell;

        [Header("Genome")]
        [Tooltip("The genome to apply. Empty: derived from the cell's name (FromSeed of its stable hash).")]
        [SerializeField] CellGenomeSO genome;

        [Header("Genes to apply")]
        [Tooltip("Apply the membrane wobble genes (switches the membrane to the generated loop).")]
        [SerializeField] bool applyWobble = true;
        [Tooltip("Apply the membrane and cytoplasm tints.")]
        [SerializeField] bool applyTints = true;
        [Tooltip("Point the cytoplasm along the genome's flow. Off where a mode sets its own flow (Serpent Time's race " +
                 "line), which this would replace.")]
        [SerializeField] bool applyFlow = true;

        CellGenome _genome;
        CapsuleMembrane _membrane;
        SnowChanger _cytoplasm;
        CellGenomeFlow _flow;
        CapsuleMembraneAnimationSO.BakeSignature _originalWobble;
        bool _membraneApplied, _cytoplasmApplied, _started;

        /// <summary>The genome this applier uses.</summary>
        public CellGenome Genome => _genome;

        void Start()
        {
            _started = true;
            Apply();
        }

        void OnEnable()
        {
            if (_started) Apply();
        }

        void Apply()
        {
            if (!cell && !TryGetComponent(out cell)) cell = GetComponentInParent<Cell>();
            if (!cell) return;
            _genome = genome ? genome.Genome : CellGenome.FromSeed(CellGenome.StableHash(cell.name));
            ApplyMembrane();
            ApplyCytoplasm();
        }

        void Update()
        {
            if (_cytoplasmApplied || !cell) return;
            ApplyCytoplasm();
        }

        void ApplyMembrane()
        {
            if (_membraneApplied) return;
            _membrane = cell.MembraneVisual;
            if (!_membrane) return;
            if (applyWobble)
            {
                _originalWobble = _membrane.CurrentSignature;
                _membrane.ApplyWobble(_genome.WobbleSeed, _genome.WobbleFrequency, _genome.WobbleAmplitude,
                    _genome.WobblePulseSpeed);
            }
            if (applyTints && _membrane.TryGetMaterialColours(out var bright, out var dull))
                _membrane.SetColourOverride(CellGenome.ShiftHsv(bright, _genome.MembraneBrightHsv),
                    CellGenome.ShiftHsv(dull, _genome.MembraneDullHsv));
            _membraneApplied = true;
        }

        void ApplyCytoplasm()
        {
            if (_cytoplasmApplied) return;
            _cytoplasm = cell.CytoplasmVisual;
            if (!_cytoplasm) return;
            if (applyTints && _cytoplasm.TryGetMaterialColour(out var colour))
                _cytoplasm.SetColourOverride(CellGenome.ShiftHsv(colour, _genome.CytoplasmHsv));
            if (applyFlow && _genome.FlowEnabled)
            {
                float radius = _membrane ? _membrane.Radius * _membrane.transform.lossyScale.x : 500f;
                Vector3 centre = _membrane ? _membrane.transform.position : cell.transform.position;
                _flow = new CellGenomeFlow(_genome, centre, radius);
                _cytoplasm.SetFlow(_flow);
            }
            _cytoplasmApplied = true;
        }

        void OnDisable()
        {
            if (_membraneApplied && _membrane)
            {
                if (applyWobble)
                    _membrane.ApplyWobble(_originalWobble.seed, _originalWobble.noiseFrequency,
                        _originalWobble.noiseAmplitude, _originalWobble.pulseSpeed);
                if (applyTints) _membrane.ClearColourOverride();
            }
            if (_cytoplasmApplied && _cytoplasm)
            {
                if (applyTints) _cytoplasm.ClearColourOverride();
                if (_flow != null) _cytoplasm.SetFlow(null);
            }
            _membraneApplied = _cytoplasmApplied = false;
            _flow = null;
        }
    }
}
