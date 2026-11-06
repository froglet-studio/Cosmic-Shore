using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>What a launch action button is, by the style guide's families.</summary>
    public enum LaunchActionKind
    {
        /// <summary>A neutral link: a dashboard, a doc.</summary>
        Plain = 0,
        /// <summary>Lime: the next step you can still change (build, upload, stage, deploy to development).</summary>
        CallToAction = 1,
        /// <summary>Ruby: no way back (promote, roll back, halt a rollout, deploy to production).</summary>
        Commit = 2,
    }

    [Serializable]
    public sealed class LaunchChannelEntry
    {
        [Tooltip("The component id on the launch-status branch (Docs/LAUNCH/STATUS_CONTRACT.md, Component ids).")]
        public string id;
        [Tooltip("The row's name in the console, e.g. 'Playtest internal'.")]
        public string label;
    }

    [Serializable]
    public sealed class LaunchActionEntry
    {
        [Tooltip("The button's words.")]
        public string label;
        [Tooltip("Lime (call to action), Ruby (no way back) or plain.")]
        public LaunchActionKind kind;
        [Tooltip("The https page the button opens: a workflow page or a dashboard. Never an API call.")]
        public string url;
    }

    [Serializable]
    public sealed class LaunchPlatformEntry
    {
        [Tooltip("Short key, the artifact's PLATFORMS key: windows, ios, android, web, liveops, backend, launch.")]
        public string key;
        [Tooltip("The pill's name.")]
        public string displayName;
        [Tooltip("The channels (or, for Launch-wide, the gates) this platform shows, in order.")]
        public List<LaunchChannelEntry> channels = new List<LaunchChannelEntry>();
        [Tooltip("The player build's component id (build-windows ...); empty when the platform has none.")]
        public string buildId;
        [Tooltip("Also count every record whose id starts with this (Launch-wide: 'smoke-'); empty for none.")]
        public string extraIdPrefix;
        [Header("The audit's verdict")]
        [Tooltip("READINESS.md section 0's verdict for this platform. Re-measure the audit before changing it.")]
        public LaunchState auditVerdict;
        [Tooltip("Section 0's 'why', in a few words.")]
        public string auditWhy;
        [Tooltip("READINESS.md section 10 ranks that block this platform.")]
        public List<int> blockerRanks = new List<int>();
        [Header("Actions")]
        [Tooltip("The artifact's action buttons for this platform, the same links.")]
        public List<LaunchActionEntry> actions = new List<LaunchActionEntry>();
        [Tooltip("How to roll back, one line; empty for none.")]
        public string rollback;
    }

    [Serializable]
    public sealed class LaunchBlockerEntry
    {
        [Tooltip("The rank in READINESS.md section 10.")]
        public int rank;
        [Tooltip("What must be true on launch day, short.")]
        public string title;
        [Tooltip("Who decides (the area's owner), never an assignee.")]
        public string decider;
        [Tooltip("Its phase names a step only a person with an account can do.")]
        public bool needsHuman;
    }

    [Serializable]
    public sealed class LaunchHumanStep
    {
        [Tooltip("The step, short.")]
        public string text;
        [Tooltip("Who it waits on.")]
        public string who;
    }

    /// <summary>
    /// The Launch Console's configuration: the milestone, where the status file lives, and per
    /// platform the component ids, the audit's verdict, its blockers and the action links (the same
    /// links as the console artifact's PLATFORMS table). The only hand-kept figures are the audit's
    /// (READINESS.md sections 0 and 10), shown with <see cref="auditDate"/> beside them. Editor-only:
    /// the class and its asset live under Editor folders and never reach a player build.
    /// </summary>
    public sealed class LaunchConsoleConfigSO : ScriptableObject
    {
        public const string AssetPath = "Assets/_SO_Assets/Launch/Editor/LaunchConsoleConfig.asset";

        [Header("Milestone")]
        [Tooltip("The milestone's name, e.g. 'Invite build v1'.")]
        public string milestoneName;
        [Tooltip("The milestone date, yyyy-MM-dd (UTC).")]
        public string milestoneDate;

        [Header("Status source")]
        [Tooltip("The repository's GitHub page, for the links.")]
        public string repoUrl;
        [Tooltip("The data-only branch the pipelines publish to.")]
        public string statusBranch = "launch-status";
        [Tooltip("The merged file on that branch.")]
        public string statusFile = "status.json";
        [Tooltip("A record older than this many hours is At risk.")]
        [Min(1)] public float staleHours = 48f;

        [Header("The console artifact")]
        [Tooltip("The claude.ai Launch Console: its checklist and Needs a human live in its store, which this window cannot read.")]
        public string consoleUrl;

        [Header("The audit")]
        [Tooltip("Where the verdicts, blockers and human steps come from.")]
        public string auditSource;
        [Tooltip("When the audit was measured, yyyy-MM-dd.")]
        public string auditDate;
        [Tooltip("The audit's quality figures, one line each.")]
        public List<string> qualityNotes = new List<string>();

        [Header("Platforms, gates, blockers")]
        public List<LaunchPlatformEntry> platforms = new List<LaunchPlatformEntry>();
        [Tooltip("The quality gates' component ids; smoke-<mode> records are added on their own.")]
        public List<LaunchChannelEntry> gates = new List<LaunchChannelEntry>();
        [Tooltip("Smoke records' id prefix.")]
        public string smokePrefix = "smoke-";
        [Tooltip("READINESS.md section 10, ranked.")]
        public List<LaunchBlockerEntry> blockers = new List<LaunchBlockerEntry>();
        [Tooltip("READINESS.md section 10, 'Needs a human, in order'.")]
        public List<LaunchHumanStep> humanSteps = new List<LaunchHumanStep>();

        public DateTime? MilestoneUtc => LaunchStatusBoard.ParseUtc(milestoneDate);

        public string MilestoneLabel =>
            MilestoneUtc is DateTime d ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : milestoneDate;

        public LaunchBlockerEntry Blocker(int rank)
        {
            foreach (var b in blockers)
                if (b != null && b.rank == rank) return b;
            return null;
        }

        /// <summary>Every component id a platform's pill reads: its channels, its build, and the prefix matches.</summary>
        public static List<string> IdsFor(LaunchPlatformEntry p, LaunchStatusBoard board)
        {
            var ids = new List<string>();
            if (p == null) return ids;
            foreach (var c in p.channels)
                if (c != null && !string.IsNullOrEmpty(c.id)) ids.Add(c.id);
            if (!string.IsNullOrEmpty(p.buildId)) ids.Add(p.buildId);
            if (!string.IsNullOrEmpty(p.extraIdPrefix) && board != null)
                foreach (var id in board.Records.Keys)
                    if (id.StartsWith(p.extraIdPrefix, StringComparison.Ordinal) && !ids.Contains(id)) ids.Add(id);
            return ids;
        }
    }
}
