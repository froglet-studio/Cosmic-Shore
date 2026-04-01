namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Immutable result of a shape drawing attempt, computed when the player finishes the shape.
    /// </summary>
    public readonly struct ShapeScoreData
    {
        /// <summary>Name of the shape that was drawn.</summary>
        public readonly string ShapeName;

        /// <summary>Seconds the player took to complete the shape.</summary>
        public readonly float ElapsedTime;

        /// <summary>Par time from the ShapeDefinition.</summary>
        public readonly float ParTime;

        /// <summary>
        /// 0-100 percentage. Measures how closely the player's path matched the ideal shape.
        /// Computed as average proximity of player trail samples to the nearest ideal segment.
        /// </summary>
        public readonly float AccuracyPercent;

        /// <summary>
        /// Combined star rating 1-5 based on time and accuracy.
        /// </summary>
        public readonly int StarRating;

        public ShapeScoreData(string shapeName, float elapsedTime, float parTime, float accuracyPercent)
        {
            ShapeName = shapeName;
            ElapsedTime = elapsedTime;
            ParTime = parTime;
            AccuracyPercent = accuracyPercent;

            // Star rating: blend of time and accuracy
            float timeScore = parTime > 0f
                ? UnityEngine.Mathf.Clamp01(parTime / UnityEngine.Mathf.Max(elapsedTime, 0.1f))
                : 1f;
            float combined = timeScore * 0.4f + (accuracyPercent / 100f) * 0.6f;

            StarRating = combined switch
            {
                >= 0.9f => 5,
                >= 0.75f => 4,
                >= 0.55f => 3,
                >= 0.35f => 2,
                _ => 1
            };
        }
    }
}
