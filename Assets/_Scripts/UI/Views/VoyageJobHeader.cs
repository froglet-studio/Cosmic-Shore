using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The job over Your Ark and Vessels (Frogma Rev 138 Seats and Vessels): the giver's picture (44 x 52), the voyage's
    /// name (20), the danger meter (four segments), the sea and "3 cells · for &lt;giver&gt;" (15, Sub) at the left; WANTED
    /// and the classes the job wants (glyph 18, name 17) at the right. Laid out in the page's own units from its top left
    /// (<see cref="VoyageKit.At"/>); every word is an authored field (<see cref="VoyageQuaySO"/>, <see cref="VoyageClassesSO"/>).
    /// </summary>
    public sealed class VoyageJobHeader
    {
        const float WantedWidth = 900f, MetaY = 30f, SeaX = 156f;

        readonly VoyageKit _kit;
        readonly float _width;
        readonly VoyageQuaySO _words = VoyageQuaySO.Load();
        readonly VoyageClassesSO _classes = VoyageClassesSO.Load();
        readonly PilotArtifactCatalogSO _catalog = PilotArtifactCatalogSO.Load();
        readonly Image _face;
        readonly TMP_Text _title, _sea, _for, _wantedLabel;
        readonly Image[] _danger = new Image[4];
        readonly RectTransform _wanted;
        readonly List<(RectTransform root, VoyageGlyph glyph, TMP_Text name)> _wants = new();

        public RectTransform Root { get; }

        /// <summary>Build the header in <paramref name="parent"/>, <paramref name="width"/> wide, its top left at
        /// (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public VoyageJobHeader(VoyageKit kit, RectTransform parent, float x, float y, float width)
        {
            _kit = kit;
            _width = width;
            Root = At("JobHeader", parent, x, y, width, 52f);
            _face = VoyageKit.CoverBox(At("Picture", Root, 0f, 0f, 44f, 52f));
            _title = Text("Title", 60f, 2f, 900f, 26f, 20, _kit.Light);
            var danger = At("Danger", Root, 60f, MetaY + 3f, 84f, 14f);
            for (int i = 0; i < _danger.Length; i++)
                _danger[i] = VoyageKit.Block(At("Segment" + i, danger, i * 22f, 4f, 18f, 6f), _kit.Light);
            _sea = Text("Sea", SeaX, MetaY, 200f, 20f, 15, _kit.Sub);
            _for = Text("For", SeaX + 104f, MetaY, 600f, 20f, 15, _kit.Sub);
            _wanted = At("Wanted", Root, width - WantedWidth, 12f, WantedWidth, 28f);
            _wantedLabel = _kit.Label(At("Label", _wanted, 0f, 0f, 120f, 28f), 14, _kit.Sub);
            VoyageKit.Stretch(_wantedLabel.rectTransform);
            _wantedLabel.overflowMode = TextOverflowModes.Overflow;
        }

        static RectTransform At(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var rt = VoyageKit.Node(name, parent);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        TMP_Text Text(string name, float x, float y, float w, float h, float size, Color color)
        {
            var t = _kit.Text(At(name, Root, x, y, w, h), size, color, TextAlignmentOptions.Left);
            VoyageKit.Stretch(t.rectTransform);
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Draw <paramref name="offer"/> at <paramref name="difficulty"/>; the classes come from the seats' record
        /// <paramref name="key"/> (the job's own, <see cref="VoyageClassesSO.JobOf"/>).</summary>
        public void Draw(VoyageBoard.Offer offer, int difficulty, string key)
        {
            Root.gameObject.SetActive(offer != null);
            if (offer == null) return;
            var info = VoyageQuay.TypeOf(offer);
            var giverFace = VoyageQuay.GiverFace(info, offer, _catalog);
            VoyageKit.ShowCover(_face, giverFace ? giverFace : offer.Picture, new Vector2(44f, 52f), VoyageKit.FaceFocus);
            VoyageKit.Set(_title, offer.Title);
            int level = VoyageQuaySO.DangerLevel(difficulty);
            for (int i = 0; i < _danger.Length; i++) _danger[i].color = i < level ? _kit.Light : VoyageKit.Fade(_kit.Outline, 0.6f);
            if (!_words) return;
            VoyageKit.Set(_sea, TemplateText.Fill(_words.ArkSea, "danger", _words.Danger(difficulty)));
            float seaWidth = Mathf.Ceil(_sea.GetPreferredValues(_sea.text).x);
            VoyageKit.At(_for.rectTransform.parent as RectTransform, SeaX + seaWidth + 16f, MetaY, 600f, 20f);
            VoyageKit.Set(_for, TemplateText.Fill(_words.ArkFor, "cells",
                Mathf.Max(1, offer.Cells).ToString(CultureInfo.InvariantCulture), "giver", VoyageQuay.GiverName(info, offer, _catalog)));
            DrawWanted(VoyageClassesSO.JobOf(string.IsNullOrEmpty(key) ? offer.Key : key));
        }

        // WANTED and each wanted class, right-aligned to the header's edge.
        void DrawWanted(string job)
        {
            var wants = _classes ? _classes.WantedBy(job) : null;
            int count = wants?.Count ?? 0;
            _wanted.gameObject.SetActive(count > 0);
            if (count == 0) return;
            while (_wants.Count < count)
            {
                var root = At("Class" + _wants.Count, _wanted, 0f, 0f, 1f, 28f);
                var glyph = VoyageKit.Node("Glyph", root);
                VoyageKit.At(glyph, 0f, 5f, 18f, 18f);
                var g = glyph.gameObject.AddComponent<VoyageGlyph>();
                g.raycastTarget = false;
                var name = _kit.Text(At("Name", root, 26f, 0f, 200f, 28f), 17, _kit.Light, TextAlignmentOptions.Left);
                VoyageKit.Stretch(name.rectTransform);
                name.overflowMode = TextOverflowModes.Overflow;
                _wants.Add((root, g, name));
            }
            float x = WantedWidth;
            for (int i = _wants.Count - 1; i >= 0; i--)
            {
                var (root, glyph, name) = _wants[i];
                root.gameObject.SetActive(i < count);
                if (i >= count) continue;
                glyph.Form = VoyageGlyph.Of(wants[i]);
                glyph.color = _kit.Light;
                VoyageKit.Set(name, _classes.NameOf(wants[i]));
                float w = 26f + Mathf.Ceil(name.GetPreferredValues(name.text).x);
                x -= w;
                VoyageKit.At(root, x, 0f, w, 28f);
                x -= 26f;
            }
            VoyageKit.Set(_wantedLabel, _words ? _words.Wanted : null);
            float lw = Mathf.Ceil(_wantedLabel.GetPreferredValues(_wantedLabel.text).x);
            VoyageKit.At(_wantedLabel.rectTransform.parent as RectTransform, x - lw, 0f, lw, 28f);
        }
    }
}
