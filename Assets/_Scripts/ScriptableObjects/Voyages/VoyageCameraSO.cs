using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Voyages' chase camera (Rev 114 F1, Shombith 2026-11-16: "camera chase is fine build them in the game"): in a voyage
    /// the flight camera sits close behind the pilot's vessel and low, a little to one side, so the Ark reads ahead of
    /// it. One framing for every vessel, and a row per vessel class where a hull needs its own.
    ///
    /// <para>The framing multiplies the vessel's own camera settings (<see cref="CameraSettingsSO"/>) at the point of use
    /// (<see cref="CustomCameraController.Mode"/>); it never edits them and never touches what a vessel does, because a
    /// game mode never changes the vessel. Applied by <c>VoyageChaseCamera</c> while a voyage runs.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VoyageCamera", menuName = "ScriptableObjects/Voyages/Voyage Camera")]
    public sealed class VoyageCameraSO : ScriptableObject
    {
        /// <summary>Under Resources.</summary>
        public const string ResourcePath = "Voyages/VoyageCamera";

        [Serializable]
        public struct VesselRow
        {
            [Tooltip("The vessel class this row frames.")]
            public VesselClassType Vessel;
            [Tooltip("The framing for that class, in place of the default.")]
            public CustomCameraController.ModeFraming Framing;
        }

        [Header("Chase camera")]
        [Tooltip("The framing for every vessel without a row of its own: below 1 distance is closer, below 1 height is lower.")]
        [SerializeField] CustomCameraController.ModeFraming defaultFraming = new() { Distance = 0.75f, Height = 0.55f, Side = 0.12f };
        [Tooltip("A vessel class that needs its own framing (a long hull, a wide one).")]
        [SerializeField] List<VesselRow> vessels = new();

        public CustomCameraController.ModeFraming DefaultFraming => defaultFraming;
        public IReadOnlyList<VesselRow> Vessels => vessels;

        /// <summary>The framing for a vessel class: its own row if it has one, else the default.</summary>
        public CustomCameraController.ModeFraming For(VesselClassType vessel)
        {
            foreach (var row in vessels)
                if (row.Vessel == vessel) return row.Framing;
            return defaultFraming;
        }

        /// <summary>For tests and tools: the same lookup on a list built in code.</summary>
        public void Set(CustomCameraController.ModeFraming fallback, IEnumerable<VesselRow> rows)
        {
            defaultFraming = fallback;
            vessels = rows != null ? new List<VesselRow>(rows) : new List<VesselRow>();
        }

        static VoyageCameraSO s_loaded;

        public static VoyageCameraSO Load()
        {
            if (!s_loaded) s_loaded = Resources.Load<VoyageCameraSO>(ResourcePath);
            return s_loaded;
        }
    }
}
