using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One flight scheme's Lesson: the ordered steps that teach a hull flown that way to fly.
    /// Authored ONCE per scheme and shared by every hull of that scheme - a new vessel gets a
    /// Lesson on day one, because the steps name its abilities through tokens rather than by
    /// name (Docs/ModePreview/TRAINING_PLAN.md §4.1).
    /// </summary>
    [CreateAssetMenu(fileName = "LessonTemplate", menuName = "ScriptableObjects/Drill/Lesson Template")]
    public sealed class LessonTemplateSO : ScriptableObject
    {
        [Tooltip("Which flight scheme this Lesson teaches.")]
        [SerializeField] FlightScheme scheme = FlightScheme.TwoThumb;

        [Tooltip("The steps, in order. A step whose Applicability does not hold for a hull, or whose " +
                 "tokens name a fact the hull lacks, is dropped for that hull.")]
        [SerializeField] List<LessonStep> steps = new();

        public FlightScheme Scheme => scheme;
        public IReadOnlyList<LessonStep> Steps => steps;
    }
}
