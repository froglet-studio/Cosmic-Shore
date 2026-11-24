using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>The Hold the Core lines (Docs/Voyages/MILESTONE_2.md section 6): its toasts, objective box and end board.
    /// Written from Docs/Voyages/voyage_slice.json by Tools/Build/author_voyage_assets.py, like every other line.</summary>
    public partial class VoyageLines
    {
        [Tooltip("Hold the Core: the Ark anchors under the nucleus. {to}.")]
        public string ToastCoreAnchor = "ANCHORED UNDER {to}";
        [Tooltip("Hold the Core: under the anchor toast.")]
        public string ToastCoreAnchorSub = "Lay mass in the nucleus. The clock runs while the core is yours.";
        [Tooltip("Hold the Core: your domain leads the nucleus; the hold clock runs.")]
        public string ToastCoreYours = "THE CORE IS YOURS";
        [Tooltip("Hold the Core: under the core-is-yours toast. {left} is a clock.")]
        public string ToastCoreYoursSub = "{left} to hold.";
        [Tooltip("Hold the Core: a wave begins. {wave} {waves}.")]
        public string ToastCoreWave = "WAVE {wave} OF {waves}";
        [Tooltip("Hold the Core: under the wave toast.")]
        public string ToastCoreWaveSub = "Their fauna push in on the Ark.";
        [Tooltip("Hold the Core: the other domain's crew arrives in wave 2.")]
        public string ToastCoreRetake = "A CREW COMES TO RETAKE IT";
        [Tooltip("Hold the Core: under the retake toast. {count}.")]
        public string ToastCoreRetakeSub = "{count} hulls. Silence them before they outbuild you.";
        [Tooltip("Hold the Core: another domain leads the nucleus after it was yours.")]
        public string ToastCoreTurning = "THE CORE IS TURNING";
        [Tooltip("Hold the Core: under the turning toast. {seconds}.")]
        public string ToastCoreTurningSub = "{seconds} s to take it back.";
        [Tooltip("Hold the Core: the objective box's goal line. {to} {goal} (a clock).")]
        public string ObjectiveCoreGoal = "Keep the core of {to} yours for {goal}";
        [Tooltip("Hold the Core: the strip's figure, held against the goal (clocks).")]
        public string ObjectiveCoreFigure = "{held} / {goal}";
        [Tooltip("Hold the Core: the strip's line on the way there. {to}.")]
        public string ObjectiveCoreReach = "Reach the core of {to}";
        [Tooltip("Hold the Core: the strip's line while the core is not yours.")]
        public string ObjectiveCoreBuild = "Build in the nucleus - the clock waits";
        [Tooltip("Hold the Core: the strip's line while the core is yours. {share} {wave} {waves}.")]
        public string ObjectiveCoreYours = "Yours {share}% - wave {wave} of {waves}";
        [Tooltip("Hold the Core: the strip's line while the core is turning. {seconds}.")]
        public string ObjectiveCoreTurning = "Turning - {seconds} s to take it back";
        [Tooltip("Hold the Core: the end board's title when the hold is done. {to}.")]
        public string ScoreCoreTitle = "{to} held";
        [Tooltip("Hold the Core: the end board's title when the core turned. {to}.")]
        public string ScoreCoreLostTitle = "{to} turned";
        [Tooltip("Hold the Core: the end board's stats line. {name} {held} {goal} {wave} {waves} {time}.")]
        public string ScoreCoreStats = "{name} - held {held} of {goal} - wave {wave} of {waves} - {time}";
        [Tooltip("Hold the Core: under Venture further. {cells}.")]
        public string ScoreVentureCore = "Sail on to the next core - {cells} cells";
    }
}
