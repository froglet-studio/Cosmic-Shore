using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>Wander</b> toy: leave the cell and go wandering - <b>with an Ark</b> or <b>without
    /// one</b>. Fly it and two stations bloom out ahead (the Toy Box window shows the same two as
    /// cards); thread one and you are gone.
    ///
    /// <list type="bullet">
    /// <item><b>Without Ark</b> - the Wanderway: the cell reverts to its bare canvas and a belt of
    /// little worlds streams ahead of your flight path, with your trail a rolling tether and the
    /// way home riding its tail (<see cref="WanderwaySettingsSO"/>, <see cref="WanderwayRun"/>).</item>
    /// <item><b>With Ark</b> - the Arkway: a corridor of whole cells opens and an
    /// <see cref="Ark"/> in your domain sails it, with you sworn to its side
    /// (<see cref="ArkwaySettingsSO"/>, <see cref="ArkwayRun"/>).</item>
    /// </list>
    ///
    /// <para>They used to be two toys, and they were one idea told twice - both take you OUT of the
    /// cell into an endless run you come home from, both revert the cell to its bare canvas, and
    /// both end the same three ways. Collapsing them into one toy with two choices is the toybox
    /// saying that once, and the choice ("do you want company?") is the one thing that differs.
    /// Each choice keeps its own settings asset, so no tunable moved.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_Wander", menuName = "ScriptableObjects/Toys/Wander Toy")]
    public class WanderToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Wander - the two choices")]
        [SerializeField, Tooltip("Settings for WITHOUT ARK - the Wanderway belt of little worlds. " +
                                 "Empty = code defaults with no prism prefab (scenes carry only " +
                                 "crystals and lifeforms).")]
        WanderwaySettingsSO withoutArk;

        [SerializeField, Tooltip("Settings for WITH ARK - the Arkway voyage through a corridor of " +
                                 "cells. Empty = code defaults with no prism prefab, and an Ark " +
                                 "cannot exist without a hull, so the choice refuses to start.")]
        ArkwaySettingsSO withArk;

        [Header("Wander - the matrix")]
        [SerializeField, Min(20f), Tooltip("Centre-to-centre gap between the two stations, world units.")]
        float stationSpacing = 90f;

        [SerializeField, Min(0.5f), Tooltip("How far out the two stations bloom, in multiples of Station Spacing.")]
        float matrixDistanceFactor = 3f;

        /// <summary>Where you are, by taking you out of it: both choices hand the host CELL its bare
        /// canvas and put a world of their own ahead of you instead.</summary>
        public override ToyCategory Category => ToyCategory.World;

        public WanderwaySettingsSO WithoutArk => withoutArk;
        public ArkwaySettingsSO WithArk => withArk;
        public float StationSpacing => stationSpacing;
        public float MatrixDistanceFactor => matrixDistanceFactor;

        /// <summary>The build veil's label for a wander without an Ark.</summary>
        public const string WithoutArkVeilLabel = "WANDER";

        /// <summary>The build veil's label for a wander with an Ark.</summary>
        public const string WithArkVeilLabel = "WANDER WITH ARK";

        public ConveyorConfig BuildWithoutArkConfig() =>
            withoutArk ? withoutArk.BuildConfig(WithoutArkVeilLabel) : new ConveyorConfig { DisplayName = WithoutArkVeilLabel };

        public ArkwayConfig BuildWithArkConfig() =>
            withArk ? withArk.BuildConfig(WithArkVeilLabel) : new ArkwayConfig { DisplayName = WithArkVeilLabel };

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor);
            var toy = go.AddComponent<WanderToy>();
            toy.Configure(this);
            toy.Initialize(this, context, placement);
        }
    }
}
