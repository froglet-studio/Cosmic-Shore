using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The weekly challenge leaderboard: rank · avatar · name · time, fastest first, with the
    /// signed-in player's own row marked.
    ///
    /// <para><b>The score column is a TIME</b>, because the challenge is "reach N of something" and
    /// the only thing left to rank is how long it took. Only players who COMPLETED the objective
    /// are on the board at all — see <see cref="WeeklyChallengeLeaderboardService"/> for why a
    /// non-completion earns no entry rather than a slow one.</para>
    ///
    /// <para><b>Every field is optional.</b> A panel that wires only <see cref="rowContainer"/> and
    /// a template still lists the week; one that wires nothing logs nothing and draws nothing. The
    /// pieces of a row are found by NAME inside the template (a child whose name contains <i>rank</i>,
    /// <i>avatar</i>, <i>name</i>, <i>score</i>), so the art can be re-laid without coming back
    /// through code — the same adoption the connecting panel's pilot roster uses, for the same
    /// reason: the row count is not known until the fetch answers.</para>
    ///
    /// <para>Rewards are deliberately absent. A reward system is being built separately; this panel
    /// ranks and nothing else, and the tooltip in the mock-up is that system's surface, not this
    /// one's.</para>
    /// </summary>
    public class WeeklyChallengeLeaderboardPanel : MonoBehaviour
    {
        [Header("Rows")]
        [Tooltip("Where the rows go. Left empty, this object's own transform is used.")]
        [SerializeField] RectTransform rowContainer;

        [Tooltip("The row to clone, once per entry. Left empty, the container's first child is " +
                 "used (and hidden). It is a TEMPLATE, never a row.")]
        [SerializeField] RectTransform rowTemplate;

        [Tooltip("Rows fetched and drawn. The mock-up shows four; ten leaves room to scroll.")]
        [SerializeField, Range(1, 50)] int rowCount = 10;

        [Header("Row parts (optional — resolved by name inside the template)")]
        [SerializeField] TMP_Text templateRank;
        [SerializeField] Image templateAvatar;
        [SerializeField] TMP_Text templateName;
        [SerializeField] TMP_Text templateScore;

        [Header("Header (all optional)")]
        [Tooltip("'DAILY CHALLENGE' in the mock-up — now the week's mode, e.g. 'SCURRY'.")]
        [SerializeField] TMP_Text titleText;

        [Tooltip("'Time left: 12:28:36' — counts down to the next UTC Monday.")]
        [SerializeField] TMP_Text timeLeftText;

        [SerializeField] string timeLeftPrefix = "Time left: ";

        [Tooltip("Shown while a fetch is in flight, and while the board has nothing in it.")]
        [SerializeField] GameObject emptyState;

        [Header("Look")]
        [Tooltip("The local player's row. Marked by COLOUR rather than by a badge - the mock-up " +
                 "marks it with an asterisk on the name, and a row that changes height would " +
                 "break the list's rhythm.")]
        [SerializeField] Color localRowColor = new(0.42f, 0.85f, 1f, 1f);

        [SerializeField] Color rowColor = Color.white;

        [Tooltip("Appended to the local player's name, as in the mock-up's 'THE PLAYER *'.")]
        [SerializeField] string localNameSuffix = " *";

        class Row
        {
            public RectTransform Root;
            public TMP_Text Rank;
            public Image Avatar;
            public TMP_Text Name;
            public TMP_Text Score;
        }

        readonly List<Row> _rows = new();
        RectTransform _container;
        RectTransform _template;
        string _rankPath, _avatarPath, _namePath, _scorePath;
        CancellationTokenSource _cts;
        float _countdownTimer;

        void Awake()
        {
            ResolveContainer();
            // Hidden at AWAKE, not at the first fetch: the template is authored ACTIVE so it can be
            // laid out in the editor, and left active it is a stray row on screen.
            EnsureTemplate();
        }

        void OnEnable()
        {
            RedrawHeader();
            Refresh();
        }

        void OnDisable() => CancelFetch();
        void OnDestroy() => CancelFetch();

        void Update()
        {
            // 1 Hz: the countdown displays whole seconds, so anything faster is work nobody sees.
            _countdownTimer += Time.unscaledDeltaTime;
            if (_countdownTimer < 1f) return;
            _countdownTimer = 0f;
            RedrawHeader();
        }

        /// <summary>Re-fetch and redraw. Safe to call repeatedly — an in-flight fetch is cancelled.</summary>
        public void Refresh()
        {
            CancelFetch();

            var service = WeeklyChallengeService.Instance;
            if (service == null)
            {
                SetEmptyState(true);
                return;
            }

            _cts = new CancellationTokenSource();
            FetchAsync(service, _cts.Token).Forget();
        }

        async UniTaskVoid FetchAsync(WeeklyChallengeService service, CancellationToken ct)
        {
            SetEmptyState(true);

            var entries = await service.Leaderboard.FetchTopAsync(rowCount, ct);
            if (ct.IsCancellationRequested || !this) return;

            Draw(entries);
        }

        void CancelFetch()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        // ── Drawing ────────────────────────────────────────────────────────────

        void Draw(List<WeeklyChallengeRanking> entries)
        {
            EnsureTemplate();

            while (_rows.Count > entries.Count)
            {
                var last = _rows[^1];
                _rows.RemoveAt(_rows.Count - 1);
                if (last?.Root) Destroy(last.Root.gameObject);
            }
            while (_rows.Count < entries.Count)
            {
                var row = BuildRow();
                if (row == null) break;
                _rows.Add(row);
            }

            for (int i = 0; i < _rows.Count; i++)
                Bind(_rows[i], entries[i]);

            SetEmptyState(entries.Count == 0);
        }

        void Bind(Row row, in WeeklyChallengeRanking entry)
        {
            if (row?.Root == null) return;
            row.Root.gameObject.SetActive(true);

            var tint = entry.IsLocalPlayer ? localRowColor : rowColor;

            if (row.Rank)
            {
                row.Rank.text = entry.Rank > 0 ? entry.Rank.ToString() : "-";
                row.Rank.color = tint;
            }
            if (row.Name)
            {
                row.Name.text = entry.IsLocalPlayer
                    ? entry.PlayerName + localNameSuffix
                    : entry.PlayerName;
                row.Name.color = tint;
            }
            if (row.Score)
            {
                row.Score.text = entry.FormatTime();
                row.Score.color = tint;
            }
            if (row.Avatar)
            {
                // NO AVATAR TRAVELS WITH A LEADERBOARD ENTRY. UGS holds a player id, a name, a
                // rank and a score - not a profile - so there is nothing to look up in
                // SO_ProfileIconList, which is why this panel deliberately does not reference it.
                // The row keeps whatever the template authored rather than clearing the sprite: an
                // Image with no sprite draws a solid white rectangle. Real per-player avatars need
                // a second lookup (Friends presence, or an avatar id mirrored into the score's
                // metadata at submit time) and are a follow-up, not a silent blank.
                row.Avatar.enabled = row.Avatar.sprite;
                row.Avatar.color = tint;
            }
        }

        void RedrawHeader()
        {
            var service = WeeklyChallengeService.Instance;

            if (titleText)
            {
                var challenge = service != null ? service.ThisWeek : default;
                titleText.text = challenge.IsValid
                    ? challenge.GameMode.ToString().ToUpperInvariant()
                    : "WEEKLY CHALLENGE";
            }

            if (timeLeftText)
            {
                timeLeftText.text = service != null
                    ? timeLeftPrefix + WeeklyChallengeCard.FormatCountdown(service.TimeUntilNextChallenge)
                    : string.Empty;
            }
        }

        void SetEmptyState(bool empty)
        {
            if (emptyState) emptyState.SetActive(empty);
        }

        // ── Building ───────────────────────────────────────────────────────────

        void ResolveContainer()
        {
            if (_container) return;
            _container = rowContainer ? rowContainer : transform as RectTransform;
        }

        void EnsureTemplate()
        {
            if (_template) return;
            ResolveContainer();

            _template = rowTemplate;
            if (!_template && _container && _container.childCount > 0)
                _template = _container.GetChild(0) as RectTransform;

            if (!_template) return;

            _rankPath = RelativePath(_template, templateRank);
            _avatarPath = RelativePath(_template, templateAvatar);
            _namePath = RelativePath(_template, templateName);
            _scorePath = RelativePath(_template, templateScore);

            _template.gameObject.SetActive(false);
        }

        Row BuildRow()
        {
            if (!_container || !_template) return null;

            var clone = Instantiate(_template, _container);
            clone.gameObject.SetActive(true);
            clone.name = $"Row{_rows.Count}";

            return new Row
            {
                Root = clone,
                Rank = Resolve<TMP_Text>(clone, _rankPath) ?? FindByName<TMP_Text>(clone, "rank"),
                Avatar = Resolve<Image>(clone, _avatarPath) ?? FindByName<Image>(clone, "avatar")
                         ?? FindByName<Image>(clone, "icon") ?? FindByName<Image>(clone, "profile"),
                Name = Resolve<TMP_Text>(clone, _namePath) ?? FindByName<TMP_Text>(clone, "name"),
                Score = Resolve<TMP_Text>(clone, _scorePath) ?? FindByName<TMP_Text>(clone, "score")
                        ?? FindByName<TMP_Text>(clone, "time") ?? FindByName<TMP_Text>(clone, "value"),
            };
        }

        /// <summary>Path of <paramref name="child"/> under <paramref name="root"/>; "" = the root
        /// itself; null when it is not under the root at all.</summary>
        static string RelativePath(Transform root, Component child)
        {
            if (!root || !child) return null;
            var t = child.transform;
            if (t == root) return string.Empty;

            var parts = new List<string>();
            while (t && t != root)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            if (!t) return null;
            parts.Reverse();
            return string.Join("/", parts);
        }

        static T Resolve<T>(RectTransform clone, string path) where T : Component
        {
            if (path == null || !clone) return null;
            if (path.Length == 0) return clone.GetComponent<T>();
            var found = clone.Find(path);
            return found ? found.GetComponent<T>() : null;
        }

        static T FindByName<T>(Transform root, string fragment) where T : Component
        {
            foreach (var c in root.GetComponentsInChildren<T>(true))
                if (c.name.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            return null;
        }
    }
}
