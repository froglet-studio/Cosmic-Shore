using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using Reflex.Core;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Stands the arena a replay was raced in, inside the menu, so the ghosts fly through the real
    /// course instead of across the Theater's dark stage.
    ///
    /// <para><b>Built on the mode preview's satellite arena, not on a scene load.</b> A mode scene
    /// cannot be loaded beside the menu: Menu_Main is always hosting (the party, the lava lamp), and
    /// a mode scene carries in-scene NetworkObjects, its controller and its spawn chain, all of which
    /// expect to start a match. <see cref="ModePreviewArena"/> already answers "show this mode's
    /// arena without the menu changing": it stands the mode's own cell config as an isolated
    /// satellite cell far from the menu world (its rails and environment, its ecology), and
    /// <see cref="ModePreviewGateCourse"/> stands a gate race's rings from the same
    /// <see cref="RaceCourseSource"/> the match controller builds from. The replay stands exactly
    /// that, at the intensity and config the match used, and plays the recording shifted onto it
    /// (<see cref="ReplayStagePlacement"/>).</para>
    ///
    /// <para><b>The teardown is the preview's pool-safe one</b>: <see cref="ModePreviewArena.BeginStrike"/>
    /// returns pooled prisms to their pool, the remainder is destroyed a slice per frame, then
    /// <see cref="ModePreviewArena.FinishStrike"/> (<c>Docs/ModePreview/ARCHITECTURE.md</c> §4).</para>
    ///
    /// <para>False from <see cref="StandAsync"/> is an ordinary answer - a mode with no preview
    /// definition, no cell to clone, a build that never finishes - and the replay then plays on the
    /// dark stage as before.</para>
    /// </summary>
    public sealed class ReplayArenaStage
    {
        readonly ModePreviewArena _arena = new();
        GameObject _host;
        ModePreviewGateCourse _course;
        bool _striking;

        /// <summary>The arena's centre - where the recorded arena origin lands.</summary>
        public Vector3 Origin => _arena.Origin;

        /// <summary>True once the arena's cell has finished building.</summary>
        public bool IsStanding { get; private set; }

        /// <summary>
        /// Build the arena for <paramref name="mode"/> at <paramref name="origin"/> and wait for it.
        /// <paramref name="recordedConfigName"/> picks the exact cell config the match ran (empty
        /// falls back to the definition's config for <paramref name="intensity"/>). Cancelling strikes
        /// whatever had been built.
        /// </summary>
        public async UniTask<bool> StandAsync(GameModes mode, int intensity, string recordedConfigName, Vector3 origin,
                                              int prismStride, float timeoutSeconds, Container container,
                                              ThemeManagerDataContainerSO theme, int drainPrismsPerFrame,
                                              CancellationToken ct, IReadOnlyList<ReplayGate> recordedGates = null)
        {
            if (IsStanding || _striking || _arena.Cell) return false;

            var library = Resources.Load<ModePreviewLibrarySO>(ModePreviewLibrarySO.ResourcePath);
            var definition = library ? library.Resolve(mode) : null;
            if (!definition)
            {
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Replay] {mode} has no preview definition - no arena, dark stage.");
                return false;
            }

            var config = ResolveConfig(definition, intensity, recordedConfigName);
            var template = Cell.FindNearestActiveCell(Vector3.zero, sceneCellsOnly: true);
            if (!config || !template)
            {
                CSDebug.LogWarning($"[Replay] Cannot stand the {mode} arena: " +
                                   (!config ? "its preview definition has no cell config" : "there is no scene cell to clone") + ".");
                return false;
            }

            try
            {
                if (!_arena.Stand(definition, config, template, template.gameObject, origin, container, intensity, prismStride))
                    return false;

                float elapsed = 0f;
                while (_arena.Cell && _arena.Cell.IsSwappingConfig)
                {
                    if (elapsed >= timeoutSeconds)
                    {
                        CSDebug.LogWarning($"[Replay] The {mode} arena ({config.CellName}) did not finish building in " +
                                           $"{timeoutSeconds:0} s - playing on the dark stage.");
                        await StrikeAsync(drainPrismsPerFrame);
                        return false;
                    }
                    elapsed += Time.unscaledDeltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                if (!_arena.Cell) return false;

                if (recordedGates is { Count: > 0 }) RaiseRecordedCourse(definition.Mode, recordedGates, theme);
                else RaiseCourse(definition, config, intensity, theme);
                IsStanding = true;
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Replay] arena for {mode} ({config.CellName}) standing at {origin}.");
                return true;
            }
            catch (System.OperationCanceledException)
            {
                StrikeAsync(drainPrismsPerFrame).Forget();
                throw;
            }
        }

        /// <summary>
        /// Take the arena down, pool-safely, a slice of prisms per frame. Safe when nothing stands;
        /// runs to the end on its own, so the caller may already be gone.
        /// </summary>
        public async UniTask StrikeAsync(int prismsPerFrame)
        {
            if (_striking) return;
            _striking = true;
            IsStanding = false;
            if (_course) _course.Strike();
            if (_host) Object.Destroy(_host);
            _host = null;
            _course = null;

            var retiring = _arena.BeginStrike();
            try
            {
                if (!retiring) return;
                int slice = Mathf.Max(1, prismsPerFrame);
                var prisms = retiring.GetComponentsInChildren<Prism>(true);
                for (int i = 0; i < prisms.Length; i++)
                {
                    if (prisms[i]) Object.Destroy(prisms[i].gameObject);
                    if ((i + 1) % slice == 0) await UniTask.Yield(PlayerLoopTiming.Update);
                }
                if (retiring) Object.Destroy(retiring);
            }
            finally
            {
                _arena.FinishStrike();
                _striking = false;
            }
        }

        /// <summary>
        /// The rings the match was flown through, exactly as the recording captured them (any gate race,
        /// Skim Race included): the seed a gate race drew, or a guest never saw, does not matter.
        /// </summary>
        void RaiseRecordedCourse(GameModes mode, IReadOnlyList<ReplayGate> gates, ThemeManagerDataContainerSO theme)
        {
            _host = new GameObject($"ReplayStage ({mode})");
            var course = new List<RaceGate>(gates.Count);
            var rings = new List<RaceGateRing>(gates.Count);
            for (int i = 0; i < gates.Count; i++)
            {
                var gate = new RaceGate(_arena.Origin + gates[i].Position, gates[i].Axis, gates[i].Radius);
                course.Add(gate);
                var go = new GameObject($"Gate_{i + 1:00}");
                go.transform.SetParent(_host.transform, false);
                var ring = go.AddComponent<RaceGateRing>();
                ring.Build(i, gate, theme, RecordedRingBloomSeconds, RaceGateRing.FindCoincident(course, rings, i));
                rings.Add(ring);
            }
        }

        const float RecordedRingBloomSeconds = 0.9f;

        /// <summary>
        /// A gate race's rings as the preview stands them - only for a race whose course is a property of
        /// its ARENA (Regatta, Skein), which a replay recorded before rings were captured can still trust.
        /// A seeded race (Switchback, Headlong, ...) drew a course this cannot know, so it stands no rings.
        /// </summary>
        void RaiseCourse(ModePreviewDefinitionSO definition, CellConfigDataSO config, int intensity,
                         ThemeManagerDataContainerSO theme)
        {
            if (RaceCourseSource.For(definition.Mode) == null) return;
            if (definition.Mode is not (GameModes.Regatta or GameModes.Skein)) return;

            _host = new GameObject($"ReplayStage ({definition.Mode})");
            _course = _host.AddComponent<ModePreviewGateCourse>();
            float nucleus = _arena.Cell ? _arena.Cell.ExpectedNucleusWorldRadius : 0f;
            if (!_course.Raise(definition.Mode, intensity, config, nucleus, _arena.Origin, theme)) return;

            if (definition.CourseStructurePrefab)
                _arena.BuildCourseStructure(definition.CourseStructurePrefab, _course.Source, _course.CellLocalCourse, intensity);
        }

        /// <summary>The config the match ran when the recording names it; otherwise the definition's for the intensity.</summary>
        static CellConfigDataSO ResolveConfig(ModePreviewDefinitionSO definition, int intensity, string recordedName)
        {
            if (!string.IsNullOrEmpty(recordedName))
            {
                if (definition.PreviewCellsByIntensity != null)
                    foreach (var config in definition.PreviewCellsByIntensity)
                        if (config && config.name == recordedName) return config;
                if (definition.PreviewCell && definition.PreviewCell.name == recordedName) return definition.PreviewCell;
            }
            return definition.ResolveCell(intensity);
        }
    }
}
