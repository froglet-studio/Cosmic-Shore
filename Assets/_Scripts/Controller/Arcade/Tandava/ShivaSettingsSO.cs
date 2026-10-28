using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One creature a form releases from his hands: a species config and how many of it.</summary>
    [Serializable]
    public struct ShivaFaunaSpawn
    {
        [Tooltip("The species' FaunaConfigurationSO (its prefab, element and network sync).")]
        public FaunaConfigurationSO Config;
        [Tooltip("How many of it come out of his hands when the form begins.")]
        [Min(0)] public int Count;
    }

    /// <summary>One flora species the regrown cell plants for a form, and how many.</summary>
    [Serializable]
    public struct ShivaFloraSpawn
    {
        [Tooltip("The species' FloraConfigurationSO (a gyroid / Schwarz colony grows itself out of each seed).")]
        public FloraConfigurationSO Config;
        [Tooltip("Seeds planted when the cell grows again for this form.")]
        [Min(0)] public int Count;
    }

    /// <summary>One of his three forms: its name, its swarm, its cell.</summary>
    [Serializable]
    public struct ShivaFormSpec
    {
        [Tooltip("The form's name on the HUD and in the toasts (the Still Shiva, Nataraja, the Devouring Mouth).")]
        public string DisplayName;
        [Tooltip("The toast when the form begins (after the first: when the cell has grown again). Empty: no toast.")]
        public string BeginLine;
        [Header("Serpents (the Tandava swarm config's scripted plans)")]
        [Tooltip("Many-headed serpents this form releases.")]
        [Min(0)] public int SerpentCount;
        [Tooltip("The serpent's body: its index in the swarm config's ScriptedPlans (author_shiva_assets.py writes it).")]
        public int SerpentPlanIndex;
        [Tooltip("The rear pose of its lunge (every head raised).")]
        public int RearPlanIndex;
        [Tooltip("The strike pose of its lunge (every head struck at the pilot).")]
        public int StrikePlanIndex;
        [Header("The rest of the swarm")]
        public List<ShivaFaunaSpawn> Fauna;
        [Header("The cell")]
        [Tooltip("The flora the cell grows for this form (planted when the wave has passed).")]
        public List<ShivaFloraSpawn> Flora;
        [Tooltip("Elemental crystals scattered through the cell when the form begins.")]
        [Min(0)] public int Crystals;
        [Tooltip("The cell's colours while this form lasts (CellVisualTint).")]
        public CellPalette Palette;
    }

    /// <summary>
    /// Tandava, redesigned as SHIVA'S CELL (Assets/_Scripts/Controller/Arcade/TANDAVA.md §0): every number the
    /// <see cref="ShivaController"/> reads. Authored by Tools/Build/author_shiva_assets.py from the lab's shipped table
    /// (Docs/Studios/ShivaLab/sim.js SHIPPED) - re-run it rather than editing the asset by hand.
    /// </summary>
    [CreateAssetMenu(fileName = "ShivaSettings", menuName = "ScriptableObjects/Arcade/Shiva Settings")]
    public class ShivaSettingsSO : ScriptableObject
    {
        [Header("The director (the circuit, the clocks, the shells, the clap)")]
        public ShivaDirectorSettings Director = new();

        [Header("His three forms")]
        public List<ShivaFormSpec> Forms = new();

        [Header("The serpents")]
        [Tooltip("Tandava's swarm config (its ScriptedPlans carry the many-headed serpents and their lunge poses).")]
        public SwarmFaunaConfigSO SwarmConfig;
        [Tooltip("The serpent species (its prefab is the Tandava swarm).")]
        public FaunaConfigurationSO SerpentSpecies;
        [Tooltip("Cruise scale while it holds its ring.")]
        public float GuardCruise = 1f;
        [Tooltip("Turn scale while it holds its ring.")]
        public float GuardTurn = 1.6f;
        [Tooltip("Cruise scale of the strike (TandavaDirectorSettings: Lunging 2.4).")]
        public float StrikeCruise = 2.4f;
        [Tooltip("Turn scale of the strike.")]
        public float StrikeTurn = 3f;
        [Tooltip("A pilot inside this draws a lunge (u).")]
        public float LungeRange = 380f;
        [Tooltip("Seconds between one serpent's lunges.")]
        public float LungeCooldown = 6f;
        [Tooltip("Seconds it rears before it strikes.")]
        public float RearSeconds = 0.6f;
        [Tooltip("The longest a strike lasts (s).")]
        public float StrikeSeconds = 2.5f;
        [Tooltip("How far round a ring's mouth it coils (x the mouth).")]
        public float GuardOrbit = 1.4f;
        [Tooltip("A client's serpent is nudged toward the server's past this gap (u).")]
        public float NudgeThreshold = 25f;
        [Tooltip("The share of the gap one nudge closes.")]
        public float NudgeFraction = 0.2f;
        [Tooltip("The most one nudge moves it (u).")]
        public float MaxNudge = 8f;

        [Header("The shield")]
        [Tooltip("The spacing of the danger prisms on each shell (u). The shell is a cage you can see him through.")]
        public float ShellSpacing = 70f;
        [Tooltip("A shell prism's size (u).")]
        public Vector3 ShellPrismScale = new(10f, 10f, 2f);
        [Tooltip("The domain the shell's danger prisms carry.")]
        public Domains ShellDomain = Domains.Ruby;
        [Tooltip("The prism spawn channel (the vessels' own; BoostRingBuilder.LayOne).")]
        public PrismEventChannelWithReturnSO PrismChannel;

        [Header("His figure (drawn, not laid: it moves every frame and nothing can touch it)")]
        [Tooltip("The prism mesh the figure is drawn with.")]
        public Mesh FigureMesh;
        [Tooltip("The prism material (cloned per colour, GPU instancing on; the asset is never written).")]
        public Material FigureMaterial;
        [Tooltip("The spacing of the figure's prisms (u). Smaller is denser.")]
        public float FigureSpacing = 14f;
        [ColorUsage(true, true)] public Color BodyBright = new(0.32f, 0.5f, 1.6f);
        [ColorUsage(true, true)] public Color BodyDark = new(0.02f, 0.04f, 0.3f);
        [ColorUsage(true, true)] public Color EyeColor = new(2.2f, 2.2f, 2.6f);
        [ColorUsage(true, true)] public Color ThirdEyeColor = new(2.6f, 1.6f, 0.4f);
        [ColorUsage(true, true)] public Color VasukiColor = new(0.15f, 0.9f, 0.6f);
        [ColorUsage(true, true)] public Color HairColor = new(0.12f, 0.1f, 0.32f);
        [ColorUsage(true, true)] public Color MoonColor = new(2f, 1.85f, 1.3f);

        [Header("The clap")]
        [Tooltip("Gold shards thrown out on the wave as it leaves him.")]
        public int WaveShards = 240;
        [Tooltip("How fast a stripped vessel's crystals are thrown (u/s).")]
        public float StripSpeed = 40f;
        [Tooltip("The flora's colours flash this much as the cell collapses.")]
        public float TransitionFlash = 0.6f;
        [Tooltip("Seconds the cell takes to change colour.")]
        public float TransitionSeconds = 2f;
        [Tooltip("Seconds the cell takes to get its own colours back at the end.")]
        public float RestoreSeconds = 3f;

        [Header("The mouth (third form)")]
        [Tooltip("The black hole's strength in his mouth (horizon = 2 u per strength, BlackHoleConfig).")]
        public float BlackHoleStrength = 30f;

        [Header("The end")]
        [Tooltip("Gold shards his figure comes apart into when the third shell breaks.")]
        public int WinShards = 500;
        public float BurstSpeed = 260f;
        public float BurstScale = 9f;
        public float FlashRadius = 1800f;
        [Range(0f, 1f)] public float FlashStrength = 1f;
        public float FlashSeconds = 4f;

        [Header("Pilots")]
        [Tooltip("A step longer than this x dt is a respawn or a hitch and threads nothing (u/s).")]
        public float MaxPlausibleSpeed = 1600f;

        [Header("Text (every word the player reads)")]
        [Tooltip("The toast when the match begins.")]
        public string StartLine = "Thread his rings. Break his shield.";
        [Tooltip("A shell cracks. {0} = the shells left.")]
        public string ShellLine = "A shell breaks. {0} to go.";
        [Tooltip("He claps.")]
        public string ClapLine = "Shiva claps.";
        [Tooltip("The swarm comes out of his hands.")]
        public string SwarmLine = "His swarm comes out of his hands.";
        [Tooltip("The squad won.")]
        public string WonLine = "The Tandava is broken. The cell is yours.";
        [Tooltip("The drum ran out. {0} = the form.")]
        public string LostLine = "The drum stops. His dance ends the cell.";
        [Tooltip("The goal row naming the form.")]
        public string FormLabel = "Form {0} of 3";
        [Tooltip("The goal row counting the rings.")]
        public string RingsLabel = "Rings";
        [Tooltip("The goal row showing the drum.")]
        public string DrumLabel = "Drum";
        [Tooltip("The goal row counting his shells.")]
        public string ShellsLabel = "Shells";
        [Tooltip("The goal row while the cell grows again.")]
        public string RegrowLabel = "The cell grows again";
    }
}
