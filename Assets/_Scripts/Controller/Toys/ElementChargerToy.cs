using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The element charger: <b>one toy that opens into the four elements</b>. Fly it and a row of
    /// four element crystals blooms out ahead; fly a crystal and your vessel's level in that
    /// element rises. Another pass through the toy folds the row away.
    ///
    /// <para>The grant is a CRYSTAL's grant: a raise of the vessel's persistent base level, in whole
    /// petals, through <see cref="ResourceSystem.GrantPetals"/> - the base-level write every crystal
    /// pickup and every steal's receiving half ends in.
    /// So the HUD flowers bloom, a level-5 upgrade unlocks and the hull morphs through their own
    /// subscriptions, and the maintained-mechanism law holds with nothing added here - a base
    /// raised past 10 is overcharge, and the resource system bleeds it back to 10.</para>
    ///
    /// <para>Unlike the vessel changer the row STAYS OPEN after a pass: charging is something you
    /// do several times in a row (rest → 5 → 10), and a row that closed behind every pass would
    /// make you fly the toy again between each one. The per-station cooldown on
    /// <see cref="ToyMatrixStation"/> is what stops one pass from charging twice.</para>
    ///
    /// <para>Elements are told apart by SHAPE, never by colour (colour belongs to domains): every
    /// crystal wears the toy's one accent material and each station's ring is Neutral.</para>
    /// </summary>
    public sealed class ElementChargerToy : MatrixToy, IToyShellSurface
    {
        /// <summary>The level range <see cref="ResourceSystem.GetLevel"/> reports (normalized
        /// -0.5..1.5 × 10).</summary>
        public const int MinLevel = -5;
        public const int MaxLevel = 15;

        const float PunchSeconds = 0.35f;
        const float PunchScale = 1.35f;

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

        /// <summary>
        /// Which element station <paramref name="index"/> of the row carries. The row is built along
        /// the toy's own +right, and the toy FACES the cell centre - so for a pilot flying out from
        /// the centre through the toy (the approach the matrix is laid for), the toy's +right is the
        /// pilot's LEFT and index 0 lands on the pilot's right. Reversing the index is what puts
        /// Charge on the left as you arrive, reading the way the HUD does.
        /// </summary>
        public static Element ElementAtStation(int index)
            => Elements[Mathf.Clamp(Elements.Length - 1 - index, 0, Elements.Length - 1)];

        // ── The toy's own emblem: the four crystals ──────────────────────────

        protected override void OnInitialized() => AttachEmblem(new EmblemSource(), 6f);

        /// <summary>
        /// Core-only: the four element crystal MODELS on a sub-ring, all sharing the emblem's one
        /// material - "these are the four things this toy gives you". There is nothing to orbit and
        /// nothing that changes, so no satellites and no live key.
        /// </summary>
        sealed class EmblemSource : ToyEmblem.IEmblemSource
        {
            public int SatelliteCount => 0;
            public bool UsesSharedMaterial => true;

            public bool TryBuildSlot(int slot, Transform holder, float radius, Material shared, out bool heavy)
            {
                heavy = false;
                if (slot != 0) return false;

                float ring = radius * 0.62f;
                float each = radius * 0.42f;
                bool any = false;
                for (int i = 0; i < Elements.Length; i++)
                {
                    // Built unparented, then parented - the model builder fits by world bounds.
                    if (!ElementCrystalModelBuilder.TryBuild(Elements[i], each, shared, out var model)) continue;
                    model.transform.SetParent(holder, false);
                    float a = i / (float)Elements.Length * Mathf.PI * 2f;
                    model.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ring;
                    any = true;
                }
                return any;
            }

            public bool TryGetLiveKey(out object key)
            {
                key = null;
                return false;
            }

            public bool TryGetLiveTint(out Color tint)
            {
                // Never: one shared material paints all four crystals, and tinting it per element
                // would make an element read as a colour.
                tint = default;
                return false;
            }
        }

        // ── Layout: one row, charge → time ───────────────────────────────────

        protected override int StationCount => Elements.Length;
        protected override float StationSpacing => _def ? _def.StationSpacing : 60f;
        protected override float StationRadius => Placement.BodyRadius > 0.01f ? Placement.BodyRadius : 20f;
        protected override float MatrixDistanceFactor => _def ? _def.MatrixDistanceFactor : 3f;

        /// <summary>The elements are an ORDERED set, so they sit on one line rather than a 2×2.</summary>
        protected override int MatrixColumns(int count) => count;

        protected override void BuildStation(int index, Transform parent, Vector3 position, float radius)
        {
            var element = ElementAtStation(index);
            var station = CreateStation(parent, position, element.ToString(), radius * 1.6f);

            var body = new GameObject("Body").transform;
            body.SetParent(station.transform, false);

            // The crystal IS the station: the element's canonical in-world shape, in the toy's one
            // accent material. A sphere stands in only when the crystal set is unavailable.
            if (ElementCrystalModelBuilder.TryBuild(element, radius,
                    ToyFactory.AccentMaterial(Definition.AccentColor), out var model))
                model.transform.SetParent(body, false);
            else
                ToyFactory.AddSphereBody(body, radius, Definition.AccentColor);

            station.OnVesselPassed = () =>
            {
                if (Grant(element))
                    Punch(body, this.GetCancellationTokenOnDestroy()).Forget();
            };
        }

        // ── The grant ────────────────────────────────────────────────────────

        /// <summary>
        /// Raise the local vessel's base level in <paramref name="element"/> by one pass. Returns
        /// false (and changes nothing) when there is no local vessel to charge - mid vessel-swap,
        /// say. Shared by the world station and the app shell, so there is one implementation of
        /// "charge this element".
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

        /// <summary>
        /// The station acknowledges the pass: its crystal swells and settles back. Never a
        /// scale-from-zero - the crystal must not vanish for the frames a regrow would take.
        /// </summary>
        static async UniTaskVoid Punch(Transform body, CancellationToken ct)
        {
            if (!body) return;
            float elapsed = 0f;
            while (elapsed < PunchSeconds)
            {
                if (!body) return;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / PunchSeconds);
                // Up and back down in one smooth arc: sin(pi t) peaks at the midpoint.
                body.localScale = Vector3.one * Mathf.Lerp(1f, PunchScale, Mathf.Sin(t * Mathf.PI));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            if (body) body.localScale = Vector3.one;
        }

        // ── App-shell face ───────────────────────────────────────────────────

        ToyDefinitionSO IToyShellSurface.ShellDefinition => Definition;

        // Mid vessel-swap there is no hull to charge; the card greys out rather than offering a
        // grant that would land on nothing.
        bool IToyShellSurface.ShellAvailable
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
        void IToyShellSurface.BuildShellOptions(List<ToyShellOption> into)
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
                    Apply = () => Grant(captured),
                    BuildPreview = parent => BuildShellPreview(captured, parent),
                });
            }
        }

        GameObject BuildShellPreview(Element element, Transform parent)
        {
            if (!parent) return null;
            Color accent = Definition ? Definition.AccentColor : Color.white;
            if (!ElementCrystalModelBuilder.TryBuild(element, StationRadius,
                    ToyFactory.AccentMaterial(accent), out var model))
                return null;

            model.transform.SetParent(parent, false);
            return model;
        }
    }
}
