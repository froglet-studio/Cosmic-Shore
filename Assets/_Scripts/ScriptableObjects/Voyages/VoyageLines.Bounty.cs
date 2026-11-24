using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>The Bounty lines (Docs/Voyages/MILESTONE_2.md section 7; Frogma Rev 137): its toasts, objective box and end
    /// board. Written from Docs/Voyages/voyage_slice.json by Tools/Build/author_voyage_assets.py, like every other line.</summary>
    public partial class VoyageLines
    {
        [Tooltip("Bounty: the hunted crew made port before the squad beat them. {name}.")]
        public string ToastRivalEscaped = "{name} MADE PORT";
        [Tooltip("Bounty: under the escaped toast.")]
        public string ToastRivalEscapedSub = "The warrant goes unserved. They will be harder to find.";
        [Tooltip("Bounty: a rival Ark's crew turns back after her hull broke. {name}.")]
        public string ToastRivalTurnBack = "{name} TURN BACK";
        [Tooltip("Bounty: under the turn-back toast.")]
        public string ToastRivalTurnBackSub = "Warrant served. Take what spilled before the fauna do.";
        [Tooltip("Bounty: the objective box's goal line. {rival} {to}.")]
        public string ObjectiveBountyGoal = "Serve the warrant on {rival} at {to}";
        [Tooltip("Bounty: the strip line on the way. {rival} {to}.")]
        public string ObjectiveBountyReach = "{rival} are at {to}. Get there before they leave.";
        [Tooltip("Bounty, a runaway crew: the strip line. {count} silenced of {total}, {time} a clock.")]
        public string ObjectiveBountyRunaway = "Silence all {total} at once - {count} down - they run in {time}";
        [Tooltip("Bounty, a rival Ark: the strip line while her escorts wall her. {ark} {count} {total} {time}.")]
        public string ObjectiveBountyArk = "Silence her escorts - {count} of {total} - {ark} leaves in {time}";
        [Tooltip("Bounty, a rival Ark: the strip line while her hull is open. {ark} {time}.")]
        public string ObjectiveBountyOpen = "{ark} is open for {time}. Break her hull!";
        [Tooltip("Bounty, a runaway crew: the strip figure. {count} {total}.")]
        public string ObjectiveBountyFigure = "{count} / {total} silenced";
        [Tooltip("Bounty, a rival Ark: the strip figure. {hull} {breaks} (percent).")]
        public string ObjectiveBountyArkFigure = "Her hull {hull}% - breaks at {breaks}%";
        [Tooltip("Bounty: the end board's title when the warrant was served. {rival}.")]
        public string ScoreBountyTitle = "Warrant served on {rival}";
        [Tooltip("Bounty: the end board's title when the crew got away. {rival}.")]
        public string ScoreBountyLostTitle = "{rival} got away";
        [Tooltip("Bounty: the end board's stats line. {name} {rival} {how} {time}.")]
        public string ScoreBountyStats = "{name} - {rival} {how} - {time}";
        [Tooltip("Bounty: {how} on the stats line for a routed runaway crew.")]
        public string ScoreBountyRouted = "routed";
        [Tooltip("Bounty: {how} on the stats line for a broken rival Ark.")]
        public string ScoreBountyBroken = "broken";
        [Tooltip("Bounty: {how} on the stats line when the crew made port.")]
        public string ScoreBountyEscaped = "made port";
        [Tooltip("Bounty: the venture caption. {cells}.")]
        public string ScoreVentureBounty = "Take the next warrant - {cells} cells";
    }
}
