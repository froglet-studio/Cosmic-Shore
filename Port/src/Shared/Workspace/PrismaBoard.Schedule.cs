#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Prisma
{
    /// <summary>
    /// The scheduler's fields on a board card. All of them are optional, and a card without them
    /// writes exactly what it wrote before, so older Prisma builds keep reading the board.
    /// Dates and times are the user's local wall clock (a day and a time of day, no zone): "Friday
    /// 15:00" stays Friday 15:00 for whoever reads it.
    /// </summary>
    public sealed partial class PrismaBoard
    {
        public sealed partial class Item
        {
            /// <summary>The day it is planned for (when you will do it).</summary>
            public DateOnly? Scheduled { get; set; }
            public TimeOnly? ScheduledTime { get; set; }
            /// <summary>The deadline.</summary>
            public DateOnly? Due { get; set; }
            public TimeOnly? DueTime { get; set; }
            /// <summary>Lower-case labels without the '#'.</summary>
            public List<string> Tags { get; set; } = new();
            /// <summary>Who it is for (a name or, later, a workspace member id).</summary>
            public string? Assignee { get; set; }
            public int? EstimateMinutes { get; set; }
            public List<ChecklistEntry> Checklist { get; set; } = new();
            /// <summary>A <see cref="Workspace.Recurrence"/> rule ("daily", "weekly:mon,thu" ...); completing the card schedules the next one.</summary>
            public string? Recurrence { get; set; }
            /// <summary>When it last moved to DONE (cleared if it leaves DONE).</summary>
            public DateTime? CompletedAt { get; set; }

            /// <summary>TO DO or DOING.</summary>
            [JsonIgnore] public bool IsOpen => State is Status.Todo or Status.Doing;
            /// <summary>The day the card belongs to: planned day, else deadline.</summary>
            [JsonIgnore] public DateOnly? Day => Scheduled ?? Due;
            [JsonIgnore] public TimeOnly? Time => Scheduled != null ? ScheduledTime : DueTime;
        }

        public sealed class ChecklistEntry
        {
            public string Text { get; set; } = "";
            public bool Done { get; set; }
        }
    }
}
