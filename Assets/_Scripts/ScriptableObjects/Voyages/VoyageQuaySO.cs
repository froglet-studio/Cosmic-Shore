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

        [Header("The giver's ask")]
        [Tooltip("Over the giver's words. Tokens {kind} and {giver}.")]
        public string AskLabel = "{kind}  ·  {giver}";
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
