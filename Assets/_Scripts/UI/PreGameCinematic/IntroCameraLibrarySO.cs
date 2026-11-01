using System.Collections.Generic;
using CosmicShore.Data;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Every mode's pre-game intro. Modes are grouped into FAMILIES by arena kind (gate races,
    /// the Boneyard, forests, cages, ...); a match plays one of its family's intros at random,
    /// never the same one twice in a row, and shows its own mode's words over it.
    ///
    /// Loaded from <c>Resources/IntroCameraLibrary</c> by <see cref="PreGameCinematicController"/>.
    /// Authored by <c>Tools/Build/author_intro_cameras.py</c> (<c>--check</c> is the drift gate)
    /// from the Intro Cam Lab artifact; Docs/IntroCameras/README.md. A mode in no family plays
    /// the old orbit.
    /// </summary>
    [CreateAssetMenu(fileName = "IntroCameraLibrary", menuName = "ScriptableObjects/UI/Intro Camera Library")]
    public class IntroCameraLibrarySO : ScriptableObject
    {
        public const string ResourcePath = "IntroCameraLibrary";

        [Header("Families")]
        [SerializeField] List<IntroFamily> families = new();

        [Header("Words (empty shows nothing)")]
        [SerializeField] List<IntroModeWords> modeWords = new();

        [Header("Scale")]
        [Tooltip("The cell radius the intros were designed in (the artifact's stand-in cell). Arena-space " +
                 "distances scale by the real cell's radius over this.")]
        [SerializeField] float referenceRadius = 240f;
        [Tooltip("Multiplies the arena scale for every intro: raise it if intros fly too close in every mode.")]
        [SerializeField] float scaleMultiplier = 1f;

        [Header("Look")]
        [Tooltip("Height of each letterbox bar as a fraction of the screen.")]
        [SerializeField, Range(0f, 0.2f)] float letterbox = 0.1222f;
        [Tooltip("Caption font: the title and the lower third's name. Empty uses TextMesh Pro's default.")]
        [SerializeField] TMP_FontAsset captionFont;
        [Tooltip("The smaller lines' font: the tagline, the objective and the lower third's line. Empty uses the " +
                 "caption font.")]
        [SerializeField] TMP_FontAsset detailFont;
        [SerializeField] float titleFontSize = 110f;
        [SerializeField] float taglineFontSize = 32f;
        [SerializeField] float objectiveFontSize = 24f;
        [SerializeField] float lowerTitleFontSize = 44f;
        [SerializeField] float lowerLineFontSize = 18f;

        public IReadOnlyList<IntroFamily> Families => families;
        public IReadOnlyList<IntroModeWords> ModeWords => modeWords;
        public float ReferenceRadius => referenceRadius;
        public float ScaleMultiplier => scaleMultiplier;
        public float Letterbox => letterbox;
        public TMP_FontAsset CaptionFont => captionFont;
        public TMP_FontAsset DetailFont => detailFont;
        public float TitleFontSize => titleFontSize;
        public float TaglineFontSize => taglineFontSize;
        public float ObjectiveFontSize => objectiveFontSize;
        public float LowerTitleFontSize => lowerTitleFontSize;
        public float LowerLineFontSize => lowerLineFontSize;

        // The last intro each family played this session, so a rematch never repeats it.
        static readonly Dictionary<string, int> LastPlayed = new();

        public static IntroCameraLibrarySO Load() => Resources.Load<IntroCameraLibrarySO>(ResourcePath);

        /// <summary>The family a mode belongs to, or null when it has none.</summary>
        public IntroFamily FamilyFor(GameModes mode)
        {
            for (int i = 0; i < families.Count; i++)
            {
                var f = families[i];
                if (f == null) continue;
                for (int m = 0; m < f.Modes.Count; m++)
                    if (f.Modes[m] == mode) return f;
            }
            return null;
        }

        /// <summary>The mode's words, or null when none are authored.</summary>
        public IntroModeWords WordsFor(GameModes mode)
        {
            for (int i = 0; i < modeWords.Count; i++)
                if (modeWords[i] != null && modeWords[i].Mode == mode) return modeWords[i];
            return null;
        }

        /// <summary>
        /// Picks one of the mode's family intros at random, avoiding the one that family played
        /// last. False when the mode has no family or the family has no playable intro.
        /// </summary>
        public bool TryPick(GameModes mode, out IntroSequence sequence)
        {
            sequence = null;
            var family = FamilyFor(mode);
            if (family == null || family.Sequences.Count == 0) return false;

            int previous = LastPlayed.TryGetValue(family.Name ?? string.Empty, out var p) ? p : -1;
            int index = PickIndex(family.Sequences.Count, previous, Random.value);
            sequence = family.Sequences[index];
            if (sequence == null || sequence.Shots.Count == 0) { sequence = null; return false; }

            LastPlayed[family.Name ?? string.Empty] = index;
            return true;
        }

        /// <summary>
        /// A uniform pick from <paramref name="count"/> items that is never <paramref name="previous"/>
        /// when there is any other choice. <paramref name="roll"/> is in [0, 1].
        /// </summary>
        public static int PickIndex(int count, int previous, float roll)
        {
            if (count <= 1) return 0;
            bool skip = previous >= 0 && previous < count;
            int choices = skip ? count - 1 : count;
            int i = Mathf.Min(choices - 1, Mathf.FloorToInt(Mathf.Clamp01(roll) * choices));
            if (skip && i >= previous) i++;
            return i;
        }
    }
}
