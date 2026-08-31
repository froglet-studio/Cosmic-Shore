using System;
using System.Collections.Generic;
using System.Text;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Per-episode fitness breakdown. Each fitness component contributes a labeled
    /// score and the runner aggregates them into a single weighted total.
    ///
    /// Keeping the breakdown around lets the editor window show why a genome scored
    /// what it did — without that visibility you can't tell whether the search is
    /// pursuing the actual game objective or has found a degenerate local maximum.
    /// </summary>
    [Serializable]
    public class TrainingFitness
    {
        [Serializable]
        public struct Component
        {
            public string Label;
            public float Raw;
            public float Weight;
            public float Weighted => Raw * Weight;
        }

        public List<Component> Components = new();
        public float Total;
        public bool TimedOut;
        public bool Crashed;
        public float EpisodeSeconds;

        public void Add(string label, float raw, float weight)
        {
            Components.Add(new Component { Label = label, Raw = raw, Weight = weight });
            Total += raw * weight;
        }

        public string Summarize()
        {
            var sb = new StringBuilder(256);
            sb.Append($"total={Total:F2} t={EpisodeSeconds:F1}s");
            if (TimedOut) sb.Append(" [TIMEOUT]");
            if (Crashed) sb.Append(" [CRASHED]");
            sb.Append(" |");
            foreach (var c in Components)
                sb.Append($" {c.Label}={c.Weighted:F1}({c.Raw:F2})");
            return sb.ToString();
        }
    }
}
