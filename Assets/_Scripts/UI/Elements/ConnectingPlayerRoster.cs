using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The connecting panel's answer to "who else am I waiting for?" — one chip per HUMAN pilot:
    /// that player's avatar, ringed by a halo in their domain colour.
    ///
    /// <para>A chip has two states and they say one thing: <b>waiting</b> — avatar greyed, halo dim
    /// — means that pilot's machine is still building its arena; <b>ready</b> — avatar at full
    /// colour, halo up to the domain's full signal colour and breathing — means they are done. So
    /// the panel never has to explain the wait in prose: the row IS the answer, and the status line
    /// only names it.</para>
    ///
    /// <para><b>AI are absent from the wait.</b> Nothing ever waits on an AI — it has no machine of its own
    /// to finish loading (<see cref="IPlayer.IsArenaReady"/> is true for one by construction) — so AI never
    /// count in <see cref="AllHumansReady"/>. A chip row leaves them out; the ROW MODE (the Rev 33 cell-gate
    /// loading screen's pilots panel, Docs/MENU_REDESIGN_B.md section 99) can list them with
    /// <see cref="showAI"/>, tagged and already ready, because that panel is the match's line-up, not
    /// only the wait.</para>
    ///
    /// <para><b>Row mode</b> is the chip with words: wire any of the template's name, tag, hull line, state
    /// word and pip and they are filled per player (every word authored here). Unwired parts are simply
    /// not drawn, so the chip row is unchanged.</para>
    /// </summary>
    public class ConnectingPlayerRoster : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [Tooltip("Avatar art. Left empty, the HUD's own list is adopted, so the connecting screen " +
                 "and the scoreboard cannot show different faces.")]
        [SerializeField] SO_ProfileIconList profileIcons;

        [Header("Layout")]
        [Tooltip("Where the chips go. Left empty, a descendant named \"PlayerIcons\" is adopted; " +
                 "failing that, this object itself.")]
        [SerializeField] RectTransform container;

        [Tooltip("The chip to clone, once per player. Left empty, the container's first child is " +
                 "used (and hidden). It is a TEMPLATE, never a chip — the row's length is the " +
                 "player count, not one.")]
        [SerializeField] RectTransform entryTemplate;

        [Header("Template parts (optional — resolved by name if left empty)")]
        [Tooltip("THE AVATAR. The Image inside the template that shows the player's picture. " +
                 "Left empty: a child named avatar / icon / profile / portrait, else the " +
                 "template's own Image, else one is created.")]
        [SerializeField] Image templateAvatarImage;

        [Tooltip("THE DOMAIN PLATE — optional, and usually left EMPTY. An Image inside the " +
                 "template to tint with the player's domain colour. A template with a single " +
                 "Image should leave this empty: that Image is the avatar, and the domain colour " +
                 "is carried by the halo behind it. Wiring the same Image here would paint the " +
                 "domain over the player's face.")]
        [SerializeField] Image templateDomainImage;

        [Header("Domain halo")]
        [Tooltip("Sprite for the ring behind the avatar. Left empty, the template's own sprite is " +
                 "borrowed, so the halo is the chip's SHAPE — a rectangular halo behind a round " +
                 "avatar reads as a broken sprite rather than as a glow.")]
        [SerializeField] Sprite haloSprite;

        [SerializeField, Min(1f)] float haloWaitingScale = 1.12f;
        [SerializeField, Min(1f)] float haloReadyScale = 1.32f;
        [SerializeField, Range(0f, 1f)] float haloWaitingAlpha = 0.22f;
        [SerializeField, Range(0f, 1f)] float haloReadyMinAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] float haloReadyMaxAlpha = 0.95f;

        [Tooltip("Halo breath, in cycles per second, once that pilot is ready. Slow — it is a " +
                 "state, not an alarm.")]
        [SerializeField, Min(0f)] float glowPulseHz = 0.7f;

        [Header("Look")]
        [Tooltip("Avatar tint while that pilot is still loading.")]
        [SerializeField] Color waitingAvatarTint = new(0.42f, 0.45f, 0.5f, 0.65f);

        [Tooltip("How far a wired domain PLATE is pulled down while that pilot is loading. " +
                 "Unused when no plate is wired.")]
        [SerializeField, Range(0f, 1f)] float waitingDomainStrength = 0.28f;

        [Tooltip("Seconds the ready state takes to arrive. Continuity of existence applies to UI: " +
                 "a chip that snaps on reads as a chip that was replaced.")]
        [SerializeField, Min(0.01f)] float litRiseSeconds = 0.35f;

        [SerializeField, Min(16f)] float builtEntrySize = 64f;

        [Tooltip("The domain-coloured ring behind each entry. Off for a row whose template is a panel " +
                 "rather than a round avatar: a halo the shape of a row reads as a coloured box.")]
        [SerializeField] bool domainHalo = true;

        [Header("Row mode (optional - the template's words)")]
        [Tooltip("List AI seats too, tagged and already ready. They never count in the wait.")]
        [SerializeField] bool showAI;
        [Tooltip("The player's name.")]
        [SerializeField] TMP_Text templateNameText;
        [Tooltip("\"You\" on the local pilot, \"AI\" on an AI seat, nothing on another human.")]
        [SerializeField] TMP_Text templateBadgeText;
        [Tooltip("The line under the name, from Line Template.")]
        [SerializeField] TMP_Text templateLineText;
        [Tooltip("\"Loading\" until that pilot's arena is built, then \"Ready\".")]
        [SerializeField] TMP_Text templateStateText;
        [Tooltip("The dot beside the state word: the Waiting colour, then the Ready colour.")]
        [SerializeField] Image templatePip;
        [Tooltip("Where a hull's name is looked up for {hull}.")]
        [SerializeField] SO_VesselList vessels;
        [SerializeField] string youWord = "You";
        [SerializeField] string aiWord = "AI";
        [SerializeField] string waitingWord = "Loading";
        [SerializeField] string readyWord = "Ready";
        [Tooltip("The line under the name. {hull} is the pilot's vessel. Empty shows no line.")]
        [SerializeField] string lineTemplate = "{hull}";
        [Tooltip("Optional count of the listed pilots who are ready, e.g. \"2 / 3 Ready\".")]
        [SerializeField] TMP_Text countText;
        [Tooltip("{ready} and {total} are filled. Empty shows nothing.")]
        [SerializeField] string countTemplate = "{ready} / {total} Ready";
        [SerializeField] UIColorToken readyToken = UIColorToken.Cta;
        [SerializeField] UIColorToken waitingToken = UIColorToken.NeutralLightest;
        [SerializeField] UIColorToken youToken = UIColorToken.Selection;
        [SerializeField] UIColorToken otherBadgeToken = UIColorToken.NeutralLightest;

        class Chip
        {
            public IPlayer Player;
            public RectTransform Root;
            public Image Avatar;
            public Image Plate;      // optional authored domain plate
            public Image Halo;       // the domain ring behind the avatar
            public TMP_Text Name, Badge, Line, State;
            public Image Pip;
            public float Lit01;
            public bool Ready;
        }

        readonly List<Chip> _chips = new();
        readonly List<IPlayer> _humans = new();
        readonly List<IPlayer> _shown = new();
        RectTransform _container;
        RectTransform _template;
        Sprite _templateSprite;
        string _avatarPath, _domainPath, _namePath, _badgePath, _linePath, _statePath, _pipPath;
        bool _running;
        int _shownReady = -1;

        /// <summary>Human pilots this row is tracking.</summary>
        public int HumanCount => _humans.Count;

        /// <summary>How many of them have reported their arena built.</summary>
        public int ReadyHumanCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _humans.Count; i++)
                    if (_humans[i] != null && _humans[i].IsArenaReady) n++;
                return n;
            }
        }

        /// <summary>
        /// True when nobody is left to wait for. A roster that knows of no humans at all answers
        /// TRUE — an empty roster must never be able to hold a loading screen shut.
        /// </summary>
        public bool AllHumansReady => ReadyHumanCount >= _humans.Count;

        /// <summary>Announce THIS machine's build. Idempotent; safe to call every frame.</summary>
        public void ReportLocalReady() => gameData?.LocalPlayer?.ReportArenaReady();

        /// <summary>
        /// Hand the roster its sources when it was ENSURED at runtime rather than authored. An
        /// authored reference always wins — this only fills what is empty.
        /// </summary>
        public void AdoptSources(GameDataSO data, SO_ProfileIconList icons)
        {
            if (!gameData) gameData = data;
            if (!profileIcons) profileIcons = icons;
        }

        /// <summary>Start drawing. Rebuilds from the current player set.</summary>
        public void Begin()
        {
            _running = true;
            ResolveContainer();
            EnsureTemplate();
            Rebuild();
        }

        /// <summary>Stop drawing (the panel is coming down).</summary>
        public void End() => _running = false;

        void Awake()
        {
            ResolveContainer();
            // Hidden at AWAKE, not at the first Begin: the template is authored ACTIVE so it can be
            // seen and laid out in the editor, and left active it is a stray chip wearing a blank
            // sprite that shows on screen before the first player is known.
            EnsureTemplate();
        }

        void Update()
        {
            if (!_running) return;

            if (RosterChanged()) Rebuild();

            float dt = Time.unscaledDeltaTime;
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowPulseHz * Mathf.PI * 2f);

            int readyCount = 0;
            for (int i = 0; i < _chips.Count; i++)
            {
                TickChip(_chips[i], dt, breath);
                if (_chips[i].Ready) readyCount++;
            }
            DrawCount(readyCount);
        }

        static bool IsReady(IPlayer player) => player != null && (player.IsInitializedAsAI || player.IsArenaReady);

        // The listed pilots' count, written only when it changes.
        void DrawCount(int ready)
        {
            if (!countText || ready == _shownReady) return;
            _shownReady = ready;
            countText.text = TemplateText.Fill(countTemplate, "ready", ready.ToString(), "total", _chips.Count.ToString());
            countText.color = Theme(ready >= _chips.Count && _chips.Count > 0 ? readyToken : waitingToken);
        }

        static Color Theme(UIColorToken token) => UITheme.Resolve(null, token);

        void TickChip(Chip chip, float deltaTime, float breath01)
        {
            if (chip?.Root == null) return;

            bool ready = IsReady(chip.Player);
            if (ready != chip.Ready)
            {
                chip.Ready = ready;
                DrawState(chip);
            }
            chip.Lit01 = Mathf.MoveTowards(chip.Lit01, ready ? 1f : 0f, deltaTime / litRiseSeconds);

            Color domain = DomainColor(chip.Player);

            if (chip.Avatar)
                chip.Avatar.color = Color.Lerp(waitingAvatarTint, Color.white, chip.Lit01);

            if (chip.Plate)
            {
                var dim = domain * waitingDomainStrength;
                dim.a = domain.a;
                chip.Plate.color = Color.Lerp(dim, domain, chip.Lit01);
            }

            if (chip.Halo)
            {
                float readyAlpha = Mathf.Lerp(haloReadyMinAlpha, haloReadyMaxAlpha, breath01);
                var halo = domain;
                halo.a = Mathf.Lerp(haloWaitingAlpha, readyAlpha, chip.Lit01);
                chip.Halo.color = halo;
                chip.Halo.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(haloWaitingScale, haloReadyScale, chip.Lit01);
            }
        }

        // ── Roster ──────────────────────────────────────────────────────────

        bool Listed(IPlayer p) => p != null && (showAI || !p.IsInitializedAsAI);

        bool RosterChanged()
        {
            int seen = 0;
            var players = gameData != null ? gameData.Players : null;
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (!Listed(p)) continue;
                    if (seen >= _shown.Count || !ReferenceEquals(_shown[seen], p)) return true;
                    seen++;
                }
            }
            return seen != _shown.Count;
        }

        void Rebuild()
        {
            _humans.Clear();
            _shown.Clear();
            var players = gameData != null ? gameData.Players : null;
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (p != null && !p.IsInitializedAsAI) _humans.Add(p);
                    if (Listed(p)) _shown.Add(p);
                }

            while (_chips.Count > _shown.Count)
            {
                var last = _chips[^1];
                _chips.RemoveAt(_chips.Count - 1);
                if (last?.Root) Destroy(last.Root.gameObject);
            }
            while (_chips.Count < _shown.Count)
            {
                var chip = BuildChip();
                if (chip == null) break;
                _chips.Add(chip);
            }

            _shownReady = -1;
            for (int i = 0; i < _chips.Count; i++)
            {
                var chip = _chips[i];
                chip.Player = _shown[i];
                chip.Ready = IsReady(_shown[i]);
                chip.Lit01 = chip.Ready ? 1f : 0f;
                ApplyAvatar(chip, _shown[i]);
                ApplyWords(chip, _shown[i]);
                DrawState(chip);
                if (chip.Root) chip.Root.gameObject.SetActive(true);
                TickChip(chip, 0f, 1f);
            }
        }

        // Row mode: the name, the tag and the hull line. Fixed for a player, so written on (re)build only.
        void ApplyWords(Chip chip, IPlayer player)
        {
            if (chip.Name) chip.Name.text = player?.Name ?? string.Empty;
            if (chip.Badge)
            {
                bool you = player != null && player.IsLocalPilot;
                bool ai = player != null && player.IsInitializedAsAI;
                chip.Badge.text = you ? youWord : ai ? aiWord : string.Empty;
                chip.Badge.color = Theme(you ? youToken : otherBadgeToken);
                chip.Badge.gameObject.SetActive(!string.IsNullOrEmpty(chip.Badge.text));
            }
            if (chip.Line) chip.Line.text = TemplateText.Fill(lineTemplate, "hull", HullName(player));
        }

        // Row mode: the state word and the pip.
        void DrawState(Chip chip)
        {
            var c = Theme(chip.Ready ? readyToken : waitingToken);
            if (chip.State)
            {
                chip.State.text = chip.Ready ? readyWord : waitingWord;
                chip.State.color = c;
            }
            if (chip.Pip) chip.Pip.color = c;
        }

        string HullName(IPlayer player)
        {
            var status = player?.Vessel?.VesselStatus;
            if (status == null) return string.Empty;
            var hull = status.VesselType;
            if (vessels != null && vessels.TryGetVesselByClass(hull, out var so, reportMissing: false) &&
                !string.IsNullOrEmpty(so.Name))
                return so.Name;
            return hull.ToString();
        }

        /// <summary>
        /// Put the player's face on the chip.
        ///
        /// <para>A resolved sprite wins; an UNRESOLVED one leaves the template's authored sprite in
        /// place rather than clearing it, because an Image with no sprite draws a solid WHITE
        /// RECTANGLE — the blank box that reads as a broken chip. If there is no sprite either way
        /// the Image is switched off, for the same reason: a graphic with nothing to show must show
        /// nothing, not a white box.</para>
        /// </summary>
        void ApplyAvatar(Chip chip, IPlayer player)
        {
            if (chip?.Avatar == null) return;

            var sprite = AvatarSprite(player);
            if (sprite) chip.Avatar.sprite = sprite;
            chip.Avatar.enabled = chip.Avatar.sprite;
        }

        // ── Building ────────────────────────────────────────────────────────

        void ResolveContainer()
        {
            if (_container) return;
            _container = container;
            if (!_container)
            {
                RectTransform named = null;
                foreach (var t in GetComponentsInChildren<RectTransform>(true))
                    if (t != transform && t.name == "PlayerIcons") { named = t; break; }
                _container = named;
            }
            if (!_container) _container = transform as RectTransform;
        }

        void EnsureTemplate()
        {
            if (_template) return;
            _template = entryTemplate;

            if (!_template && _container && _container.childCount > 0)
                _template = _container.GetChild(0) as RectTransform;

            if (!_template) return;

            // Remember what the authored chip LOOKS like before it is hidden: its own sprite is the
            // halo's shape, and the wired parts are recorded as PATHS because the references point
            // at the template, and every chip is a clone with its own copies of them.
            var own = _template.GetComponent<Image>();
            _templateSprite = own ? own.sprite : null;
            _avatarPath = RelativePath(_template, templateAvatarImage);
            _domainPath = RelativePath(_template, templateDomainImage);
            _namePath = RelativePath(_template, templateNameText);
            _badgePath = RelativePath(_template, templateBadgeText);
            _linePath = RelativePath(_template, templateLineText);
            _statePath = RelativePath(_template, templateStateText);
            _pipPath = RelativePath(_template, templatePip);

            // Hidden, not destroyed: it is the source the chips are cut from, and a layout group
            // ignores an inactive child, so it costs the row nothing.
            _template.gameObject.SetActive(false);
        }

        /// <summary>
        /// Build one chip as a WRAPPER holding the halo and the visual, in that order.
        ///
        /// <para>The wrapper is not ceremony. A UGUI graphic always draws before its own children,
        /// so nothing parented under the authored chip can draw BEHIND it — and the halo has to be
        /// behind. Wrapping gives it a sibling slot under the visual, which is the only place it
        /// can be, and costs the layout group nothing: it lays out wrappers exactly as it laid out
        /// chips.</para>
        /// </summary>
        Chip BuildChip()
        {
            if (!_container) return null;

            var wrapperGo = new GameObject($"PlayerChip{_chips.Count}", typeof(RectTransform));
            var wrapper = (RectTransform)wrapperGo.transform;
            wrapper.SetParent(_container, false);
            wrapper.sizeDelta = _template ? _template.sizeDelta : new Vector2(builtEntrySize, builtEntrySize);

            var chip = new Chip { Root = wrapper };
            if (domainHalo) chip.Halo = BuildHalo(wrapper);

            RectTransform visual;
            if (_template)
            {
                visual = Instantiate(_template, wrapper);
                visual.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("Visual", typeof(RectTransform));
                visual = (RectTransform)go.transform;
                visual.SetParent(wrapper, false);
            }
            visual.name = "Visual";
            Stretch(visual, 0f);
            visual.localScale = Vector3.one;

            // An authored DOMAIN plate only exists if one was wired or named. It is deliberately
            // NOT inferred from "the template's only Image" - that Image is the player's picture,
            // and tinting it would paint the domain colour over their face (and show as a blank
            // coloured box before the avatar resolves).
            chip.Plate = Resolve(visual, _domainPath) ?? FindImage(visual, "domain");

            chip.Avatar = Resolve(visual, _avatarPath)
                          ?? FindImage(visual, "avatar") ?? FindImage(visual, "icon")
                          ?? FindImage(visual, "profile") ?? FindImage(visual, "portrait");

            // The template's own Image takes whichever role is LEFT OVER. Both directions matter,
            // and the second one is the white box: a template whose ROOT is a plate with the avatar
            // as a child resolves the avatar by name and leaves the root unclaimed - so it renders
            // its authored sprite, untinted, as a white backing behind every chip. A chip has
            // exactly two layers; neither may be left unowned.
            var own = visual.GetComponent<Image>();
            if (!chip.Avatar)
                chip.Avatar = own && own != chip.Plate ? own : BuildAvatar(visual);
            else if (!chip.Plate && own && own != chip.Avatar)
                chip.Plate = own;

            if (chip.Avatar)
            {
                chip.Avatar.raycastTarget = false;
                chip.Avatar.preserveAspect = true;
            }
            if (chip.Plate) chip.Plate.raycastTarget = false;

            chip.Name = ResolveText(visual, _namePath);
            chip.Badge = ResolveText(visual, _badgePath);
            chip.Line = ResolveText(visual, _linePath);
            chip.State = ResolveText(visual, _statePath);
            chip.Pip = Resolve(visual, _pipPath);
            return chip;
        }

        static TMP_Text ResolveText(RectTransform clone, string path)
        {
            if (path == null || !clone) return null;
            var found = path.Length == 0 ? clone : clone.Find(path);
            return found ? found.GetComponent<TMP_Text>() : null;
        }

        Image BuildAvatar(RectTransform visual)
        {
            var go = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(visual, false);
            Stretch(rt, 0.1f);
            rt.SetAsLastSibling();
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        Image BuildHalo(RectTransform wrapper)
        {
            var go = new GameObject("DomainHalo", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(wrapper, false);
            Stretch(rt, 0f);
            rt.localScale = Vector3.one * haloWaitingScale;

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = haloSprite ? haloSprite : _templateSprite;
            // No sprite means a solid rectangle, which behind a round avatar is a white box rather
            // than a halo. Better no halo than a box.
            img.enabled = img.sprite;
            img.color = new Color(1f, 1f, 1f, 0f);
            return img;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = new Vector2(inset, inset);
            rt.anchorMax = new Vector2(1f - inset, 1f - inset);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            if (!t) return null;                       // not under the template
            parts.Reverse();
            return string.Join("/", parts);
        }

        static Image Resolve(RectTransform clone, string path)
        {
            if (path == null || !clone) return null;
            if (path.Length == 0) return clone.GetComponent<Image>();
            var found = clone.Find(path);
            return found ? found.GetComponent<Image>() : null;
        }

        static Image FindImage(Transform root, string nameFragment)
        {
            foreach (var img in root.GetComponentsInChildren<Image>(true))
                if (img.transform != root &&
                    img.name.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return img;
            return null;
        }

        // ── Sources ─────────────────────────────────────────────────────────

        Sprite AvatarSprite(IPlayer player)
        {
            if (profileIcons == null || profileIcons.ProfileIcons == null ||
                profileIcons.ProfileIcons.Count == 0)
                return null;

            int id = player?.AvatarId ?? 0;
            foreach (var icon in profileIcons.ProfileIcons)
                if (icon.Id == id) return icon.IconSprite;
            return profileIcons.ProfileIcons[0].IconSprite;
        }

        /// <summary>
        /// The pilot's domain at FULL signal strength — the accessor the HUD's own domain surfaces
        /// read, so a chip and a score column can never disagree about what Ruby looks like. Read
        /// live rather than snapshotted: a domain can change right up to the launch.
        /// </summary>
        Color DomainColor(IPlayer player)
        {
            var theme = gameData != null ? gameData.ThemeManagerData : null;
            if (theme == null || theme.ColorSet == null) return Color.white;
            return theme.ColorSet.GetDomainSignalColor(player?.Domain ?? Domains.Blue);
        }
    }
}
