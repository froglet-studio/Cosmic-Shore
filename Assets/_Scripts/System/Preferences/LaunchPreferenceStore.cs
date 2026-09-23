using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The on-disk memory of every card's last launch (<see cref="LaunchPreference"/>), one
    /// record per game mode, shared by the arcade and arena grids because they are one modal
    /// pointed at two rosters.
    ///
    /// <para>Same shape as <see cref="FavoriteSystem"/>: a static, lazily-initialised store over
    /// <c>DataAccessor</c>, re-read once per play session. Local disk only - a launch setup is a
    /// convenience of THIS machine (the party it was made with, the unlocks it has), not a
    /// profile fact worth a cloud round trip, and the record is re-validated on every read
    /// anyway.</para>
    ///
    /// <para>It supersedes the vessel half of <see cref="LoadoutSystem.SaveGameLoadOut"/>'s "last
    /// game play configuration", which the one-panel launch flow never wrote (its only writer
    /// is the retired two-screen path's <c>PlaySelectedGame</c>). That read is kept as a
    /// fallback below this store so a machine with an old loadout file still opens on the hull
    /// it last flew.</para>
    /// </summary>
    public static class LaunchPreferenceStore
    {
        const string SaveFileName = "launch_preferences.data";

        static List<LaunchPreference> _records;
        static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _records = null;
            _initialized = false;
        }

        static void Init()
        {
            _records = DataAccessor.Load<List<LaunchPreference>>(SaveFileName) ?? new List<LaunchPreference>();
            _initialized = true;
        }

        /// <summary>The saved record for <paramref name="mode"/>, or false when nothing has been
        /// launched on that card from this machine.</summary>
        public static bool TryGet(GameModes mode, out LaunchPreference preference)
        {
            if (!_initialized) Init();

            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].GameMode != mode) continue;
                preference = _records[i];
                if (preference.AIDomains == null) preference.AIDomains = new List<Domains>();
                return true;
            }

            preference = LaunchPreference.Empty(mode);
            return false;
        }

        /// <summary>
        /// The launch authority just launched <paramref name="mode"/> with these terms. Writes
        /// both halves of the record.
        /// </summary>
        public static void SaveHostTerms(GameModes mode, int intensity, int domainCount,
                                         IList<Domains> aiDomains, Domains domain, VesselClassType vessel)
        {
            TryGet(mode, out var current);
            Put(current.WithHostTerms(intensity, domainCount, aiDomains, domain, vessel));
        }

        /// <summary>
        /// A pilot pressed ready on <paramref name="mode"/> as a guest. Writes only their own
        /// half; the host terms this machine last launched with are left as they were.
        /// </summary>
        public static void SavePilotChoice(GameModes mode, Domains domain, VesselClassType vessel)
        {
            TryGet(mode, out var current);
            Put(current.WithPilotChoice(domain, vessel));
        }

        /// <summary>Forget one card's record. Test and tooling hygiene; nothing in the UI calls it.</summary>
        public static void Clear(GameModes mode)
        {
            if (!_initialized) Init();
            _records.RemoveAll(r => r.GameMode == mode);
            DataAccessor.Save(SaveFileName, _records);
        }

        static void Put(LaunchPreference record)
        {
            if (!_initialized) Init();

            bool replaced = false;
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].GameMode != record.GameMode) continue;
                _records[i] = record;
                replaced = true;
                break;
            }
            if (!replaced) _records.Add(record);

            DataAccessor.Save(SaveFileName, _records);

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[LaunchPreference] Saved {record.GameMode}: intensity={record.Intensity}, " +
                $"domains={record.DomainCount}, ai={record.AIDomains?.Count ?? 0}, " +
                $"domain={record.Domain}, vessel={record.Vessel}, host={record.HasHostTerms}.");
        }
    }
}
