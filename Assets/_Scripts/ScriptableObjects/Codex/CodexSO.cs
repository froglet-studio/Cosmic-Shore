using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// One fact about a codex entry, as the UI renders it: a label and an already-formatted
    /// value string.
    ///
    /// <para>Formatted rather than typed on purpose. A codex row is prose, not a number - "Breeds
    /// every 40 prisms grown" reads and a float 40 does not, and a typed value would force the UI
    /// to carry a formatter per stat kind. The HARVESTER does the formatting once, in the editor,
    /// where the source asset is in hand.</para>
    /// </summary>
    [Serializable]
    public struct CodexStat
    {
        public string Label;
        public string Value;

        /// <summary>
        /// True when a human typed this row. <b>The harvester never touches an authored stat</b> -
        /// a rescan replaces the harvested rows and leaves these standing. This is the whole
        /// reason the flag exists: without it, re-running the scan after a designer adds a line
        /// of flavour silently deletes it.
        /// </summary>
        public bool Authored;

        public CodexStat(string label, string value, bool authored = false)
        {
            Label = label;
            Value = value;
            Authored = authored;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Label) && string.IsNullOrWhiteSpace(Value);
    }

    /// <summary>
    /// One variant WITHIN an entry - what the detail page shows when the player steps sideways.
    ///
    /// <para>For ecology that is the species' four elements (Gyroid Charge / Mass / Space / Time),
    /// each of which is a real authored config asset — and since Docs/ECOSYSTEM.md 40 that is the
    /// WHOLE variation a lifeform has, because there is no level. An ethirion has no variants at
    /// all: its size is not a property of the crystal but of the LIFEFORM carrying it, so it is
    /// stated on the flora and fauna entries instead. The type stays general because a future
    /// kingdom may key its variants on something that is not an element.</para>
    /// </summary>
    [Serializable]
    public class CodexVariant
    {
        [Tooltip("What the tab says - normally an element name.")]
        public string Label;

        [Tooltip("The element this variant is, when it is an element. Element.None for " +
                 "anything that is not element-keyed.")]
        public Element Element = Element.None;

        [Tooltip("The authored asset this variant was harvested from - a FloraConfigurationSO " +
                 "or FaunaConfigurationSO. Owned by the harvester; edit the source asset, not " +
                 "this field.")]
        public ScriptableObject SourceConfig;

        [Tooltip("The prefab this variant spawns. Owned by the harvester.")]
        public GameObject SourcePrefab;

        [Tooltip("Optional per-variant art. Empty is normal and is NOT a gap: an element-keyed " +
                 "variant resolves to that element's own ethirion image (see " +
                 "CodexSO.VariantImage), and a variant whose identity is a colour draws its " +
                 "AccentColor instead. Only a variant that is a distinct OBJECT - a painting, a " +
                 "hull, a world - bakes art of its own.")]
        public Sprite Image;

        [Tooltip("Accent for this variant. Alpha 0 means \"unset\". It is what a variant with no " +
                 "art is drawn as - a domain's colour IS that variant's identity, and baking a " +
                 "PNG of a flat colour would be silly.")]
        public Color AccentColor = new(0f, 0f, 0f, 0f);

        public List<CodexStat> Stats = new();

        /// <summary>The accent to draw with, resolving the alpha-0 "unset" sentinel.</summary>
        public Color ResolveAccent(Color fallback) => AccentColor.a <= 0f ? fallback : AccentColor;
    }

    /// <summary>
    /// One page of the encyclopedia: an ethirion, or a species of flora or fauna.
    ///
    /// <para><b>The field-ownership contract</b>, which is what makes a re-scan safe to run at any
    /// time (enforced by <c>CodexHarvester.Merge</c> in the editor assembly):</para>
    /// <list type="bullet">
    /// <item><b>Harvester-owned</b> - <see cref="Kingdom"/>, <see cref="Group"/>,
    /// <see cref="SourcePrefab"/>, <see cref="SourceConfig"/>, <see cref="Variants"/> wiring, and
    /// every <see cref="CodexStat"/> whose <c>Authored</c> flag is false. A re-scan rewrites these
    /// from the project.</item>
    /// <item><b>Filled only when empty</b> - <see cref="DisplayName"/>, <see cref="Tagline"/>,
    /// <see cref="Image"/>, <see cref="AccentColor"/>, <see cref="DiscoveryKey"/>. The harvester
    /// proposes; a human's value always wins.</item>
    /// <item><b>Never touched</b> - <see cref="Description"/>,
    /// <see cref="UnlockedByDefault"/>, <see cref="SortOrder"/>,
    /// the preview pose, and authored stats.</item>
    /// </list>
    /// <para>Set <see cref="LockAutoHarvest"/> to freeze an entry completely - the scan will skip
    /// it whole, which is how a fully hand-authored page (a lore entry with no asset behind it)
    /// survives.</para>
    /// </summary>
    [Serializable]
    public class CodexEntry
    {
        [Tooltip("Stable identity - the key the harvester matches on, and the key a save file or " +
                 "an analytics event would reference. Never re-key a shipped entry: rename the " +
                 "DisplayName instead. Convention: kingdom.slug, e.g. \"fauna.shark\".")]
        public string Id;

        public CodexKingdom Kingdom = CodexKingdom.Ethirion;

        [Tooltip("The name the player reads.")]
        public string DisplayName;

        [Tooltip("One line under the title. PROPOSED, never overwritten: the harvester fills it " +
                 "only when it is blank (a toy's own authored one-liner is exactly this line, " +
                 "already written for the player), and anything a human types here stands.")]
        [TextArea(1, 3)] public string Tagline;

        [Tooltip("The body copy. Authored - the harvester never writes this.")]
        [TextArea(4, 14)] public string Description;

        [Tooltip("Hero image, baked by the codex tool into Assets/_Graphics/Codex/.")]
        public Sprite Image;

        [Tooltip("The prefab a detail page can build a live, spinnable model from (via " +
                 "ToyModelBuilder - the same path the toybox stations use). Harvester-owned.")]
        public GameObject SourcePrefab;

        [Tooltip("The authored CONFIG asset behind this entry when there is no prefab - a " +
                 "ToyDefinitionSO for a toy. Harvester-owned. A toy has no prefab at all: it is " +
                 "built at runtime from its definition, so the definition is the asset that " +
                 "exists. An entry with NEITHER is the orphan case.")]
        public ScriptableObject SourceConfig;

        /// <summary>
        /// True when the scan can still find the asset this entry was harvested from. A prefab
        /// for a lifeform or an ethirion, a config for a tool - one question, asked once, because
        /// "is this an orphan?" being spelled out per caller is how a new kingdom gets reported as
        /// orphaned everywhere it is drawn.
        /// </summary>
        public bool HasSource => SourcePrefab || SourceConfig;

        [Tooltip("Accent for this page. Alpha 0 means \"unset\" and lets the harvester propose " +
                 "an element/kingdom colour; any authored colour is kept.")]
        public Color AccentColor = new(0f, 0f, 0f, 0f);

        [Header("Discovery (hook only - nothing gates on this yet)")]
        [Tooltip("Every entry ships unlocked today. The flag and key exist so progression can be " +
                 "added without a schema change or a UI rewrite - the same way ToyboxSO deferred " +
                 "its unlock state.")]
        public bool UnlockedByDefault = true;

        [Tooltip("The event that would unlock this entry when discovery ships (e.g. " +
                 "\"collect.ethirion.charge\"). Inert today.")]
        public string DiscoveryKey;

        [Header("Preview pose (drives the baked image)")]
        public float PreviewYaw = -28f;
        public float PreviewPitch = 16f;
        [Tooltip("1 = the subject exactly fills the frame; above 1 pushes the camera back. The " +
                 "fit is solved from the bounds' corners, so 1 really does mean edge to edge.")]
        [Min(1f)] public float PreviewPadding = 1.05f;

        [Tooltip("Bake the image as a flat unlit silhouette instead of with the source's own " +
                 "materials. Gameplay prism and crystal shaders read globals that do not exist " +
                 "outside a running frame, so some of them render as a black blob or as nothing " +
                 "at all; this is the escape hatch. The baker also flips it automatically when a " +
                 "render comes back essentially empty.")]
        public bool FlatSilhouette;

        [Header("Ordering and authoring")]
        [Tooltip("Sub-heading this entry files under WITHIN its kingdom - the tool categories " +
                 "(Pilot / World / Creation) today. Harvester-owned, and empty for a kingdom that " +
                 "does not divide, which is why the UI must treat an empty group as \"no " +
                 "sub-heading\" rather than as a group called nothing.")]
        public string Group;

        [Tooltip("Ascending. Ties fall back to DisplayName.")]
        public int SortOrder;

        [Tooltip("Freeze this entry - the scan skips it entirely, harvested stats included.")]
        public bool LockAutoHarvest;

        public List<CodexStat> Stats = new();
        public List<CodexVariant> Variants = new();

        /// <summary>The accent to draw with, resolving the alpha-0 "unset" sentinel.</summary>
        public Color ResolveAccent(Color fallback) => AccentColor.a <= 0f ? fallback : AccentColor;

        /// <summary>The variant matching <paramref name="element"/>, or null.</summary>
        public CodexVariant FindVariant(Element element)
        {
            for (int i = 0; i < Variants.Count; i++)
                if (Variants[i] != null && Variants[i].Element == element) return Variants[i];
            return null;
        }

        /// <summary>
        /// The art to draw for <paramref name="variant"/> - its own image when it has one,
        /// otherwise the entry's. Four elements of one species normally share a silhouette, so
        /// per-variant art is the exception.
        /// </summary>
        public Sprite ImageFor(CodexVariant variant) =>
            variant != null && variant.Image ? variant.Image : Image;
    }

    /// <summary>
    /// The encyclopedia - <b>Ethirions</b> (every crystal), <b>Ecology</b> (every lifeform) and
    /// <b>Toys</b> (every freestyle toy) as the in-game UI reads them.
    ///
    /// <para>ONE catalog asset at <c>Assets/Resources/Codex.asset</c>, so a UI screen needs no
    /// inspector wiring and no DI registration: <c>CodexSO.Load()</c> and draw. That matters
    /// because the codex is opened from more than one place (a menu screen, a toy, a pause panel)
    /// and a per-scene reference is a per-scene thing to forget.</para>
    ///
    /// <para>Authored through <b>FrogletTools &gt; Interface &gt; Codex</b>,
    /// which harvests entries from the project's own assets and merges them in under the
    /// field-ownership contract on <see cref="CodexEntry"/>. Hand-editing this asset in the
    /// inspector is supported and survives a re-scan; that is the point of the contract.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Codex", menuName = "ScriptableObjects/" + nameof(CodexSO))]
    public class CodexSO : ScriptableObject
    {
        public const string ResourcePath = "Codex";

        [Tooltip("Every crystal in the game. One entry per element family.")]
        [SerializeField] List<CodexEntry> ethirions = new();

        [Tooltip("Every lifeform in the game. One entry per SPECIES; its four elements are " +
                 "variants inside the entry.")]
        [SerializeField] List<CodexEntry> ecology = new();

        [Tooltip("Every TOY - the freestyle stations you fly into. One entry per toy; the " +
                 "choices it offers are variants inside the entry.")]
        // Renamed from "tools". The attribute is correct here for the reason it is usually WRONG:
        // the rename changed only the WORD, not what the value means, so carrying the old key
        // forward preserves exactly the right data instead of resurrecting a stale meaning. Unity
        // would otherwise drop every authored toy page on the next load, silently.
        [FormerlySerializedAs("tools")]
        [SerializeField] List<CodexEntry> toys = new();

        public List<CodexEntry> Ethirions => ethirions;
        public List<CodexEntry> Ecology => ecology;
        public List<CodexEntry> Toys => toys;

        /// <summary>Ethirions, ecology, then toys, in list order. Allocates - do not call per frame.</summary>
        public List<CodexEntry> AllEntries()
        {
            var all = new List<CodexEntry>(ethirions.Count + ecology.Count + toys.Count);
            all.AddRange(ethirions);
            all.AddRange(ecology);
            all.AddRange(toys);
            return all;
        }

        /// <summary>
        /// The entries of one kingdom. Routed through <see cref="ListFor"/> and then filtered,
        /// so it stays correct for the shared list (Flora and Fauna both live in
        /// <see cref="Ecology"/>) and for the single-kingdom ones alike - one implementation
        /// rather than a branch per list.
        /// </summary>
        public List<CodexEntry> EntriesOf(CodexKingdom kingdom)
        {
            var source = ListFor(kingdom);
            var result = new List<CodexEntry>(source.Count);
            for (int i = 0; i < source.Count; i++)
                if (source[i] != null && source[i].Kingdom == kingdom) result.Add(source[i]);
            return result;
        }

        /// <summary>The entry with this id, from any list, or null.</summary>
        public CodexEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return FindIn(ethirions, id) ?? FindIn(ecology, id) ?? FindIn(toys, id);
        }

        /// <summary>
        /// The art to draw for one variant, in priority order: its OWN image, then - for an
        /// element-keyed variant - that element's <b>ethirion</b> image, then the entry's.
        ///
        /// <para>The middle step is why this lives on the catalog rather than on
        /// <see cref="CodexEntry"/>: a species' four elements do not need four baked PNGs each,
        /// because the picture of "the Charge variant" is the Charge ethirion and that image is
        /// already baked once, on its own page. Resolving it at draw time instead of copying the
        /// sprite reference also means re-baking the ethirion updates every lifeform that drops
        /// one, with nothing to re-scan.</para>
        /// </summary>
        public Sprite VariantImage(CodexEntry entry, CodexVariant variant)
        {
            if (variant == null) return entry?.Image;
            if (variant.Image) return variant.Image;

            if (variant.Element != Element.None)
            {
                var ethirion = EthirionFor(variant.Element);
                if (ethirion != null && ethirion.Image) return ethirion.Image;
            }

            return entry?.Image;
        }

        /// <summary>The ethirion entry for an element, or null. Matched on the harvested id.</summary>
        public CodexEntry EthirionFor(Element element)
        {
            for (int i = 0; i < ethirions.Count; i++)
            {
                var candidate = ethirions[i];
                if (candidate == null) continue;
                if (string.Equals(candidate.DisplayName, element.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        static CodexEntry FindIn(List<CodexEntry> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].Id == id) return list[i];
            return null;
        }

        /// <summary>
        /// The list an entry of this kingdom belongs in. Editor and runtime both route through
        /// this so "which list?" is answered once - Flora and Fauna share the ecology list, and a
        /// second copy of that rule is a second place to get it wrong.
        /// </summary>
        public List<CodexEntry> ListFor(CodexKingdom kingdom) => kingdom switch
        {
            CodexKingdom.Ethirion => ethirions,
            CodexKingdom.Toy => toys,
            _ => ecology,
        };

        static CodexSO _cached;

        /// <summary>Loads (and caches) the project's codex from Resources.</summary>
        public static CodexSO Load()
        {
            if (_cached) return _cached;
            _cached = Resources.Load<CodexSO>(ResourcePath);
            return _cached;
        }
    }
}
