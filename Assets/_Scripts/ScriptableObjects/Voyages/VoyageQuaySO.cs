using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The words of the Milestone 2 quay (Frogma Rev 134 Board B, kept in Rev 138 Board; Docs/MENU_REDESIGN_B.md section
    /// 231) and of the pilots' moods (<see cref="PilotMood"/>): the giver's ask, "Your crew says", the squad fit, Take the
    /// job, See every class, and each mood's name, line and reason. Every word is a field a person can edit or blank here
    /// (<c>Resources/Voyages/VoyageQuay</c>, made by <c>Tools/Build/author_voyage_classes.py</c>).
    /// </summary>
    [CreateAssetMenu(fileName = "VoyageQuay", menuName = "ScriptableObjects/Voyages/Voyage Quay")]
    public class VoyageQuaySO : ScriptableObject
    {
        /// <summary>Where the shipped words live, under Resources.</summary>
        public const string ResourcePath = "Voyages/VoyageQuay";

        static VoyageQuaySO s_loaded;

        /// <summary>The shipped words (<c>Resources/Voyages/VoyageQuay</c>), or null when the asset is missing.</summary>
        public static VoyageQuaySO Load()
        {
            if (!s_loaded) s_loaded = Resources.Load<VoyageQuaySO>(ResourcePath);
            return s_loaded;
        }

        [Header("The quay")]
        [Tooltip("Over the jobs: where the Ark is docked. Tokens {place} and {count}. Blank: no line.")]
        public string Docked = "Docked at {place}.  {count} jobs on the quay.";
        [Tooltip("The chart's label, top left.")]
        public string OnTheChart = "ON THE CHART";
        [Tooltip("Under the chart: the voyage's length and its sea. Tokens {cells} and {danger}.")]
        public string ChartFoot = "{cells} cells  ·  {danger} sea";

        [Header("Voyage types (a card's kind; a type is never a voyage's name)")]
        public string KindSalvage = "Salvage";
        public string KindEscort = "Escort the Ark";
        public string KindBounty = "Bounty";
        public string KindHold = "Hold the Core";

        [Header("The sea's danger by difficulty (1-2, 3-4, 5-7, 8-9)")]
        public string DangerCalm = "Calm";
        public string DangerRough = "Rough";
        public string DangerHostile = "Hostile";
        public string DangerDeadly = "Deadly";

        [Header("The giver's ask")]
        [Tooltip("Over the giver's words. Tokens {kind}, {giver} and {faction}.")]
        public string AskLabel = "{giver}  ·  {faction}";
        [Tooltip("The giver's words. Token {say}.")]
        public string AskQuote = "\"{say}\"";
        [Tooltip("Before the classes the voyage wants.")]
        public string BringMe = "BRING ME";

        [Header("Your crew")]
        [Tooltip("Over the crew's answers.")]
        public string CrewSays = "YOUR CREW SAYS";
        [Tooltip("A crew member's answer. Token {say}.")]
        public string CrewQuote = "\"{say}\"";

        [Header("The foot")]
        [Tooltip("Beside the fit meter. Token {fit}.")]
        public string Fit = "Squad fit {fit} of 3";
        [Tooltip("Under the chart: the voyage's length. Token {cells}.")]
        public string Cells = "{cells} cells";
        [Tooltip("The call to action: the picked job goes to Your Ark.")]
        public string TakeJob = "TAKE THE JOB";
        [Tooltip("The keycap on Take the job.")]
        public string TakeJobKey = "A";
        [Tooltip("Opens Vessels to browse every class.")]
        public string SeeClasses = "SEE EVERY CLASS";
        [Tooltip("The keycap on See every class.")]
        public string SeeClassesKey = "Y";

        [Header("The full screen")]
        [Tooltip("Over the givers down the right. Token {place}.")]
        public string WorkAt = "WORK AT {place}";
        [Tooltip("Hides the givers' column.")]
        public string Hide = "HIDE";
        [Tooltip("Shows the givers' column again.")]
        public string Show = "SHOW";
        [Tooltip("A giver's row: its kind and the giver's faction. Tokens {kind}, {giver} and {faction}.")]
        public string GiverKicker = "{kind}  ·  {faction}";
        [Tooltip("A giver's row, after the classes. Tokens {place} and {danger}.")]
        public string GiverMeta = "{place}  ·  {danger}";
        [Tooltip("The picked job's card at the foot, over its name. Tokens {kind} and {giver}.")]
        public string FullKicker = "{kind}  ·  {giver}";
        [Tooltip("The picked job's card, under its name. Tokens {from}, {to} and {cells}.")]
        public string FullRoute = "{from} to {to}  ·  {cells} cells";
        [Tooltip("Before the classes the picked job wants.")]
        public string Wants = "WANTS";
        [Tooltip("A rival crew on the map: its name, then this. Token {to}.")]
        public string RivalBound = "Also bound for {to}";
        [Tooltip("A wreck next door on the map.")]
        public string Wreck = "Wreck";
        [Tooltip("A bloom on the map.")]
        public string Bloom = "Bloom";

        [Header("Moods")]
        public string MoodKeen = "Keen";
        public string MoodSteady = "Steady";
        public string MoodReluctant = "Reluctant";
        public string MoodRefuses = "Refuses";
        [Tooltip("What a Keen pilot says on the quay.")]
        public string SayKeen = "Say no more. I'm in.";
        [Tooltip("What a Steady pilot says on the quay.")]
        public string SaySteady = "Work is work.";
        [Tooltip("What a Reluctant pilot says on the quay.")]
        public string SayReluctant = "Not our kind of job. If we must.";
        [Tooltip("What a pilot who refuses says on the quay.")]
        public string SayRefuses = "Not this one. Find someone else.";
        [Tooltip("Why, under a seat's mood on Your Ark (Keen).")]
        public string WhyKeen = "Their faction wants this work";
        [Tooltip("Why, under a seat's mood on Your Ark (Steady).")]
        public string WhySteady = "Pay is pay";
        [Tooltip("Why, under a seat's mood on Your Ark (Reluctant).")]
        public string WhyReluctant = "Not their faction's kind of work";
        [Tooltip("Why, under a seat's mood on Your Ark (Refuses).")]
        public string WhyRefuses = "Against their faction's ethos";
        [Tooltip("Beside a held Set sail while a seat refuses. Token {pilot}.")]
        public string RefusesBlock = "{pilot} refuses this voyage";

        [Header("Your Ark")]
        [Tooltip("Over the classes the job wants, top right of Your Ark.")]
        public string Wanted = "WANTED";
        [Tooltip("Beside the danger meter under the job's name: the sea. Token {danger}.")]
        public string ArkSea = "{danger} sea";
        [Tooltip("After the sea under the job's name. Tokens {cells}, {giver}.")]
        public string ArkFor = "{cells} cells  ·  for {giver}";
        [Tooltip("Left of Set sail: what the job pays. Token {pay}.")]
        public string ArkPay = "Pay {pay}";

        [Header("Pilots")]
        [Tooltip("Over the pilots list. Tokens {seat}, {voyage}.")]
        public string PilotsKicker = "{seat}  ·  PILOT FOR {voyage}";
        [Tooltip("The pilots page's heading. Token {vessel}.")]
        public string PilotsHeading = "Who flies the {vessel}";
        [Tooltip("Under a pilot's name in the list. Tokens {faction}, {why}.")]
        public string PilotsRowMeta = "{faction}  ·  {why}";
        [Tooltip("The call to action: the picked pilot takes the seat.")]
        public string TakeSeat = "TAKE THE SEAT";
        [Tooltip("The keycap on Take the seat.")]
        public string TakeSeatKey = "A";

        /// <summary>The kind of a voyage type (a job template id, or escort); null for a job of no Milestone 2 type.</summary>
        public string KindOf(string type) => type switch
        {
            "job.salvage.drift" => KindSalvage,
            NamedVoyage.EscortType => KindEscort,
            "job.bounty.crew" => KindBounty,
            "job.defence.aid" => KindHold,
            _ => null,
        };

        /// <summary>How dangerous the sea is at <paramref name="difficulty"/> (1-9), 1 (calm) to 4 (deadly).</summary>
        public static int DangerLevel(int difficulty) => difficulty <= 2 ? 1 : difficulty <= 4 ? 2 : difficulty <= 7 ? 3 : 4;

        /// <summary>The sea's danger at <paramref name="difficulty"/>, in words.</summary>
        public string Danger(int difficulty) => DangerLevel(difficulty) switch
        {
            1 => DangerCalm,
            2 => DangerRough,
            3 => DangerHostile,
            _ => DangerDeadly,
        };

        public string MoodName(PilotMood m) => m switch
        {
            PilotMood.Keen => MoodKeen,
            PilotMood.Steady => MoodSteady,
            PilotMood.Reluctant => MoodReluctant,
            PilotMood.Refuses => MoodRefuses,
            _ => string.Empty,
        };

        public string Say(PilotMood m) => m switch
        {
            PilotMood.Keen => SayKeen,
            PilotMood.Steady => SaySteady,
            PilotMood.Reluctant => SayReluctant,
            PilotMood.Refuses => SayRefuses,
            _ => string.Empty,
        };

        public string Why(PilotMood m) => m switch
        {
            PilotMood.Keen => WhyKeen,
            PilotMood.Steady => WhySteady,
            PilotMood.Reluctant => WhyReluctant,
            PilotMood.Refuses => WhyRefuses,
            _ => string.Empty,
        };
    }
}
