using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The escort on the galactic map (Docs/Voyages/ARK_ESCORT.md §5): an escort sails between two real places on the
    /// HyperSea, from where the squad's Ark lies (<see cref="VoyageState.At"/>) to the nearest place it can berth
    /// (<see cref="VoyageRules.EscortRoute"/>), and the Ark lies there once it makes it through
    /// (<see cref="VoyageRules.EscortArrived"/>). The worlds sailed are still the escort's own, rolled per voyage; the
    /// course only names where the voyage starts and stops, and the chart measures it (<see cref="VoyageChart"/>), the
    /// same light years and sea the job cards show. Content with no ports, or uncharted content, leaves the escort as it
    /// was: worlds and no destination.
    /// </summary>
    public sealed partial class ArkEscortRun
    {
        List<string> _course;

        /// <summary>The location ids the escort sails, start and destination included, while it is out; else null.</summary>
        public IReadOnlyList<string> Course => _active ? _course : null;

        /// <summary>The course an escort would sail from where the squad's Ark lies now, for the card; null with no
        /// destination.</summary>
        public static List<string> PreviewCourse(VoyageToyDefinitionSO definition, Domains home)
        {
            var content = ContentOf(definition);
            return content == null ? null : VoyageRules.EscortRoute(VoyageRun.LoadOrBegin(content, home), content);
        }

        static VoyageContent ContentOf(VoyageToyDefinitionSO definition)
        {
            var shelf = definition ? definition.Content : VoyageContentSO.Load();
            return shelf ? shelf.Content : null;
        }

        /// <summary>At setting sail: where from and where to, into the report.</summary>
        void PlanCourse()
        {
            _course = PreviewCourse(_def, VoyageRun.LiveDomain(_context));
            if (_course is not { Count: >= 2 })
            {
                _course = null;
                return;
            }
            _report.From = _course[0];
            _report.To = _course[^1];
            var sea = VoyageChart.For(ContentOf(_def))?.Along(_course);
            if (sea == null) return;
            _report.CourseLightYears = (float)sea.LightYears;
            _report.CourseCells = sea.Cells;
            _report.CourseIntensity = sea.Intensity;
        }

        string Place(string id) => ContentOf(_def)?.Location(id)?.Name ?? id;

        /// <summary>"JADE HARBOUR TO TOLLMOOR" under the departure banner, when there is a course.</summary>
        string CourseBannerSuffix() =>
            _course == null || string.IsNullOrEmpty(_escort.CourseLine) ? string.Empty
                : "\n" + TemplateText.Fill(_escort.CourseLine, "from", Place(_course[0]), "to", Place(_course[^1]));

        /// <summary>"BERTHED AT TOLLMOOR" under the made-it banner, when there is a course.</summary>
        string BerthedBannerSuffix() =>
            _course == null || string.IsNullOrEmpty(_escort.BerthedLine) ? string.Empty
                : "\n" + TemplateText.Fill(_escort.BerthedLine, "to", Place(_course[^1]));

        /// <summary>The escort made it through: the squad's Ark lies at the destination now. The escort sim (every seat
        /// AI) never moves the device's saved galaxy, so every sim run sets out from the same place.</summary>
        void ArriveOnCourse()
        {
            if (_course == null || _aiLocal) return;
            var content = ContentOf(_def);
            if (content == null) return;
            var state = VoyageRun.LoadOrBegin(content, VoyageRun.LiveDomain(_context));
            if (!VoyageRules.EscortArrived(state, content, _course[^1])) return;
            VoyageStore.Save(state);
            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[ArkEscort] Berthed at {_course[^1]}.");
        }
    }
}
