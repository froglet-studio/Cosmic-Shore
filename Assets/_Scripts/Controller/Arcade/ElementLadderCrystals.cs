using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The standard element ladder's I3 rung, as a platform default (Garrett, 2026-11-16; <see cref="ElementLadder"/>,
    /// Docs/ElementalLoop/PLAYBOOK.md step 2): from I3 up, every team in the match gets its OWN crystals of the game's
    /// element on the map, each taken only by that team (<see cref="ElementalCrystalImpactor.RestrictTo"/>), regrowing a
    /// while after it is taken. Harvest's I3 (Shombith, 2026-11-10) generalised; Harvest and Shatterplane keep their
    /// own and opt out (<see cref="MultiplayerMiniGameControllerBase.LaysOwnLadderCrystals"/>).
    ///
    /// <para><b>Local, identical on every peer</b>, like Harvest's: laid from the countdown's ClientRpc with a seeded
    /// stream per team (the scene, the intensity and the team's enum value), so every machine places the same crystals
    /// and nothing replicates. Where they go is the mode's call
    /// (<see cref="MultiplayerMiniGameControllerBase.CollectLadderCrystalPoints"/>): a gate race lays them beside its
    /// rings, so taking one is a detour the racing AI's learned gene decides (<see cref="RaceAICrystalGenesSO"/>);
    /// anything else scatters them through the cell between the nucleus and the membrane.</para>
    ///
    /// <para>Never on an arena card (one intensity, own balance), never at I1-I2, never when the game names no single
    /// element (<see cref="ModePreviewLibrarySO.TryGenres"/>), and never in a mode scored on crystals, where a team
    /// crystal would be a free point.</para>
    /// </summary>
    public class ElementLadderCrystals : MonoBehaviour
    {
        sealed class Slot
        {
            public Vector3 Position;
            public Domains Team;
            public Crystal Crystal;
            public ElementalCrystalImpactor Impactor;
            public float RegrowAt = -1f;
        }

        static readonly HashSet<Crystal> s_ladderCrystals = new();

        /// <summary>True for a crystal this rung minted (they have no <c>CrystalManager</c>, so the racing AI's crystal
        /// search asks this instead).</summary>
        public static bool IsLadderCrystal(Crystal crystal) => crystal && s_ladderCrystals.Contains(crystal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => s_ladderCrystals.Clear();

        readonly List<Slot> _slots = new();
        readonly List<Vector3> _points = new();
        MultiplayerMiniGameControllerBase _owner;
        GameDataSO _gameData;
        Element _element;
        float _nextCheck;

        /// <summary>The rung's component on <paramref name="owner"/>'s object, created on first use.</summary>
        public static ElementLadderCrystals EnsureOn(MultiplayerMiniGameControllerBase owner, GameDataSO gameData)
        {
            if (!owner.TryGetComponent<ElementLadderCrystals>(out var rung))
                rung = owner.gameObject.AddComponent<ElementLadderCrystals>();
            rung._owner = owner;
            rung._gameData = gameData;
            return rung;
        }

        /// <summary>Clear any crystals from a previous turn and lay this match's, when the ladder says to.</summary>
        public void Lay()
        {
            Clear();
            if (!_owner || _gameData == null || _owner.LaysOwnLadderCrystals || _gameData.IsArenaMatch) return;

            int intensity = _gameData.SelectedIntensity != null ? _gameData.SelectedIntensity.Value : 1;
            int perTeam = _owner.LadderCrystalsPerTeam;
            if (!ElementLadder.HasTeamCrystals(intensity) || perTeam <= 0) return;
            if (!ModePreviewLibrarySO.TryGenres(_gameData.GameMode, out _element, out var secondary)) return;
            // A mode SCORED on crystals would count these as points (Skim Race's count ends its race): it stands down.
            if (ModePreviewLibrarySO.ObjectiveMetricOf(_gameData.GameMode) is ScoringMetric.Crystals
                or ScoringMetric.OmniCrystals or ScoringMetric.ElementalCrystals) return;
            // A two-element game (Brood Rush is the only one, and it is an arena card) has no one element to lay.
            if (secondary != Element.None || ElementalCrystalSetSO.Load() == null) return;
            if (_element is not (Element.Mass or Element.Charge or Element.Space or Element.Time)) return;

            int sceneHash = StableHash(gameObject.scene.name);
            foreach (var team in TeamsPresent())
            {
                _points.Clear();
                var rng = new System.Random(unchecked(sceneHash * 31 + intensity * 7919 + (int)team));
                _owner.CollectLadderCrystalPoints(team, perTeam, rng, _points);
                for (int i = 0; i < _points.Count; i++)
                {
                    var slot = new Slot { Position = _points[i], Team = team };
                    _slots.Add(slot);
                    Mint(slot);
                }
            }
        }

        /// <summary>Destroy every crystal this rung laid.</summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var c = _slots[i].Crystal;
                if (!c) continue;
                s_ladderCrystals.Remove(c);
                Destroy(c.gameObject);
            }
            _slots.Clear();
        }

        void OnDestroy() => Clear();

        void Update()
        {
            if (_slots.Count == 0 || !_owner || Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.5f;

            float now = Time.time;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Crystal && slot.Impactor && !slot.Impactor.HasBeenCollected) continue;
                if (slot.RegrowAt < 0f) { slot.RegrowAt = now + _owner.LadderCrystalRegrowSeconds; continue; }
                // A collected crystal destroys itself at the end of its capture; one still flying is left to finish.
                if (now < slot.RegrowAt || slot.Crystal) continue;
                Mint(slot);
            }
        }

        void Mint(Slot slot)
        {
            var set = ElementalCrystalSetSO.Load();
            var prefab = set != null ? set.GetPrefab(_element) : null;
            if (prefab == null) return;

            var crystal = Instantiate(prefab, slot.Position, Quaternion.identity);
            // One size, one petal, like every elemental crystal. Sized BEFORE Start(): Crystal stamps its value from
            // lossyScale.
            crystal.transform.localScale = Vector3.one * ElementalCrystalSetSO.CrystalWorldScale;
            crystal.enabled = true;
            crystal.gameObject.SetActive(true);
            crystal.ApplyDomainPreview(slot.Team);

            var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
            impactor.Crystal = crystal;
            impactor.RestrictTo(slot.Team);
            if (set.CollectionEffects is { Length: > 0 })
                impactor.SetCollectionEffects(set.CollectionEffects);
            crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);

            if (slot.Crystal) s_ladderCrystals.Remove(slot.Crystal);
            s_ladderCrystals.Add(crystal);
            slot.Crystal = crystal;
            slot.Impactor = impactor;
            slot.RegrowAt = -1f;
        }

        /// <summary>The teams in the match, in enum order, so every peer lays the same crystals.</summary>
        List<Domains> TeamsPresent()
        {
            var teams = new List<Domains>();
            var players = _gameData.Players;
            if (players == null) return teams;
            for (int i = 0; i < players.Count; i++)
            {
                var d = players[i]?.Domain ?? Domains.Blue;
                if (d == Domains.Blue || teams.Contains(d)) continue;
                teams.Add(d);
            }
            teams.Sort();
            return teams;
        }

        /// <summary>FNV-1a: the same on every peer and every run, unlike <c>string.GetHashCode</c>.</summary>
        static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                if (s != null)
                    for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619; }
                return (int)h;
            }
        }

        /// <summary>
        /// The default placement: <paramref name="count"/> points spread through the shell between
        /// <paramref name="innerRadius"/> and <paramref name="outerRadius"/> round <paramref name="centre"/>, drawn from
        /// <paramref name="rng"/>. Pure, so it is tested without a scene.
        /// </summary>
        public static void ScatterInShell(Vector3 centre, float innerRadius, float outerRadius, int count,
                                          System.Random rng, List<Vector3> into)
        {
            if (rng == null || into == null || count <= 0 || outerRadius <= 0f) return;
            innerRadius = Mathf.Clamp(innerRadius, 0f, outerRadius);
            for (int i = 0; i < count; i++)
            {
                // Uniform direction; radius uniform in VOLUME between the two shells.
                double z = rng.NextDouble() * 2.0 - 1.0;
                double a = rng.NextDouble() * 2.0 * Mathf.PI;
                double r = System.Math.Sqrt(1.0 - z * z);
                var dir = new Vector3((float)(r * System.Math.Cos(a)), (float)(r * System.Math.Sin(a)), (float)z);
                double i3 = innerRadius * (double)innerRadius * innerRadius;
                double o3 = outerRadius * (double)outerRadius * outerRadius;
                float radius = (float)System.Math.Pow(i3 + rng.NextDouble() * (o3 - i3), 1.0 / 3.0);
                into.Add(centre + dir * radius);
            }
        }
    }
}
