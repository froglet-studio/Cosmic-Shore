using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The connecting panel's answer to "who else am I waiting for?" — one chip per HUMAN pilot,
    /// each carrying that player's avatar over their domain colour.
    ///
    /// <para>A chip has exactly two states and they say one thing: <b>greyed</b> means that pilot's
    /// machine is still building its arena; <b>lit</b> — avatar at full colour, domain colour up,
    /// glow behind it — means they are done. So the panel never has to explain the wait in prose:
    /// the row IS the answer, and the status line only names it.</para>
    ///
    /// <para><b>AI are deliberately absent.</b> The row exists to show what is being waited on, and
    /// nothing ever waits on an AI — it has no machine of its own to finish loading
    /// (<see cref="IPlayer.IsArenaReady"/> is true for one by construction). A row that listed them
    /// would show four chips of which two could never change, which reads as two players who never
    /// arrive.</para>
    ///
    /// <para>The chips are built from a TEMPLATE if the panel authors one (the first child of the
    /// container), so an art pass is honoured rather than overwritten; with no template it builds a
    /// plain one. Either way the pieces are found by name — a child named *domain*, *avatar*,
    /// *glow* — falling back to sensible defaults, because the alternative is a serialized
    /// reference per chip on a row whose length is not known until the match starts.</para>
    /// </summary>
    public class ConnectingPlayerRoster : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [Tooltip("Avatar art. Falls back to the first icon in the list for an unknown id, and to " +
                 "no sprite at all when nothing is wired — a chip with no avatar still carries its " +
                 "domain colour and its ready state, which is the part being read.")]
        [SerializeField] SO_ProfileIconList profileIcons;

        [Header("Layout")]
        [Tooltip("Where the chips go. Left empty, a descendant named \"PlayerIcons\" is adopted; " +
                 "failing that, this object itself.")]
        [SerializeField] RectTransform container;

        [Tooltip("Optional authored chip to clone. Left empty, the container's first child is used " +
                 "as the template (and hidden); with no children at all, a plain chip is built.")]
        [SerializeField] RectTransform entryTemplate;

        [SerializeField, Min(16f)] float builtEntrySize = 64f;

        [Header("Look")]
        [Tooltip("Avatar tint while that pilot is still loading.")]
        [SerializeField] Color waitingAvatarTint = new(0.42f, 0.45f, 0.5f, 0.65f);

        [Tooltip("How far the domain colour is pulled down while that pilot is still loading. " +
                 "0 = no domain colour at all, 1 = full — it is DIM rather than absent so the chip " +
                 "still says which team is missing.")]
        [SerializeField, Range(0f, 1f)] float waitingDomainStrength = 0.28f;

        [Tooltip("Seconds the lit state takes to arrive. Continuity of existence applies to UI: a " +
                 "chip that snaps on reads as a chip that was replaced.")]
        [SerializeField, Min(0.01f)] float litRiseSeconds = 0.35f;

        [Tooltip("Glow breath, in cycles per second. Slow — it is a state, not an alarm.")]
        [SerializeField, Min(0f)] float glowPulseHz = 0.7f;

        [SerializeField, Range(0f, 1f)] float glowMinAlpha = 0.25f;
        [SerializeField, Range(0f, 1f)] float glowMaxAlpha = 0.6f;
        [SerializeField, Min(1f)] float glowScale = 1.35f;

        class Chip
        {
            public IPlayer Player;
            public RectTransform Root;
            public Image Avatar;
            public Image Domain;
            public Image Glow;
            public float Lit01;
        }

        readonly List<Chip> _chips = new();
        readonly List<IPlayer> _humans = new();
        RectTransform _container;
        RectTransform _template;
        bool _running;

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
        /// Hand the roster its sources when it was ENSURED at runtime rather than authored (the
        /// connecting panel adds one so a hierarchy that only carries the art still gets the
        /// behaviour). An authored reference always wins - this only fills what is empty.
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
            Rebuild();
        }

        /// <summary>Stop drawing (the panel is coming down).</summary>
        public void End() => _running = false;

        void Awake() => ResolveContainer();

        void Update()
        {
            if (!_running) return;

            if (RosterChanged()) Rebuild();

            float dt = Time.unscaledDeltaTime;
            float pulse = Mathf.Lerp(glowMinAlpha, glowMaxAlpha,
                                     0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowPulseHz * Mathf.PI * 2f));

            for (int i = 0; i < _chips.Count; i++)
                TickChip(_chips[i], dt, pulse);
        }

        void TickChip(Chip chip, float deltaTime, float pulseAlpha)
        {
            if (chip?.Root == null) return;

            bool ready = chip.Player != null && chip.Player.IsArenaReady;
            chip.Lit01 = Mathf.MoveTowards(chip.Lit01, ready ? 1f : 0f, deltaTime / litRiseSeconds);

            Color domain = DomainColor(chip.Player);

            if (chip.Avatar)
                chip.Avatar.color = Color.Lerp(waitingAvatarTint, Color.white, chip.Lit01);

            if (chip.Domain)
            {
                var dim = domain * waitingDomainStrength;
                dim.a = domain.a;
                chip.Domain.color = Color.Lerp(dim, domain, chip.Lit01);
            }

            if (chip.Glow)
            {
                var glow = domain;
                glow.a = pulseAlpha * chip.Lit01;
                chip.Glow.color = glow;
                chip.Glow.gameObject.SetActive(chip.Lit01 > 0.001f);
            }
        }

        // ── Roster ──────────────────────────────────────────────────────────

        bool RosterChanged()
        {
            int seen = 0;
            var players = gameData != null ? gameData.Players : null;
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (p == null || p.IsInitializedAsAI) continue;
                    if (seen >= _humans.Count || !ReferenceEquals(_humans[seen], p)) return true;
                    seen++;
                }
            }
            return seen != _humans.Count;
        }

        void Rebuild()
        {
            _humans.Clear();
            var players = gameData != null ? gameData.Players : null;
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                    if (players[i] != null && !players[i].IsInitializedAsAI)
                        _humans.Add(players[i]);

            EnsureTemplate();

            // Grow / shrink the chip pool to the roster, reusing what is already built.
            while (_chips.Count > _humans.Count)
            {
                var last = _chips[^1];
                _chips.RemoveAt(_chips.Count - 1);
                if (last?.Root) Destroy(last.Root.gameObject);
            }
            while (_chips.Count < _humans.Count)
            {
                var chip = BuildChip();
                if (chip == null) break;
                _chips.Add(chip);
            }

            for (int i = 0; i < _chips.Count; i++)
            {
                _chips[i].Player = _humans[i];
                _chips[i].Lit01 = _humans[i] != null && _humans[i].IsArenaReady ? 1f : 0f;
                if (_chips[i].Avatar) _chips[i].Avatar.sprite = AvatarSprite(_humans[i]);
                if (_chips[i].Root) _chips[i].Root.gameObject.SetActive(true);
                TickChip(_chips[i], 0f, glowMaxAlpha);
            }
        }

        // ── Building ────────────────────────────────────────────────────────

        void ResolveContainer()
        {
            if (_container) return;
            _container = container;
            if (!_container)
            {
                var named = transform.Find("PlayerIcons") as RectTransform;
                if (!named)
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

            // An authored chip is the template, not a chip: it is what the row's art LOOKS like,
            // and the row's length is the player count, not one.
            if (!_template && _container && _container.childCount > 0)
                _template = _container.GetChild(0) as RectTransform;

            // Hidden, not destroyed: it is the source the chips are cut from, and a
            // layout group ignores an inactive child, so it costs the row nothing.
            if (_template) _template.gameObject.SetActive(false);
        }

        /// <summary>
        /// Build one chip as a WRAPPER holding a glow and the visual, in that order.
        ///
        /// <para>The wrapper is not ceremony. A UGUI graphic always draws before its own children,
        /// so if the authored chip carries its domain plate on its ROOT — which the shipped one
        /// does, a single 70x70 Image — nothing parented under it can ever draw BEHIND that plate.
        /// A glow in front of the plate is not a glow. Wrapping gives the glow a sibling slot
        /// under the visual, which is the only place it can be, and costs the layout group
        /// nothing: it lays out wrappers exactly as it laid out chips.</para>
        /// </summary>
        Chip BuildChip()
        {
            if (!_container) return null;

            var wrapperGo = new GameObject($"PlayerChip{_chips.Count}", typeof(RectTransform));
            var wrapper = (RectTransform)wrapperGo.transform;
            wrapper.SetParent(_container, false);
            wrapper.sizeDelta = _template ? _template.sizeDelta : new Vector2(builtEntrySize, builtEntrySize);

            var chip = new Chip { Root = wrapper };

            // Glow first, so it is behind everything the chip draws.
            chip.Glow = BuildGlow(wrapper);

            RectTransform visual;
            if (_template)
            {
                visual = Instantiate(_template, wrapper);
                visual.gameObject.SetActive(true);
            }
            else
            {
                visual = BuildPlainVisual(wrapper);
            }
            visual.name = "Visual";
            Stretch(visual, 0f);
            visual.localScale = Vector3.one;

            chip.Domain = FindImage(visual, "domain") ?? visual.GetComponent<Image>();
            chip.Avatar = FindImage(visual, "avatar") ?? FindImage(visual, "icon")
                          ?? FindImage(visual, "profile") ?? FindImage(visual, "portrait");

            // A template carrying exactly one Image is a domain plate with no avatar slot; give it
            // one rather than dropping the avatar silently.
            if (!chip.Avatar || chip.Avatar == chip.Domain) chip.Avatar = BuildAvatar(visual);

            // The glow is the chip's own SHAPE - a rectangular halo behind a round plate reads as
            // a broken sprite, not as a glow.
            if (chip.Glow && chip.Domain)
            {
                chip.Glow.sprite = chip.Domain.sprite;
                chip.Glow.type = chip.Domain.type;
            }

            if (chip.Avatar) chip.Avatar.raycastTarget = false;
            if (chip.Domain) chip.Domain.raycastTarget = false;
            if (chip.Glow) chip.Glow.raycastTarget = false;
            return chip;
        }

        RectTransform BuildPlainVisual(RectTransform wrapper)
        {
            var go = new GameObject("Visual", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(wrapper, false);
            go.GetComponent<Image>().raycastTarget = false;
            return rt;
        }

        Image BuildAvatar(RectTransform visual)
        {
            var go = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(visual, false);
            Stretch(rt, 0.12f);
            rt.SetAsLastSibling();          // over the domain plate
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        Image BuildGlow(RectTransform wrapper)
        {
            var go = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(wrapper, false);
            Stretch(rt, 0f);
            rt.localScale = Vector3.one * glowScale;

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            go.SetActive(false);
            return img;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = new Vector2(inset, inset);
            rt.anchorMax = new Vector2(1f - inset, 1f - inset);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            if (profileIcons == null || profileIcons.profileIcons == null ||
                profileIcons.profileIcons.Count == 0)
                return null;

            int id = player?.AvatarId ?? 0;
            foreach (var icon in profileIcons.profileIcons)
                if (icon.Id == id) return icon.IconSprite;
            return profileIcons.profileIcons[0].IconSprite;
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
