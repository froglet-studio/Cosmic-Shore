using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A chain of identical first-order low-pass stages over a fixed set of channels: a causal
    /// approximation of a Gaussian, and the gamepad's steering smoother
    /// (<see cref="CosmicShore.ScriptableObjects.GamepadFlightConfigSO"/>).
    ///
    /// <para><b>Order buys smoothness, the time constant costs lag, and they are separate dials.</b>
    /// A stick can step its turn rate in a few frames, which leaves a corner in the nose's angle.
    /// Each stage makes one more derivative of the output continuous, so the angle the hull
    /// integrates from <c>n</c> smoothed rates is continuous to its <c>n</c>th derivative. The chain's
    /// mean delay is the sum of its stages', <c>n * tau</c>. The single 0.67 s stage pilots flew until
    /// 2026-10-21 bought one derivative with all of its 0.67 s; four 30 ms stages buy four derivatives
    /// for 0.12 s.</para>
    ///
    /// <para>Each stage is discretised exactly (<c>1 - e^(-h / tau)</c>) over substeps of at most
    /// <see cref="MaxSubstepSeconds"/>, with the frame's input held: a chained stage's input moves
    /// within a frame, so whole-frame steps would make a 30 fps chain faster than a 144 fps one
    /// (measured: 90% of a step at 0.167 s vs 0.194 s; substepped, 0.200 s at both). A zero total
    /// delay passes the input straight through.</para>
    /// </summary>
    public sealed class CascadeSmoother
    {
        public const int MaxStages = 8;

        /// <summary>The longest integration step a frame is cut into (240 Hz).</summary>
        public const float MaxSubstepSeconds = 1f / 240f;

        // Below this a channel whose input is exactly zero lands on zero, so a released stick reads
        // as centred (an exponential only ever approaches it).
        const float SettleEpsilon = 1e-5f;

        readonly float[,] _stages;
        readonly int _channels;
        bool _primed;
        int _lastStageCount;

        public CascadeSmoother(int channels)
        {
            _channels = channels;
            _stages = new float[channels, MaxStages];
        }

        /// <summary>Settle every stage on zero (a released stick), without a glide from where it was.</summary>
        public void ResetToZero()
        {
            System.Array.Clear(_stages, 0, _stages.Length);
            _primed = true;
            _lastStageCount = MaxStages;
        }

        /// <summary>
        /// Advance every channel by <paramref name="dt"/> seconds toward <paramref name="values"/> and
        /// write the chain's output back into it. <paramref name="stageCount"/> is clamped to
        /// 1..<see cref="MaxStages"/>; <paramref name="totalDelaySeconds"/> is the chain's mean delay.
        /// </summary>
        public void Step(float[] values, float dt, int stageCount, float totalDelaySeconds)
        {
            int n = Mathf.Clamp(stageCount, 1, MaxStages);

            if (!_primed)
            {
                for (int c = 0; c < _channels; c++)
                    for (int s = 0; s < MaxStages; s++)
                        _stages[c, s] = values[c];
                _primed = true;
            }
            else if (n > _lastStageCount)
            {
                // Stages added while flying (a live retune) start where the chain's output was, so
                // the change does not glide in from a stale value.
                for (int c = 0; c < _channels; c++)
                    for (int s = _lastStageCount; s < n; s++)
                        _stages[c, s] = _stages[c, _lastStageCount - 1];
            }
            _lastStageCount = n;

            if (totalDelaySeconds <= 0f)
            {
                // Off: every stage sits on its input, so turning the smoother back on starts at rest.
                for (int c = 0; c < _channels; c++)
                    for (int s = 0; s < n; s++)
                        _stages[c, s] = values[c];
                return;
            }

            dt = Mathf.Max(0f, dt);
            int substeps = Mathf.Max(1, Mathf.CeilToInt(dt / MaxSubstepSeconds - 1e-4f));
            float alpha = 1f - Mathf.Exp(-(dt / substeps) * n / totalDelaySeconds);

            for (int c = 0; c < _channels; c++)
            {
                float target = values[c];
                float input = target;
                for (int k = 0; k < substeps; k++)
                {
                    input = target;
                    for (int s = 0; s < n; s++)
                    {
                        _stages[c, s] += (input - _stages[c, s]) * alpha;
                        input = _stages[c, s];
                    }
                }

                if (target == 0f && Mathf.Abs(input) < SettleEpsilon)
                {
                    for (int s = 0; s < n; s++) _stages[c, s] = 0f;
                    input = 0f;
                }
                values[c] = input;
            }
        }
    }
}
