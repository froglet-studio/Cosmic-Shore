using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>Element Charger</b> - <b>one toy that opens into the four elements</b>. Fly the
    /// station and a row of four element crystals blooms out ahead, charge → mass → space → time
    /// left to right as you approach (the same order as the HUD's flowers and ability row); fly a
    /// crystal and your vessel's level in that element rises by <see cref="LevelsPerPass"/>.
    ///
    /// <para>The grant is exactly what collecting an elemental crystal does - a raise of the
    /// vessel's persistent BASE level through <see cref="ResourceSystem.AdjustLevel"/> - so every
    /// consumer (HUD flowers, level-5 ability upgrades, hull morphs) reacts through its ordinary
    /// subscription with nothing wired for this toy. It inherits the maintained-mechanism law for
    /// free: a base raised past level 10 is overcharge, and <c>RecoverBaseLevels</c> bleeds it back
    /// down to 10, so a charge into the 10..15 band is felt and then drains like any crystal's.</para>
    ///
    /// <para>Toy-faithful: no score, no end condition, no timer. Element levels are simulated on the
    /// OWNING machine and never replicate, and a toy only ever fires for the local pilot - so the
    /// grant lands on the one machine that owns the number, with no networking of its own.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_ElementCharger", menuName = "ScriptableObjects/Toys/Element Charger Toy")]
    public class ElementChargerToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Grant")]
        [SerializeField, Range(1, 20), Tooltip("Integer element levels one pass through a crystal adds. " +
                                               "5 is one pass from rest to the level-5 ability upgrade, and " +
                                               "two to the sustained ceiling (10). Anything past 10 is " +
                                               "overcharge and drains back to 10, exactly as a crystal's does.")]
        int levelsPerPass = 5;

        [Header("Layout")]
        [SerializeField, Min(10f), Tooltip("Spacing between crystals in the row.")]
        float stationSpacing = 60f;

        [SerializeField, Min(0.5f), Tooltip("How far out the row blooms, in multiples of Station Spacing, " +
                                            "measured from the toy along the outward radial (away from the " +
                                            "cell centre). You fly AT the toy and keep going, so this is the " +
                                            "gap you cross before the choices.")]
        float matrixDistanceFactor = 3f;

        public int LevelsPerPass => Mathf.Max(1, levelsPerPass);
        public float StationSpacing => stationSpacing;
        public float MatrixDistanceFactor => matrixDistanceFactor;

        /// <summary>Your vessel's elements. It changes what YOUR hull can do - the world, your domain
        /// and your hull are exactly where you left them.</summary>
        public override ToyCategory Category => ToyCategory.Pilot;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor);
            var toy = go.AddComponent<ElementChargerToy>();
            toy.Configure(this);
            toy.Initialize(this, context, placement);
        }
    }
}
