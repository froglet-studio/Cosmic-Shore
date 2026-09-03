using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The weekly challenge leaderboard's ROW LIST: rank · avatar · name · time, fastest first,
    /// with the signed-in player's own row marked and the podium tinted.
    ///
    /// <para><b>This panel owns the rows; the modal owns the window.</b> Tabs, the countdown, the
    /// reward panel and the open/close animation belong to
    /// <see cref="WeeklyChallengeLeaderboardModal"/>, which tells this panel which
    /// <see cref="LeaderboardScope"/> to draw. The split is the same one the arcade launch panel
    /// records: a panel that also decided things is a panel every other caller has to fight.</para>
    ///
    /// <para><b>The score column is a TIME</b>, because the challenge is "reach N of something" and
    /// the only thing left to rank is how long it took. Only players who COMPLETED the objective
    /// are on the board at all — see <see cref="WeeklyChallengeLeaderboardService"/> for why a
    /// non-completion earns no entry rather than a slow one.</para>
    ///
    /// <para><b>Every field is optional.</b> A panel that wires only <see cref="rowContainer"/> and
    /// a template still lists the week; one that wires nothing logs nothing and draws nothing. The
    /// pieces of a row are found by NAME inside the template (a child whose name contains <i>rank</i>,
    /// <i>avatar</i>, <i>name</i>/<i>username</i>, <i>score</i>), so the art can be re-laid without
    /// coming back through code — the same adoption the connecting panel's pilot roster uses, for
    /// the same reason: the row count is not known until the fetch answers.</para>
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

        [Tooltip("The row's own background Image — the one the podium colours tint. Left empty, " +
                 "the template's own Image is used.")]
        [SerializeField] Image templateBackground;

        [Header("Header (all optional)")]
        [Tooltip("The week's mode, e.g. 'SCURRY'. The modal usually owns this; wire it here only " +
                 "when the panel is used standalone.")]
        [SerializeField] TMP_Text titleText;

        [Tooltip("Shown while a fetch is in flight, and while the board has nothing in it.")]
        [SerializeField] GameObject emptyState;

        [Tooltip("Optional line inside the empty state saying WHY it is empty — an unconfigured " +
                 "region board and a board nobody has finished yet are different facts.")]
        [SerializeField] TMP_Text emptyStateText;

        [SerializeField] string emptyBoardMessage = "NO TIMES YET — BE THE FIRST";
        [SerializeField] string loadingMessage = "LOADING…";

        [Header("Avatars")]
        [Tooltip("Resolves a row's avatar id to a sprite. An entry carries its icon id in the " +
                 "score's METADATA (stamped at submit), so a row shows a real face only when that " +
                 "player submitted after avatars started travelling; older rows keep the " +
                 "template's art rather than going blank.")]
        [SerializeField] SO_ProfileIconList profileIcons;

        [Header("Look")]
        [Tooltip("The local player's row. Marked by COLOUR rather than by a badge — a row that " +
                 "changes height would break the list's rhythm.")]
        [SerializeField] Color localRowColor = new(0.42f, 0.85f, 1f, 1f);

        [SerializeField] Color rowColor = Color.white;

        [Tooltip("Appended to the local player's name, as in the mock-up's 'THE PLAYER *'.")]
        [SerializeField] string localNameSuffix = " *";

        [Header("Podium")]
        [Tooltip("Row-background tints for ranks 1, 2 and 3, in order. FEWER than three entries " +
                 "is fine — only the ranks listed are tinted. Empty leaves every row on its " +
                 "authored background, which is the correct look for a board with no podium art.")]
        [SerializeField]
        Color[] podiumColors =
        {
            new(1f, 0.84f, 0.25f, 1f),      // gold
            new(0.78f, 0.82f, 0.86f, 1f),   // silver
            new(0.80f, 0.53f, 0.30f, 1f),   // bronze
        };

        [Header("Entry animation")]
        [Tooltip("Rows fade and slide in one after another. 0 disables the whole effect — every " +
                 "row simply appears, which is what a reduced-motion setting wants.")]
        [SerializeField, Range(0f, 0.6f)] float rowFadeDuration = 0.22f;

        [Tooltip("Delay added per row, so the list cascades rather than popping as a block. The " +
                 "cascade is CAPPED (see maxStaggerTotal) so a long list still finishes promptly.")]
        [SerializeField, Range(0f, 0.15f)] float rowStagger = 0.035f;

        [Tooltip("The whole cascade never takes longer than this, however many rows there are. " +
                 "Without the cap a fifty-row board would take two seconds to finish arriving, " +
                 "and the last rows would read as a bug rather than as flourish.")]
        [SerializeField, Range(0.1f, 2f)] float maxStaggerTotal = 0.5f;

        [Tooltip("How small a row starts before settling to full size. 1 disables the swell and " +
                 "leaves the entry as a pure fade.")]
        [SerializeField, Range(0.7f, 1f)] float rowStartScale = 0.94f;

        class Row
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public TMP_Text Rank;
            public Image Avatar;
            public TMP_Text Name;
            public TMP_Text Score;
            public Image Background;
            public Color BackgroundRest;
        }

        readonly List<Row> _rows = new();
        RectTransform _container;
        RectTransform _template;
        string _rankPath, _avatarPath, _namePath, _scorePath, _backgroundPath;
        CancellationTokenSource _cts;

        /// <summary>Which population the next fetch asks for. Set by the modal's tabs.</summary>
        public LeaderboardScope Scope { get; private set; } = LeaderboardScope.World;

        /// <summary>
        /// Set by a driver (the modal) to say "I will tell you when to fetch". It suppresses the
        /// <see cref="OnEnable"/> refresh ONLY.
        ///
        /// <para>Without it the panel and its modal both fetch on open — the panel because
        /// enabling it is normally the whole trigger, the modal because it selects a scope — and
        /// the window costs two network round trips every time it is opened. The panel keeps its
        /// own refresh by default so it still works with nothing driving it.</para>
        /// </summary>
        public bool DrivenExternally { get; set; }

        /// <summary>Raised after a draw with the row count, so the modal can react (a tab badge,
        /// a scroll reset) without polling.</summary>
        public event System.Action<LeaderboardScope, int> OnDrawn;

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
            if (!DrivenExternally) Refresh();
        }

        void OnDisable()
        {
            CancelFetch();
            KillRowTweens();
        }

        void OnDestroy()
        {
            CancelFetch();
            KillRowTweens();
        }

        /// <summary>Switch scope and redraw. A no-op when the scope has not changed AND rows are
        /// already drawn, so a player hammering a tab does not re-fetch on every press.</summary>
        public void SetScope(LeaderboardScope scope, bool forceRefresh = false)
        {
            if (Scope == scope && !forceRefresh && _rows.Count > 0) return;
            Scope = scope;
            Refresh();
        }

        /// <summary>Re-fetch and redraw. Safe to call repeatedly — an in-flight fetch is cancelled.</summary>
        public void Refresh()
        {
            CancelFetch();

            var service = WeeklyChallengeService.Instance;
            if (service == null)
            {
                ClearRows();
                ShowEmpty(emptyBoardMessage);
                return;
            }

            var board = service.Leaderboard;

            // An UNCONFIGURED scope is answered BEFORE the fetch, not after: a board that does not
            // exist and a board nobody has finished both come back empty, and the player deserves
            // to know which one they are looking at.
            if (!board.IsScopeAvailable(Scope))
            {
                ClearRows();
                ShowEmpty(board.UnavailableReason(Scope));
                OnDrawn?.Invoke(Scope, 0);
                return;
            }

            _cts = new CancellationTokenSource();
            FetchAsync(board, Scope, _cts.Token).Forget();
        }

        async UniTaskVoid FetchAsync(
            WeeklyChallengeLeaderboardService board, LeaderboardScope scope, CancellationToken ct)
        {
            ShowEmpty(loadingMessage);

            var entries = await board.FetchAsync(scope, rowCount, ct);
            if (ct.IsCancellationRequested || !this) return;

            // The tab may have moved while the fetch was in flight. Drawing a stale answer under a
            // different heading is worse than drawing nothing.
            if (scope != Scope) return;

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
            KillRowTweens();

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
            {
                Bind(_rows[i], entries[i]);
                PlayRowEntry(_rows[i], i, _rows.Count);
            }

            if (entries.Count == 0) ShowEmpty(emptyBoardMessage);
            else HideEmpty();

            OnDrawn?.Invoke(Scope, entries.Count);
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

            BindAvatar(row, entry);
            BindPodium(row, entry.Rank);
        }

        /// <summary>
        /// The row's face. An entry carries its icon id in the submitted score's METADATA, so a
        /// row has a real avatar only when that player submitted after avatars started travelling.
        ///
        /// <para>An entry without one KEEPS THE TEMPLATE'S SPRITE rather than clearing it: an
        /// <see cref="Image"/> with no sprite draws a solid white rectangle, so "no avatar" would
        /// read as a rendering bug. The fallback is the normal case for old entries, not a failure.</para>
        /// </summary>
        void BindAvatar(Row row, in WeeklyChallengeRanking entry)
        {
            if (!row.Avatar) return;

            var sprite = ResolveAvatarSprite(entry.AvatarId);
            if (sprite) row.Avatar.sprite = sprite;

            row.Avatar.enabled = row.Avatar.sprite;
            row.Avatar.color = Color.white;   // the ART carries the colour; tinting it dyes a face
        }

        Sprite ResolveAvatarSprite(int avatarId)
        {
            if (avatarId < 0 || profileIcons?.profileIcons == null) return null;

            foreach (var icon in profileIcons.profileIcons)
                if (icon.Id == avatarId)
                    return icon.IconSprite;

            return null;
        }

        /// <summary>
        /// The podium tint. Applied to the row's own background Image, and every row that is NOT
        /// on the podium is restored to the colour the TEMPLATE authored — captured once per row
        /// at build time, so a re-draw that moves a player off the podium cannot leave them gold.
        /// </summary>
        void BindPodium(Row row, int rank)
        {
            if (!row.Background) return;

            row.Background.color = podiumColors != null && rank >= 1 && rank <= podiumColors.Length
                ? podiumColors[rank - 1]
                : row.BackgroundRest;
        }

        void RedrawHeader()
        {
            if (!titleText) return;

            var service = WeeklyChallengeService.Instance;
            var challenge = service != null ? service.ThisWeek : default;
            titleText.text = challenge.IsValid
                ? challenge.GameMode.ToString().ToUpperInvariant()
                : "WEEKLY CHALLENGE";
        }

        // ── Empty state ────────────────────────────────────────────────────────

        void ShowEmpty(string message)
        {
            if (emptyState) emptyState.SetActive(true);
            if (emptyStateText) emptyStateText.text = message ?? string.Empty;
        }

        void HideEmpty()
        {
            if (emptyState) emptyState.SetActive(false);
        }

        void ClearRows()
        {
            KillRowTweens();
            foreach (var row in _rows)
                if (row?.Root) Destroy(row.Root.gameObject);
            _rows.Clear();
        }

        // ── Entry animation ────────────────────────────────────────────────────

        /// <summary>
        /// Fade + swell, staggered down the list.
        ///
        /// <para><b>Deliberately NOT a rise.</b> The rows live under a
        /// <see cref="UnityEngine.UI.VerticalLayoutGroup"/>, which owns <c>anchoredPosition</c> and
        /// rewrites it on every layout rebuild — so a position tween is a second writer to a value
        /// the layout considers its own, and the rows snap the first time anything dirties the
        /// layout. Alpha and localScale are both untouched by a layout group (this one has
        /// <c>ChildScale</c> off, so scale does not even feed back into sizing), which makes them
        /// the two channels a row can safely animate wherever it is parented.</para>
        ///
        /// <para>The stagger is DIVIDED DOWN when the list is long enough to exceed
        /// <see cref="maxStaggerTotal"/>, rather than truncated: truncating leaves the tail of a
        /// long board arriving all at once, which reads as the animation giving up.</para>
        /// </summary>
        void PlayRowEntry(Row row, int index, int total)
        {
            if (row?.Root == null) return;

            if (rowFadeDuration <= 0f)
            {
                if (row.Group) row.Group.alpha = 1f;
                row.Root.localScale = Vector3.one;
                return;
            }

            float stagger = rowStagger;
            if (total > 1 && stagger * (total - 1) > maxStaggerTotal)
                stagger = maxStaggerTotal / (total - 1);

            float delay = stagger * index;

            if (row.Group)
            {
                row.Group.alpha = 0f;
                row.Group.DOFade(1f, rowFadeDuration)
                    .SetDelay(delay).SetEase(Ease.OutQuad)
                    .SetUpdate(true).SetLink(row.Root.gameObject);
            }

            if (rowStartScale < 1f)
            {
                row.Root.localScale = Vector3.one * rowStartScale;
                row.Root.DOScale(1f, rowFadeDuration)
                    .SetDelay(delay).SetEase(Ease.OutBack)
                    .SetUpdate(true).SetLink(row.Root.gameObject);
            }
        }

        /// <summary>
        /// Kill every row tween AND snap it to rest. A killed tween leaves its target wherever it
        /// was mid-flight, so a panel closed 40 ms into a cascade would re-open with half its rows
        /// transparent and undersized — the same rule <c>ElementalBarsView</c> follows on disable.
        /// </summary>
        void KillRowTweens()
        {
            foreach (var row in _rows)
            {
                if (row?.Root == null) continue;
                DOTween.Kill(row.Root, complete: false);
                row.Root.localScale = Vector3.one;

                if (row.Group)
                {
                    DOTween.Kill(row.Group, complete: false);
                    row.Group.alpha = 1f;
                }
            }
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
            _backgroundPath = RelativePath(_template, templateBackground);

            _template.gameObject.SetActive(false);
        }

        Row BuildRow()
        {
            if (!_container || !_template) return null;

            var clone = Instantiate(_template, _container);
            clone.gameObject.SetActive(true);
            clone.name = $"Row{_rows.Count}";

            var background = Resolve<Image>(clone, _backgroundPath) ?? clone.GetComponent<Image>();

            var row = new Row
            {
                Root = clone,
                Group = clone.GetComponent<CanvasGroup>() ?? clone.gameObject.AddComponent<CanvasGroup>(),
                Rank = Resolve<TMP_Text>(clone, _rankPath) ?? FindByName<TMP_Text>(clone, "rank"),
                Avatar = Resolve<Image>(clone, _avatarPath) ?? FindByName<Image>(clone, "avatar")
                         ?? FindByName<Image>(clone, "icon") ?? FindByName<Image>(clone, "profile"),
                Name = Resolve<TMP_Text>(clone, _namePath) ?? FindByName<TMP_Text>(clone, "username")
                       ?? FindByName<TMP_Text>(clone, "name"),
                Score = Resolve<TMP_Text>(clone, _scorePath) ?? FindByName<TMP_Text>(clone, "score")
                        ?? FindByName<TMP_Text>(clone, "time") ?? FindByName<TMP_Text>(clone, "value"),
                Background = background,
                BackgroundRest = background ? background.color : Color.white,
            };

            return row;
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
