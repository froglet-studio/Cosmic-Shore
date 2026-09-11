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
    /// The scoreboard's answer to "does anybody want another round?" — one avatar under the Play
    /// Again button per player who has asked for a rematch, ringed in their domain colour.
    ///
    /// <para><b>Why it exists.</b> Play Again is host-authoritative: a client's press is a VOTE and
    /// only the host's press restarts the party. So the host is the one peer whose answer matters,
    /// and until now a client could press and the host would learn about it from a toast that had
    /// already scrolled away and a number in a button label. A row of faces is the fix — it is
    /// state rather than an announcement, so it is still on screen when the host looks.</para>
    ///
    /// <para>The vote it draws is <see cref="IPlayer.HasVotedRematch"/>, a server-written
    /// <c>NetworkVariable</c> on the voter, so every peer — the voter, the host, and everyone
    /// watching — draws the same row off the same replicated fact. This row never writes: it is a
    /// READOUT of that state and nothing else.</para>
    ///
    /// <para>Only VOTERS get a chip. A row that also listed the silent as greyed placeholders
    /// would say the same thing twice (the button label already carries "2/3") while making an
    /// empty row look like a broken one instead of like nobody having asked.</para>
    ///
    /// <para><b>AI are absent</b>, for the reason the connecting panel's roster gives: nothing can
    /// be waited on that has no machine and no opinion, and a chip that could never appear reads
    /// as a player who never answered.</para>
    ///
    /// <para>Everything is adopted by name, so a prefab carrying only the art lights up with no
    /// wiring: the container is a descendant named <c>PlayerAvatars</c> (else this object), and the
    /// template is the field, else that container's first child. The template is a TEMPLATE, never
    /// a chip — the row's length is the vote count, not one.</para>
    /// </summary>
    public class RematchVoteRoster : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;

        [Tooltip("Avatar art. Left empty, the scoreboard's own list is adopted, so the vote row " +
                 "and the score cards cannot show different faces for one player.")]
        [SerializeField] SO_ProfileIconList profileIcons;

        [Header("Layout")]
        [Tooltip("Where the chips go — the horizontal layout group under the Play Again button. " +
                 "Left empty, a descendant named \"PlayerAvatars\" is adopted; failing that, this " +
                 "object itself.")]
        [SerializeField] RectTransform container;

        [Tooltip("The chip to clone, once per voter. Left empty, the container's first child is " +
                 "used (and hidden). It is a TEMPLATE, never a chip.")]
        [SerializeField] RectTransform entryTemplate;

        [Tooltip("THE AVATAR: the Image inside the template that shows the player's picture. Left " +
                 "empty, a child named avatar / icon / player / profile / portrait is adopted, " +
                 "else the template's own Image.")]
        [SerializeField] Image templateAvatarImage;

        [Header("Domain halo")]
        [Tooltip("Sprite for the ring behind the avatar. Left empty the template's own sprite is " +
                 "borrowed, so the halo is the chip's SHAPE — a rectangular halo behind a round " +
                 "avatar reads as a broken sprite rather than as a glow.")]
        [SerializeField] Sprite haloSprite;

        [SerializeField, Min(1f)] float haloScale = 1.22f;
        [SerializeField, Range(0f, 1f)] float haloMinAlpha = 0.45f;
        [SerializeField, Range(0f, 1f)] float haloMaxAlpha = 0.9f;

        [Tooltip("Halo breath, in cycles per second. Slow — a vote is a state, not an alarm.")]
        [SerializeField, Min(0f)] float glowPulseHz = 0.7f;

        [Header("Arrival")]
        [Tooltip("Seconds a newly arrived vote takes to bloom in. Continuity of existence applies " +
                 "to UI: a chip that pops reads as a glitch rather than as somebody answering.")]
        [SerializeField, Min(0.01f)] float arriveSeconds = 0.28f;

        [SerializeField, Min(16f)] float builtEntrySize = 64f;

        class Chip
        {
            public IPlayer Player;
            public RectTransform Root;
            public Image Avatar;
            public Image Halo;
            public float Arrive01;
        }

        /// <summary>The name this row adopts as its strip - the object the artist authored.</summary>
        const string ContainerName = "PlayerAvatars";

        readonly List<Chip> _chips = new();
        readonly List<IPlayer> _voters = new();
        RectTransform _container;
        RectTransform _template;
        bool _containerAuthored;
        Sprite _templateSprite;
        string _avatarPath;
        bool _running;

        /// <summary>How many players are currently asking for a rematch.</summary>
        public int VoteCount => _voters.Count;

        /// <summary>
        /// Hand the roster its sources when it was ENSURED at runtime rather than authored. An
        /// authored reference always wins — this only fills what is empty.
        /// </summary>
        public void AdoptSources(GameDataSO data, SO_ProfileIconList icons)
        {
            if (!gameData) gameData = data;
            if (!profileIcons) profileIcons = icons;
        }

        /// <summary>Start drawing. Clears whatever the last scoreboard left behind.</summary>
        public void Begin()
        {
            ResolveContainer();
            EnsureTemplate();
            _running = true;
            Rebuild();
        }

        /// <summary>Stop drawing and clear the row (the scoreboard is coming down).</summary>
        public void End()
        {
            _running = false;
            _voters.Clear();
            for (int i = 0; i < _chips.Count; i++)
                if (_chips[i]?.Root) Destroy(_chips[i].Root.gameObject);
            _chips.Clear();
        }

        void Awake()
        {
            ResolveContainer();
            // Hidden at AWAKE, not at the first Begin: the template is authored ACTIVE so it can be
            // seen and laid out in the editor, and left active it is a stray chip wearing a blank
            // sprite that shows on screen before anybody has voted.
            EnsureTemplate();
        }

        void Update()
        {
            if (!_running) return;

            // Polled rather than driven off the vote RPC, for the reason the connecting roster
            // polls: the state is replicated, and a NetworkVariable landing on a late-joining or
            // re-shown scoreboard raises no event this component was around to hear.
            if (VotersChanged()) Rebuild();

            float dt = Time.unscaledDeltaTime;
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowPulseHz * Mathf.PI * 2f);
            for (int i = 0; i < _chips.Count; i++)
                TickChip(_chips[i], dt, breath);
        }

        void TickChip(Chip chip, float deltaTime, float breath01)
        {
            if (chip?.Root == null) return;

            chip.Arrive01 = Mathf.MoveTowards(chip.Arrive01, 1f, deltaTime / arriveSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, chip.Arrive01);
            chip.Root.localScale = Vector3.one * eased;

            if (chip.Avatar)
            {
                var tint = Color.white;
                tint.a = eased;
                chip.Avatar.color = tint;
            }

            if (chip.Halo)
            {
                var halo = DomainColor(chip.Player);
                halo.a = Mathf.Lerp(haloMinAlpha, haloMaxAlpha, breath01) * eased;
                chip.Halo.color = halo;
                chip.Halo.rectTransform.localScale = Vector3.one * haloScale;
            }
        }

        // ── Roster ──────────────────────────────────────────────────────────

        bool VotersChanged()
        {
            int seen = 0;
            var players = gameData != null ? gameData.Players : null;
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (!IsVoter(p)) continue;
                    if (seen >= _voters.Count || !ReferenceEquals(_voters[seen], p)) return true;
                    seen++;
                }
            }
            return seen != _voters.Count;
        }

        static bool IsVoter(IPlayer p)
        {
            if (p == null) return false;
            if (p is UnityEngine.Object obj && !obj) return false;   // destroyed Player still passes != null
            return !p.IsInitializedAsAI && p.HasVotedRematch;
        }

        void Rebuild()
        {
            // Remember who already had a chip, so a vote arriving does not restart the bloom on
            // everyone else's face — only the new one animates in.
            var carried = new Dictionary<IPlayer, float>();
            for (int i = 0; i < _chips.Count; i++)
                if (_chips[i]?.Player != null) carried[_chips[i].Player] = _chips[i].Arrive01;

            _voters.Clear();
            var players = gameData != null ? gameData.Players : null;
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                    if (IsVoter(players[i])) _voters.Add(players[i]);

            while (_chips.Count > _voters.Count)
            {
                var last = _chips[^1];
                _chips.RemoveAt(_chips.Count - 1);
                if (last?.Root) Destroy(last.Root.gameObject);
            }
            while (_chips.Count < _voters.Count)
            {
                var chip = BuildChip();
                if (chip == null) break;
                _chips.Add(chip);
            }

            for (int i = 0; i < _chips.Count; i++)
            {
                _chips[i].Player = _voters[i];
                _chips[i].Arrive01 = carried.TryGetValue(_voters[i], out var held) ? held : 0f;
                ApplyAvatar(_chips[i], _voters[i]);
                if (_chips[i].Root) _chips[i].Root.gameObject.SetActive(true);
                TickChip(_chips[i], 0f, 1f);
            }
        }

        /// <summary>
        /// Put the voter's face on the chip.
        ///
        /// <para>A resolved sprite wins; an UNRESOLVED one leaves the template's authored sprite in
        /// place rather than clearing it, because an Image with no sprite draws a solid WHITE
        /// RECTANGLE — the blank box that reads as a broken chip. With no sprite either way the
        /// Image is switched off, for the same reason.</para>
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
                foreach (var t in GetComponentsInChildren<RectTransform>(true))
                {
                    if (t == transform) continue;
                    if (t.name == ContainerName) { _container = t; break; }
                }
            }

            // AUTHORED means "somebody put a strip here on purpose". It gates the template adoption
            // below, and that gate is the whole point: this component is ensured onto the Play
            // Again BUTTON, whose first child is the button's own LABEL. Falling back to `transform`
            // as the container the way the connecting-panel roster does would make that label the
            // template - hiding the word PLAY AGAIN and cloning it once per vote.
            _containerAuthored = _container;
            if (!_container) _container = BuildContainer();
        }

        /// <summary>
        /// A strip to hang the chips in, for a scoreboard whose art has not been authored yet - the
        /// same "work before the art lands" fallback the domain chip's ✕ uses. It is built ONLY
        /// when no <c>PlayerAvatars</c> was found, and steps aside the moment one is.
        /// </summary>
        RectTransform BuildContainer()
        {
            var go = new GameObject(ContainerName + " (generated)", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(builtEntrySize * 4f, builtEntrySize);

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.spacing = 6f;
            return rt;
        }

        void EnsureTemplate()
        {
            if (_template) return;
            _template = entryTemplate;

            // Only an AUTHORED strip's first child is a template. A generated one has no children,
            // and this must never reach for a sibling of the strip.
            if (!_template && _containerAuthored && _container && _container.childCount > 0)
                _template = _container.GetChild(0) as RectTransform;

            if (!_template) return;

            // Remember what the authored chip LOOKS like before it is hidden: its own sprite is the
            // halo's shape, and the wired avatar is recorded as a PATH because the reference points
            // at the template and every chip is a clone with its own copy of it.
            var own = _template.GetComponent<Image>();
            _templateSprite = own ? own.sprite : null;
            _avatarPath = RelativePath(_template, templateAvatarImage);

            // Hidden, not destroyed: it is the source the chips are cut from, and a layout group
            // ignores an inactive child, so it costs the row nothing.
            _template.gameObject.SetActive(false);
        }

        /// <summary>
        /// Build one chip as a WRAPPER holding the halo and the visual, in that order.
        ///
        /// <para>A UGUI graphic always draws before its own children, so nothing parented under the
        /// authored chip can draw BEHIND it — and the halo has to be behind. Wrapping gives it a
        /// sibling slot under the visual, and costs the layout group nothing.</para>
        /// </summary>
        Chip BuildChip()
        {
            if (!_container) return null;

            var wrapperGo = new GameObject($"RematchVote{_chips.Count}", typeof(RectTransform));
            var wrapper = (RectTransform)wrapperGo.transform;
            wrapper.SetParent(_container, false);
            wrapper.sizeDelta = _template ? _template.sizeDelta : new Vector2(builtEntrySize, builtEntrySize);

            var chip = new Chip { Root = wrapper };
            chip.Halo = BuildHalo(wrapper);

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

            chip.Avatar = Resolve(visual, _avatarPath)
                          ?? FindImage(visual, "avatar") ?? FindImage(visual, "icon")
                          ?? FindImage(visual, "player") ?? FindImage(visual, "profile")
                          ?? FindImage(visual, "portrait")
                          ?? visual.GetComponent<Image>()
                          ?? BuildAvatar(visual);

            if (chip.Avatar)
            {
                chip.Avatar.raycastTarget = false;
                chip.Avatar.preserveAspect = true;
            }

            // The chip sits inside the Play Again BUTTON. Nothing it draws may eat the press that
            // the whole row exists to encourage, so no part of it is a raycast target.
            foreach (var g in wrapper.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;

            return chip;
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
            rt.localScale = Vector3.one * haloScale;

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
            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
                if (images[i].transform != root &&
                    images[i].name.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return images[i];
            return null;
        }

        // ── Sources ─────────────────────────────────────────────────────────

        Sprite AvatarSprite(IPlayer player)
        {
            if (profileIcons == null || profileIcons.profileIcons == null ||
                profileIcons.profileIcons.Count == 0)
                return null;

            int id = player?.AvatarId ?? 0;
            for (int i = 0; i < profileIcons.profileIcons.Count; i++)
                if (profileIcons.profileIcons[i].Id == id) return profileIcons.profileIcons[i].IconSprite;
            return profileIcons.profileIcons[0].IconSprite;
        }

        /// <summary>
        /// The voter's domain at FULL signal strength — the accessor every other domain surface
        /// reads, so a vote chip and a score column can never disagree about what Ruby looks like.
        /// Read live rather than snapshotted.
        /// </summary>
        Color DomainColor(IPlayer player)
        {
            var theme = gameData != null ? gameData.ThemeManagerData : null;
            if (theme == null || theme.ColorSet == null) return Color.white;
            return theme.ColorSet.GetDomainSignalColor(player?.Domain ?? Domains.Blue);
        }
    }
}
