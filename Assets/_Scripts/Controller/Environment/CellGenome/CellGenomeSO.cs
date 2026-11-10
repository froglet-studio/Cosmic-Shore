using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One cell's visual genome as an asset (Docs/GENOME_CELL_GENOME.md). Applied by <see cref="CellGenomeApplier"/>;
    /// nothing reads it for gameplay. Its packed form (<see cref="CellGenome.ToBase64"/>) is shown for copying into a
    /// search or a note.
    /// </summary>
    [CreateAssetMenu(fileName = "CellGenome", menuName = "ScriptableObjects/Environment/Cell Genome")]
    public sealed class CellGenomeSO : ScriptableObject
    {
        [Header("Genome")]
        [Tooltip("The genes: membrane wobble and tint, cytoplasm flow and tint, reserved ambient numbers.")]
        [SerializeField] CellGenome genome = CellGenome.FromSeed(0);

        [Header("Packed")]
        [Tooltip("The genome packed to 56 bytes as base64, rewritten from the genes whenever they change.")]
        [SerializeField] string packed;

        /// <summary>The genome as stored (quantised to its packed precision).</summary>
        public CellGenome Genome => genome.Quantised();

        public string Packed => genome.ToBase64();

        /// <summary>Replace the genes (a search writing a result, a test).</summary>
        public void Set(in CellGenome value)
        {
            genome = value.Quantised();
            packed = genome.ToBase64();
        }

        /// <summary>Replace the genes from a packed base64 string. False, changing nothing, when it does not parse.</summary>
        public bool TrySetPacked(string base64)
        {
            if (!CellGenome.TryFromBase64(base64, out var g)) return false;
            Set(g);
            return true;
        }

        void OnValidate() => packed = genome.ToBase64();
    }
}
