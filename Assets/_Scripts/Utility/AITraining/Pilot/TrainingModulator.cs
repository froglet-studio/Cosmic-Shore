using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// The bridge between a genome and a live vessel. Replaces the retired
    /// TrainingPilot, and the difference is the whole architecture:
    ///
    ///   The shipped AIPilot KEEPS FLYING. This component applies the genome to
    ///   the pilot's tuning surface (AIPilot.ApplyExternalTuning), composes the
    ///   intensity's skill/cooldown factors into that same apply, and then — for
    ///   intensities below 4 — degrades the pilot's own steering output once per
    ///   frame in LateUpdate through IntensityDitherer.
    ///
    /// Both halves respect the framework's founding constraint: input only.
    /// The ditherer perturbs the very same IInputStatus fields the AIPilot wrote
    /// this frame; nothing here reads or writes a transform, a velocity, or any
    /// non-input state. What deploys is indistinguishable from a pilot whose
    /// hands are simply better or worse.
    ///
    /// One quirk to know: the dither lands in LateUpdate, after this frame's
    /// consumers have read input, so its effect arrives one frame later. That
    /// is fine — the layer exists to simulate human imperfection, and one frame
    /// of latency is part of the imperfection.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrainingModulator : MonoBehaviour
    {
        TrainingGenome _genome;
        int _intensity = 4;

        IVessel _vessel;
        IVesselStatus _status;
        IInputStatus _input;
        AIPilot _aiPilot;

        readonly IntensityDitherer _ditherer = new();
        readonly EpisodeObservation _observation = new();

        bool _episodeActive;
        float _episodeStartTime;
        int _populationIndex = -1;

        public TrainingGenome Genome => _genome;
        public int Intensity => _intensity;
        public bool EpisodeActive => _episodeActive;
        public int PopulationIndex { get => _populationIndex; set => _populationIndex = value; }
        public EpisodeObservation Observation => _observation;
        public string PersonalityName => PilotTuningGenes.PersonalityName(_genome);

        /// <summary>
        /// Wires this modulator to a vessel. Finds the vessel's AIPilot; a vessel
        /// without one cannot be trained (nothing to tune) and Bind reports false.
        /// </summary>
        public bool BindVessel(IVessel vessel)
        {
            _vessel = vessel;
            _status = vessel?.VesselStatus;
            _input = _status?.InputStatus;

            var go = vessel?.Transform != null ? vessel.Transform.gameObject : null;
            _aiPilot = go != null ? go.GetComponentInChildren<AIPilot>() : null;

            if (_aiPilot == null)
            {
                Debug.LogWarning($"[TrainingModulator] No AIPilot found on '{go?.name}'; cannot modulate.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Applies a genome at a difficulty level. The genome's tuning and the
        /// intensity's tempo factors are composed into ONE ApplyExternalTuning call,
        /// so re-applying (a new episode, a new match, a difficulty change) never
        /// compounds — AIPilot scales ability timings from authored baselines.
        /// </summary>
        public void ApplyGenome(TrainingGenome genome, int intensity)
        {
            _genome = genome ?? TrainingGenome.FromRegistryDefaults();
            _intensity = Mathf.Clamp(intensity, 1, 4);

            if (_aiPilot == null) return;

            var tuning = PilotTuningGenes.ToTuning(_genome);
            var level = _ditherer.GetSettings(_intensity);

            // Compose difficulty into the same apply: a lower-intensity pilot has a
            // lower skill dial and a slower ability cadence, on top of whatever the
            // genome says. At intensity 4 both factors are exactly 1.
            tuning.SkillLevel = (tuning.SkillLevel ?? 1f) * level.SkillFactor;
            tuning.AbilityCooldownScale = (tuning.AbilityCooldownScale ?? 1f) * level.AbilityCooldownFactor;

            _aiPilot.ApplyExternalTuning(tuning);
        }

        public void BeginEpisode(int populationIndex = -1)
        {
            _populationIndex = populationIndex;
            _ditherer.Reset();
            _episodeActive = true;
            _episodeStartTime = Time.time;
            UpdateObservation();
        }

        public void EndEpisode()
        {
            _episodeActive = false;
        }

        void LateUpdate()
        {
            if (!_episodeActive || _status == null || _input == null) return;

            UpdateObservation();

            // Input degradation for intensities below the trained ceiling. Reads what
            // the AIPilot wrote this frame, writes back the humanly-imperfect version.
            if (_intensity < 4 && _aiPilot != null && _aiPilot.AutoPilotEnabled && !_input.Paused)
            {
                var frame = new IntensityDitherer.InputFrame
                {
                    XSum = _input.XSum,
                    YSum = _input.YSum,
                    XDiff = _input.XDiff,
                    YDiff = _input.YDiff,
                    EasedLeft = _input.EasedLeftJoystickPosition
                };

                frame = _ditherer.Apply(_intensity, frame, Time.time);

                if (_status.IsSingleStickControls)
                {
                    _input.EasedLeftJoystickPosition = frame.EasedLeft;
                }
                else
                {
                    _input.XSum = frame.XSum;
                    _input.YSum = frame.YSum;
                    _input.XDiff = frame.XDiff;
                    _input.YDiff = frame.YDiff;
                }
            }
        }

        void UpdateObservation()
        {
            _observation.VesselStatus = _status;
            _observation.PlayerName = _status.PlayerName;
            _observation.Position = _vessel != null && _vessel.Transform != null
                ? _vessel.Transform.position : Vector3.zero;
            _observation.Speed = _status.Speed;
            _observation.IsBoosting = _status.IsBoosting;
            _observation.IsDrifting = _status.IsDrifting;
            _observation.EpisodeTime = _episodeActive ? Time.time - _episodeStartTime : 0f;
        }
    }
}
