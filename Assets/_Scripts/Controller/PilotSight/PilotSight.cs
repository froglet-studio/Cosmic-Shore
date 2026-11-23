using CosmicShore.Data;
using CosmicShore.Gameplay.Audio;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The PILOT'S SIGHT (Docs/Pilots/CALLSIGNS.md): hold <b>LB</b> on any vessel and what your pilot
    /// sees lights up while everything else is suppressed. One kind of thing per pilot, its callsign
    /// (<see cref="PilotVision"/>).
    ///
    /// <para><b>On every vessel, by the platform law</b> (<see cref="VesselController.Initialize"/>),
    /// and live only on the LOCAL pilot's: it polls <see cref="PilotSightGesture"/> only while this
    /// hull is the local pilot's and their input is live, so a replica, an AI hull or a paused game
    /// never opens it. One sight at a time publishes (<see cref="Local"/>).</para>
    ///
    /// <para><b>It touches nothing but photons</b> (§4.1, Echo Sight's rule). It writes the prism
    /// shader's <c>_PilotVision*</c> globals (their own names, never a slot in Lit's peer bank), the
    /// suppression volume's weight, and its marks. No outcome reads any of it.</para>
    ///
    /// <para><b>It is local</b> (§4.2). No other player's screen dims because you held LB. The hull's
    /// callsign plate, the one shared part, waits on <c>/vessel-model</c> placing it (§7).</para>
    ///
    /// <para><b>Which sight.</b> A pilot carries one (<c>PilotSO.vision</c>, once the pilot types
    /// land); until then the vessel uses <see cref="CallsignSetSO.UnassignedSight"/>, or what
    /// <see cref="SetSight"/> gave it.</para>
    /// </summary>
    public sealed class PilotSight : MonoBehaviour
    {
        const string SetResourcePath = "Pilots/CallsignSet";

        static readonly int StrengthId = Shader.PropertyToID("_PilotVisionStrength");
        static readonly int SightId = Shader.PropertyToID("_PilotVisionSight");
        static readonly int DomainId = Shader.PropertyToID("_PilotVisionDomain");
        static readonly int VolumeId = Shader.PropertyToID("_PilotVisionVolume");

        static CallsignSetSO s_set;
        static bool s_setResolved;
        static PilotSight s_local;

        IVesselStatus _status;
        GameDataSO _gameData;
        PilotVision _assigned;
        bool _hasAssigned;
        float _strength;
        bool _wasHeld;
        readonly PilotSightMarks _marks = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_set = null;
            s_setResolved = false;
            s_local = null;
        }

        /// <summary>The callsigns and the sight's look (the Resources asset, or defaults when missing).</summary>
        public static CallsignSetSO Set
        {
            get
            {
                if (!s_setResolved)
                {
                    s_set = Resources.Load<CallsignSetSO>(SetResourcePath);
                    if (!s_set)
                    {
                        CSDebug.LogWarning($"[PilotSight] No Resources/{SetResourcePath} asset - the sight runs on " +
                                           "CallsignSetSO defaults with no callsigns. Run Tools/Build/author_callsign_assets.py.");
                        s_set = ScriptableObject.CreateInstance<CallsignSetSO>();
                    }
                    s_setResolved = true;
                }
                return s_set;
            }
        }

        /// <summary>The sight publishing on this machine, or null.</summary>
        public static PilotSight Local => s_local;

        /// <summary>This vessel's callsign.</summary>
        public PilotVision Sight => _hasAssigned ? _assigned
            : _status?.PilotLoadout?.Pilot is { } pilot ? pilot.Vision : Set.UnassignedSight;

        /// <summary>The LB fade, 0 (closed) to 1 (open).</summary>
        public float Strength => _strength;

        /// <summary>True while this pilot is looking (the moment the plate lights, once it exists).</summary>
        public bool IsLooking => _strength > 0f;

        /// <summary>Marks drawn last frame (diagnostics).</summary>
        public int MarkCount => _marks.Count;

        public static PilotSight EnsureOn(VesselController vessel, GameDataSO gameData)
        {
            if (!vessel) return null;
            if (!vessel.TryGetComponent(out PilotSight sight))
                sight = vessel.gameObject.AddComponent<PilotSight>();
            sight._status = vessel.VesselStatus;
            sight._gameData = gameData;
            return sight;
        }

        /// <summary>Give this vessel its pilot's sight (the squad slot's pilot).</summary>
        public void SetSight(PilotVision sight)
        {
            _assigned = sight;
            _hasAssigned = true;
        }

        void Update()
        {
            if (!IsLocalPilotsLiveHull())
            {
                if (s_local == this) Close();
                return;
            }

            var provider = PilotSightProviders.For(Sight);
            bool held = provider != null && PilotSightGesture.Held();
            var set = Set;
            float rate = held ? 1f / set.FadeInSeconds : -1f / set.FadeOutSeconds;
            _strength = Mathf.Clamp01(_strength + rate * Time.unscaledDeltaTime);

            if (held && !_wasHeld)
            {
                var row = set.For(Sight);
                if (row != null) FMODOneShotVolumeHelper.PlaySFXOneShot(row.openSound, transform.position, 1f);
            }
            _wasHeld = held;

            if (_strength <= 0f)
            {
                if (s_local == this) Close();
                return;
            }

            // One publisher: a second local hull (a swap mid-sight) takes over cleanly.
            if (s_local && s_local != this) s_local.Close();
            s_local = this;
            Publish(provider, set);
        }

        void Publish(PilotSightProvider provider, CallsignSetSO set)
        {
            var domain = _status.Player != null ? _status.Domain : Domains.Blue;
            var colors = _gameData && _gameData.ThemeManagerData ? _gameData.ThemeManagerData.ColorSet : null;
            var ctx = new PilotSightContext(_status, domain, _strength, set, colors);

            var prisms = new PilotSightPrismState();
            _marks.Clear();
            provider?.Gather(ctx, ref prisms, _marks);

            Shader.SetGlobalFloat(StrengthId, _strength);
            Shader.SetGlobalFloat(SightId, prisms.Sight);
            Shader.SetGlobalFloat(DomainId, prisms.Domain);
            Shader.SetGlobalVector(VolumeId, prisms.Volume);
            PilotSightSuppression.Apply(_strength, set);
            _marks.Draw(set.MarkMinScreenRadius);
        }

        /// <summary>Close the sight on this machine: every global back to its "off" sentinel.</summary>
        void Close()
        {
            _strength = 0f;
            _wasHeld = false;
            _marks.Clear();
            Shader.SetGlobalFloat(StrengthId, 0f);
            Shader.SetGlobalFloat(SightId, 0f);
            Shader.SetGlobalFloat(DomainId, 0f);
            Shader.SetGlobalVector(VolumeId, Vector4.zero);
            PilotSightSuppression.Apply(0f, Set);
            if (s_local == this) s_local = null;
        }

        bool IsLocalPilotsLiveHull()
        {
            var player = _status?.Player;
            if (player == null || !player.IsLocalPilot) return false;
            if (player.IsInitializedAsAI || _status.AutoPilotEnabled) return false;
            var input = _status.InputStatus;
            return input != null && !input.Paused;
        }

        void OnDisable()
        {
            if (s_local == this) Close();
        }
    }
}
