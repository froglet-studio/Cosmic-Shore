using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The element charger: <b>the four elements</b>, in the Toy Box. Charge one and your vessel's
    /// level in that element rises.
    ///
    /// <para>The grant is a CRYSTAL's grant: a raise of the vessel's persistent base level, in whole
    /// petals, through <see cref="ResourceSystem.GrantPetals"/> - the base-level write every crystal
    /// pickup and every steal's receiving half ends in.
    /// So the HUD flowers bloom, a level-5 upgrade unlocks and the hull morphs through their own
    /// subscriptions, and the maintained-mechanism law holds with nothing added here - a base
    /// raised past 10 is overcharge, and the resource system bleeds it back to 10.</para>
    ///
    /// <para>Elements are told apart by SHAPE, never by colour (colour belongs to domains): every
    /// crystal preview wears the toy's one accent material.</para>
    /// </summary>
    public sealed class ElementChargerToy : MatrixToy
    {
        /// <summary>The level range <see cref="ResourceSystem.GetLevel"/> reports (normalized
        /// -0.5..1.5 × 10).</summary>
        public const int MinLevel = -5;
        public const int MaxLevel = 15;

        /// <summary>
        /// The four elements in the order a HUD reads them left to right - the same order as
        /// <c>VesselHUDView.AbilityDisplayOrder</c> and the element flowers above it.
        /// </summary>
        static readonly Element[] Elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        /// <summary>The four elements the toy offers, in HUD order (read by the encyclopedia).</summary>
        public static IReadOnlyList<Element> MatrixElements => Elements;

        ElementChargerToyDefinitionSO _def;

        public void Configure(ElementChargerToyDefinitionSO definition) => _def = definition;

        int LevelsPerPass => _def ? _def.LevelsPerPass : 5;

        // ── The pure part (edit-mode tested) ─────────────────────────────────

        /// <summary>
        /// The integer level a pass would leave <paramref name="currentLevel"/> at - clamped to the
        /// range the resource system clamps to, so a crystal at the top of the band honestly reads
        /// as doing nothing more.
        /// </summary>
        public static int ProjectedLevel(int currentLevel, int levelsPerPass)
            => Mathf.Clamp(currentLevel + Mathf.Max(0, levelsPerPass), MinLevel, MaxLevel);

        // ── The grant ────────────────────────────────────────────────────────

        /// <summary>
        /// Raise the local vessel's base level in <paramref name="element"/> by one pass. Returns
        /// false (and changes nothing) when there is no local vessel to charge - mid vessel-swap,
        /// say.
        /// </summary>
        bool Grant(Element element)
        {
            var vessel = ResolveLocalVessel();
            var resources = vessel?.ResourceSystem;
            if (!resources)
            {
                CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[ElementCharger] No local vessel to charge {element}.");
                return false;
            }

            int before = resources.GetLevel(element);
            // Whole petals, through the same door a steal's receiving half uses: one petal is one
            // integer level, one flower step and one crystal at world scale 1.
            resources.GrantPetals(element, LevelsPerPass);
            CSDebug.LogVerbose(CSLogChannel.ToyBox,
                $"[ElementCharger] {element} {before} -> {resources.GetLevel(element)}.");
            return true;
        }

        // ── The one declaration ──────────────────────────────────────────────

        // Mid vessel-swap there is no hull to charge; the card greys out rather than offering a
        // grant that would land on nothing.
        protected override bool ShellAvailable
        {
            get
            {
                var resources = ResolveLocalVessel()?.ResourceSystem;
                return resources;   // UnityEngine.Object's bool: false when missing or destroyed
            }
        }

        /// <summary>
        /// The four elements, each row naming the level you are at and the one a charge leaves you
        /// at. It does not need freestyle: element levels live on the vessel, so a charge applied
        /// from the menu is still there when you take the stick.
        /// </summary>
        protected override void BuildOptions(List<ToyShellOption> into)
        {
            var resources = ResolveLocalVessel()?.ResourceSystem;
            Color accent = Definition ? Definition.AccentColor : Color.white;

            foreach (var element in Elements)
            {
                var captured = element;
                string detail = "";
                if (resources)
                {
                    int current = resources.GetLevel(element);
                    int next = ProjectedLevel(current, LevelsPerPass);
                    detail = next > current ? $"level {current} -> {next}" : $"level {current} (full)";
                }

                into.Add(new ToyShellOption
                {
                    Label = element.ToString(),
                    Detail = detail,
                    Accent = accent,
                    CommitVerb = "Charge",
                    Payload = captured,
                    Apply = () => Grant(captured),
                    BuildPreview = parent => BuildShellPreview(captured, parent),
                });
            }
        }

        const float PreviewRadius = 20f;

        GameObject BuildShellPreview(Element element, Transform parent)
        {
            if (!parent) return null;
            Color accent = Definition ? Definition.AccentColor : Color.white;
            if (!ElementCrystalModelBuilder.TryBuild(element, PreviewRadius,
                    ToyFactory.AccentMaterial(accent), out var model))
                return null;

            model.transform.SetParent(parent, false);
            return model;
        }
    }
}
