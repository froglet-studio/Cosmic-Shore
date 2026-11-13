using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// What the Voyage window knows about the squads and their seats, with no drawing in it: who sits where, what a seat
    /// may take for its vessel, pilot, element and artifact, and the one way a seat changes. Shared by the squad screen
    /// (<see cref="VoyageSquadDeck"/>), the squads on the voyage screen (<see cref="VoyageBoard"/>) and the Saga tab's
    /// column (<see cref="VoyageSquadColumn"/>), so all three keep the same rules.
    /// <para>The squads are the Ark's (<see cref="SquadBook"/>) under the Ark's rules (<c>SquadsPage</c>): your own seat,
    /// and the AI or open seats of a squad you made; a pilot flies one seat, so taking one who flies another seat swaps
    /// them (<see cref="SavedSquad.KeepCardsOwn"/>); every vessel keeps a pilot (<see cref="SavedSquad.FillPilots"/>).</para>
    /// </summary>
    public sealed class VoyageSeats
    {
        /// <summary>The four things a seat chooses, in the order the screens show them.</summary>
        public const int Vessel = 0, Pilot = 1, ElementKind = 2, Artifact = 3;

        public static readonly Element[] Elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        /// <summary>One choice a seat may take: what it shows and what it writes into the seat.</summary>
        public struct Option
        {
            public string Name, Note;
            public bool Picked;
            public Sprite Picture;
            public VesselClassType Vessel;
            public string Id;
            public Element Element;
        }

        /// <summary>The squad changed (another squad picked, a seat changed, a squad saved).</summary>
        public event Action Changed;

        readonly VoyageLines _lines;
        readonly SO_VesselList _vessels;
        readonly PilotArtifactCatalogSO _catalog;
        readonly VoyageBoardStyleSO _style;
        readonly List<PilotRecord> _crew = new();
        readonly List<ArtifactRecord> _hoard = new();
        readonly List<string> _ids = new();

        public VoyageSeats(VoyageBoardStyleSO style, VoyageLines lines, SO_VesselList vessels)
        {
            _style = style;
            _lines = lines;
            _vessels = vessels;
            _catalog = PilotArtifactCatalogSO.Load();
        }

        public static SquadBook Book => UGSDataService.Instance?.SquadRepo?.Data?.Book;

        static void SaveBook() => UGSDataService.Instance?.SquadRepo?.MarkDirty();

        public static SavedSquad Active => Book?.GetActive();

        public static bool CanEdit(SavedSquad squad, int seat) => squad != null && VoyageOpenCard.CanEdit(squad, seat);

        /// <summary>The player's own seat in <paramref name="squad"/> when they may change it; else none (-1).</summary>
        public static int MySeat(SavedSquad squad)
        {
            if (squad == null) return -1;
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
                if (VoyageOpenCard.IsMySeat(squad, seat) && VoyageOpenCard.CanEdit(squad, seat)) return seat;
            return -1;
        }

        // ── Reading a seat ───────────────────────────────────────────────────

        public SO_Vessel VesselOf(VesselClassType cls) =>
            _vessels && _vessels.TryGetVesselByClass(cls, out var v, reportMissing: false) ? v : null;

        public static Sprite HullOf(SO_Vessel v) =>
            !v ? null : v.SquadImage ? v.SquadImage : v.CardSilohoutteActive ? v.CardSilohoutteActive : v.IconActive;

        public PilotSO PilotOf(SquadSlot slot, SO_Vessel vessel) =>
            _catalog ? _catalog.ResolvePilot(slot, vessel ? vessel.Keywords : null) : null;

        public string ElementName(Element e) => e switch
        {
            Element.Charge => _lines?.ElementCharge,
            Element.Mass => _lines?.ElementMass,
            Element.Space => _lines?.ElementSpace,
            Element.Time => _lines?.ElementTime,
            _ => string.Empty,
        };

        /// <summary>The seat's place in the squad (Leader, Rogue One, Rogue Two).</summary>
        public string Role(int seat) => seat switch
        {
            0 => _lines?.CardSeatLeader,
            1 => _lines?.CardSeatRogueOne,
            _ => _lines?.CardSeatRogueTwo,
        };

        /// <summary>Who flies the seat: you, a squad member by name, an open seat, or the AI.</summary>
        public string Who(SavedSquad squad, int seat, SquadSlot slot)
        {
            if (VoyageOpenCard.IsMySeat(squad, seat)) return _lines?.CardSeatYou;
            if (slot.IsOpen()) return _lines?.CardSeatOpen;
            if (slot.IsPlayer()) return slot.MemberName;
            return _lines?.CardSeatAi;
        }

        /// <summary>The person's name on the player's own seats (a person this crew holds), else the callsign.</summary>
        public static string PilotName(SquadSlot slot, PilotSO pilot, bool editable)
        {
            if (editable && pilot && PilotLoadoutStore.TryGetPilot(slot.PilotId, out var person) && person.CallsignOrTemplate() == pilot.Id)
                return person.DisplayName();
            return pilot ? pilot.DisplayName : string.Empty;
        }

        public string ArtifactName(SquadSlot slot, SO_Vessel vessel, bool editable)
        {
            var kind = _catalog ? _catalog.ResolveArtifact(slot, vessel ? vessel.Keywords : null) : null;
            if (editable && kind && PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var held) && held.Kind() == kind.Id) return held.Name;
            return kind ? kind.DisplayName : _lines?.CardNoArtifact;
        }

        // ── What a seat may take ─────────────────────────────────────────────

        /// <summary>
        /// Fill <paramref name="offered"/> with what <paramref name="seat"/> of <paramref name="squad"/> may take for
        /// <paramref name="kind"/>, under the rules the Ark's picker keeps, in the order they are shown.
        /// </summary>
        public void Offer(int kind, SavedSquad squad, int seat, List<Option> offered)
        {
            offered.Clear();
            if (squad == null) return;
            var slot = squad.GetSlot(seat);
            var vessel = VesselOf(slot.Vessel);
            switch (kind)
            {
                case Vessel:
                    if (_vessels && _vessels.VesselList != null)
                        foreach (var v in _vessels.VesselList)
                            if (v && !v.IsLocked)
                                offered.Add(new Option { Name = v.CardName, Vessel = v.Class, Picture = HullOf(v), Picked = v.Class == slot.Vessel });
                    break;
                case Pilot:
                {
                    // The crew's people whose callsign can fly the hull, in crew order; one who flies another seat swaps.
                    string current = PilotLoadoutStore.TryGetPilot(slot.PilotId, out var seated) ? seated.Id : slot.PilotId;
                    if (!_catalog || !vessel) break;
                    PilotLoadoutStore.GetCrew(_crew);
                    foreach (var person in _crew)
                    {
                        if (!_catalog.TryGetPilot(person.Id, out var callsign) || !callsign || callsign.Requirement == null
                            || !callsign.Requirement.IsMetBy(vessel.Class, vessel.Keywords)) continue;
                        offered.Add(new Option
                        {
                            Name = person.DisplayName(), Id = person.Id, Picture = callsign.Portrait, Picked = person.Id == current,
                            Note = SwapNote(squad, seat, person.Id),
                        });
                    }
                    break;
                }
                case ElementKind:
                    foreach (var e in Elements)
                        offered.Add(new Option { Name = ElementName(e), Element = e, Picked = e == slot.Element });
                    break;
                default:
                {
                    // None, then the hoard's artifacts the hull can carry (each one of a kind).
                    string current = PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var carried) ? carried.Id : null;
                    offered.Add(new Option { Name = _lines?.CardNoArtifact, Id = null, Picked = string.IsNullOrEmpty(current) });
                    if (!_catalog || !vessel) break;
                    PilotLoadoutStore.GetHoard(_hoard);
                    foreach (var artifact in _hoard)
                        if (_catalog.TryGetArtifact(artifact.Id, out var held) && held && held.Requirement != null
                            && held.Requirement.IsMetBy(vessel.Class, vessel.Keywords))
                            offered.Add(new Option { Name = artifact.Name, Id = artifact.Id, Picked = artifact.Id == current });
                    break;
                }
            }
        }

        /// <summary>A pilot who flies another seat of the squad swaps with it: the note under their picture names the
        /// vessel they leave (the content's <see cref="VoyageLines.BoardSwapsWith"/>); nothing for anyone else.</summary>
        string SwapNote(SavedSquad squad, int seat, string personId)
        {
            for (int other = 0; other < SavedSquad.SlotCount; other++)
            {
                if (other == seat) continue;
                var slot = squad.GetSlot(other);
                string id = PilotLoadoutStore.TryGetPilot(slot.PilotId, out var p) ? p.Id : slot.PilotId;
                if (id != personId) continue;
                var vessel = VesselOf(slot.Vessel);
                return TemplateText.Fill(_lines?.BoardSwapsWith, "vessel", vessel ? vessel.CardName : null);
            }
            return null;
        }

        // ── Changing ─────────────────────────────────────────────────────────

        /// <summary>
        /// The active squad's <paramref name="seat"/> takes <paramref name="option"/> for <paramref name="kind"/> and the
        /// squad is saved: the pilot swap and the crew fill run (the Ark's rules). False when the seat is not the player's
        /// to change.
        /// </summary>
        public bool Pick(int seat, int kind, Option option)
        {
            var book = Book;
            var active = book?.GetActive();
            if (active == null || !VoyageOpenCard.CanEdit(active, seat)) return false;
            var squad = SquadMembership.Copy(active);
            var slot = squad.GetSlot(seat);
            string previousPilot = slot.PilotId;
            switch (kind)
            {
                case Vessel:
                    slot = slot.WithVessel(option.Vessel);
                    // The player's own card starts on the person they keep in that vessel's Hangar seat (a crew id).
                    if (VoyageOpenCard.IsMySeat(squad, seat)) slot.PilotId = PilotLoadoutStore.GetPilot(option.Vessel);
                    break;
                case Pilot:
                    slot.PilotId = option.Id;
                    break;
                case ElementKind:
                    slot.Element = option.Element;
                    break;
                default:
                    slot.ArtifactId = option.Id;
                    break;
            }
            squad.SetSlot(seat, slot);
            squad.KeepCardsOwn(seat, previousPilot);
            PilotLoadoutStore.GetCrew(_crew);
            _ids.Clear();
            foreach (var p in _crew) if (!string.IsNullOrEmpty(p.Id)) _ids.Add(p.Id);
            squad.FillPilots(_ids);
            int index = book.ActiveIndex;
            if (index < 0 || index >= book.Squads.Count) return false;
            book.Squads[index] = squad;
            SaveBook();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Make squad <paramref name="index"/> the one that sails. False when it already is, or there is none.</summary>
        public bool PickSquad(int index)
        {
            var book = Book;
            if (book == null || index == book.ActiveIndex || !book.TrySetActive(index)) return false;
            SaveBook();
            Changed?.Invoke();
            return true;
        }

        /// <summary>True when the squad that sails is the player's own and the book has room for one more.</summary>
        public static bool CanSavePreset()
        {
            var book = Book;
            var active = book?.GetActive();
            return active != null && !active.IsJoined() && book.CanAdd();
        }

        /// <summary>Keep the squad as it stands as a new squad of the player's own (<see cref="SavedSquad.Clone"/>: the
        /// book names it), and make it the one that sails.</summary>
        public bool SavePreset()
        {
            if (!CanSavePreset()) return false;
            var book = Book;
            book.Add(book.GetActive().Clone(null));
            book.TrySetActive(book.Squads.Count - 1);
            SaveBook();
            Changed?.Invoke();
            return true;
        }

        /// <summary>A squad's name as the screens write it: a joined squad names whose it is.</summary>
        public string SquadName(SavedSquad squad) => squad == null ? null : squad.IsJoined()
            ? TemplateText.Fill(_lines?.BoardSquadJoined, "name", squad.Name, "owner", squad.OwnerName)
            : squad.Name;

        // ── A hull picture ───────────────────────────────────────────────────

        /// <summary>A hull render cropped to the hull (<see cref="VoyageBoardStyleSO.CropOf"/>), fitted in its box.</summary>
        public sealed class HullView
        {
            public RawImage Image;
            public AspectRatioFitter Fit;
        }

        public static HullView HullBox(RectTransform host)
        {
            var rt = VoyageKit.Node("Render", host);
            VoyageKit.Stretch(rt);
            var view = new HullView { Image = rt.gameObject.AddComponent<RawImage>() };
            view.Image.raycastTarget = false;
            view.Image.enabled = false;
            view.Fit = rt.gameObject.AddComponent<AspectRatioFitter>();
            view.Fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            return view;
        }

        /// <summary>Show <paramref name="sprite"/> cropped to its hull (the style's measured crop), as large as its box
        /// allows.</summary>
        public void SetHull(HullView view, Sprite sprite)
        {
            if (!sprite || !sprite.texture)
            {
                view.Image.enabled = false;
                return;
            }
            var crop = _style ? _style.CropOf(sprite) : new Rect(0f, 0f, 1f, 1f);
            var tex = sprite.texture;
            var tr = sprite.textureRect;
            view.Image.texture = tex;
            view.Image.uvRect = new Rect((tr.x + crop.x * tr.width) / tex.width, (tr.y + crop.y * tr.height) / tex.height,
                crop.width * tr.width / tex.width, crop.height * tr.height / tex.height);
            view.Fit.aspectRatio = crop.width * tr.width / Mathf.Max(1f, crop.height * tr.height);
            view.Image.enabled = true;
        }
    }
}
