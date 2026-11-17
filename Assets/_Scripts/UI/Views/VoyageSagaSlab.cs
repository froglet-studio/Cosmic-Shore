using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Saga tab as the Sea table (Rev 90, Docs/MENU_REDESIGN_B.md sections 171-172): the saga's parts as chips, the
    /// picked part's arena on the table, its forms in order as stations on the front face, the squad on the right and
    /// Set sail.
    ///
    /// <para>The parts are the page's own cards (<see cref="VoyageView"/>, Tandava and its Severing): each card's name,
    /// line and art are its <see cref="SO_ArcadeGame"/>'s, its forms are the names authored on the card
    /// (<c>Form1..FormN/Name</c>), and Set sail opens the card's launch window as the card always did
    /// (<see cref="VoyageView.SetSail"/>).</para>
    ///
    /// <para><b>Locked</b> (<see cref="Locked"/>; Shombith, 2026-11-12: "lock saga", the whole tab): the page is drawn as
    /// Episodes is - the first part's arena on the table, dimmed, the saga's name with "Coming soon", one line on the
    /// front face and a button back to the Voyages tab. No part is listed and nothing launches.
    /// <see cref="VoyageJobsView"/>'s <c>sagaLocked</c> switch opens it again.</para>
    /// </summary>
    public class VoyageSagaSlab : VoyageSlabPage
    {
        readonly List<VoyageView> _parts = new();
        readonly List<Chip> _chips = new();
        readonly List<Station> _stations = new();
        readonly List<string> _forms = new();
        VoyageSquadColumn _squad;
        int _picked;

        /// <summary>The saga is not open: the page shows it locked and nothing launches. Set before the page first draws.</summary>
        public bool Locked { get; set; }

        protected override void BuildPanel()
        {
            GetComponentsInChildren(true, _parts);
            _parts.RemoveAll(p => !p || !p.Card);
            if (Locked)
            {
                BuildBack();
                return;
            }
            _squad = new VoyageSquadColumn(Kit, Panel, Vessels);
            Kit.Cta(_squad.Foot, Lines?.BoardSailKey, Lines?.CardSetSail, SetSail);
        }

        void SetSail()
        {
            if (_picked >= 0 && _picked < _parts.Count) _parts[_picked].SetSail();
        }

        protected override void Draw()
        {
            if (Locked)
            {
                var first = _parts.Count > 0 ? _parts[0].Card : null;
                ShowLocked(Lines?.SlabSagaLockedKicker, first ? first.DisplayName : null, Lines?.SlabSagaLockedLine,
                    Lines?.SlabSagaLockedFace, first ? first.CardBackground : null);
                return;
            }
            int count = _parts.Count;
            _picked = count == 0 ? -1 : Mathf.Clamp(_picked, 0, count - 1);
            string total = count.ToString("0");
            _chips.Clear();
            for (int i = 0; i < count; i++)
            {
                var card = _parts[i].Card;
                string n = (i + 1).ToString("00");
                _chips.Add(new Chip
                {
                    Title = TemplateText.Fill(Lines?.SlabChip, "n", n, "name", card.DisplayName),
                    Meta = TemplateText.Fill(Lines?.SlabSagaPart, "n", (i + 1).ToString("0"), "count", total),
                    Art = card.CardBackground,
                });
            }
            ShowChips(_chips, _picked, Pick);
            _squad.Draw();
            if (_picked < 0)
            {
                ShowName(null, null, null, null);
                Picture.Show(null);
                ShowTrack(null);
                return;
            }

            var open = _parts[_picked];
            ShowName(TemplateText.Fill(Lines?.SlabSagaKicker, "n", (_picked + 1).ToString("0"), "count", total),
                open.Card.DisplayName, open.Card.Description, null);
            Picture.Show(open.Card.CardBackground);
            Forms(open, _forms);
            _stations.Clear();
            for (int i = 0; i < _forms.Count; i++)
                _stations.Add(new Station
                {
                    Key = TemplateText.Fill(Lines?.SlabSagaForm, "n", (i + 1).ToString("0")),
                    Name = _forms[i],
                    Ring = Kit.Light,
                });
            ShowTrack(_stations);
        }

        void Pick(int index)
        {
            if (index == _picked) return;
            _picked = index;
            Redraw();
        }

        /// <summary>A part's forms, in order: the names authored on its card (<c>Form1/Name</c>, <c>Form2/Name</c>, ...).</summary>
        static void Forms(VoyageView part, List<string> into)
        {
            into.Clear();
            for (int i = 1; ; i++)
            {
                var form = part.transform.Find("Form" + i);
                if (!form) break;
                var name = form.Find("Name");
                if (name && name.TryGetComponent(out TMP_Text text) && !string.IsNullOrEmpty(text.text)) into.Add(text.text);
            }
        }
    }
}
