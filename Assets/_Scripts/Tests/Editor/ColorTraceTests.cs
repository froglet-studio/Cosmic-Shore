#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The colour trace (Docs/VesselModels/COLOR_TRACE.md): a colour found on screen by its value takes the worn set's
    /// colour for it, from the authored (Photic) value or from what the previous set painted, alpha kept; the ids C#
    /// computes are the ids Tools/Build/colour_trace.py wrote; and every shipped set carries every traced colour.
    /// </summary>
    public class ColorTraceTests
    {
        const string CatalogPath = "Assets/_SO_Assets/Color Palettes/ColorSetCatalog.asset";

        static SO_ColorSet SetWith(params (string id, Color color)[] entries)
        {
            var set = ScriptableObject.CreateInstance<SO_ColorSet>();
            set.TracedColors = entries.Select(e => new TracedColor { Id = e.id, Color = e.color }).ToArray();
            return set;
        }

        [SetUp]
        public void Fresh() => ColorTrace.Invalidate();

        [TearDown]
        public void Clear()
        {
            ColorTrace.Wear(null, null);
            ColorTrace.Invalidate();
        }

        [Test]
        public void ValueId_IsTheToolsId()
        {
            Assert.AreEqual("ui:747BAD", ColorTrace.ValueId(ColorTrace.Ui, new Color(0.4549f, 0.4823f, 0.6784f, 1f)));
            Assert.AreEqual("world:FF8000x2.00", ColorTrace.ValueId(ColorTrace.World, new Color(2f, 1f, 0f, 1f)));
            Assert.AreEqual("world:000000", ColorTrace.ValueId(ColorTrace.World, new Color(-0.1f, 0f, 0f, 1f)));
        }

        [Test]
        public void Retrace_FollowsTheWornSet_FromTheAuthoredOrThePreviousValue()
        {
            var photic = SetWith(("world:FF8000", new Color(1f, 0.5f, 0f, 1f)));
            var nocturne = SetWith(("world:FF8000", new Color(0.9f, 0.6f, 0.1f, 1f)));
            var nacre = SetWith(("world:FF8000", new Color(1f, 0.8f, 0.6f, 1f)));

            ColorTrace.Wear(nocturne, photic);
            Assert.IsTrue(ColorTrace.TryRetrace(ColorTrace.World, new Color(1f, 0.5f, 0f, 0.5f), out var a));
            Assert.AreEqual(new Color(0.9f, 0.6f, 0.1f, 0.5f), a, "the authored value takes the worn set's, alpha kept");

            ColorTrace.Wear(nacre, photic);
            Assert.IsTrue(ColorTrace.TryRetrace(ColorTrace.World, a, out var b), "what Nocturne painted is found again");
            Assert.AreEqual(new Color(1f, 0.8f, 0.6f, 0.5f), b);

            ColorTrace.Wear(null, photic);
            Assert.IsTrue(ColorTrace.TryRetrace(ColorTrace.World, b, out var c));
            Assert.AreEqual(new Color(1f, 0.5f, 0f, 0.5f), c, "back to the baked value");

            Assert.IsFalse(ColorTrace.TryRetrace(ColorTrace.World, new Color(0.1f, 0.2f, 0.3f, 1f), out var untouched));
            Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f, 1f), untouched, "a colour the trace does not know is left alone");
        }

        [Test]
        public void Retrace_AsksTheOtherRole_AndIsQuietBeforeAnySetIsWorn()
        {
            var photic = SetWith(("ui:FF8000", new Color(1f, 0.5f, 0f, 1f)));
            var other = SetWith(("ui:FF8000", new Color(0.2f, 0.4f, 0.9f, 1f)));
            Assert.AreEqual(new Color(1f, 0.5f, 0f, 1f), ColorTrace.Retrace(ColorTrace.World, new Color(1f, 0.5f, 0f, 1f)),
                "nothing is worn: the colour is itself");
            ColorTrace.Wear(other, photic);
            Assert.AreEqual(new Color(0.2f, 0.4f, 0.9f, 1f), ColorTrace.Retrace(ColorTrace.World, new Color(1f, 0.5f, 0f, 1f)),
                "a ui colour asked as world is still found");
        }

        [Test]
        public void TryColorOf_PacksBackToItsId()
        {
            foreach (var id in new[] { "ui:747BAD", "world:000000", "world:FF8000x2.00", "world:FF0101x1.50", "ui:0A0B0C" })
            {
                Assert.IsTrue(ColorTrace.TryColorOf(id, out var c), id);
                Assert.AreEqual(id, ColorTrace.ValueId(id.Substring(0, id.IndexOf(':')), c), "the colour an id names packs back to it");
            }
            Assert.IsFalse(ColorTrace.TryColorOf("ui-token:Tooltip", out _), "a token id names no colour");
            Assert.IsFalse(ColorTrace.TryColorOf("world:FF80", out _));
            Assert.IsFalse(ColorTrace.TryColorOf("world:FF8000x2.0", out _));
        }

        [Test]
        public void Retrace_FindsAThemeToken_AsAuthored_AndAsPhoticPaintedIt()
        {
            // A UI theme token's id is its authored colour, but Photic's value is the theme's (re-hued), not the id.
            var authored = new Color(0x74 / 255f, 0x7B / 255f, 0xAD / 255f, 1f);
            var photicTheme = new Color(0.455f, 0.590f, 0.675f, 1f);
            var nocturneTheme = new Color(0.657f, 0.524f, 0.603f, 1f);
            var photic = SetWith(("ui:747BAD", photicTheme));
            var nocturne = SetWith(("ui:747BAD", nocturneTheme));

            ColorTrace.Wear(nocturne, photic);
            Assert.AreEqual(nocturneTheme, ColorTrace.Retrace(ColorTrace.Ui, authored), "the authored colour is found by its id");
            Assert.AreEqual(nocturneTheme, ColorTrace.Retrace(ColorTrace.Ui, photicTheme), "what Photic painted is found too");

            ColorTrace.Wear(null, photic);
            Assert.AreEqual(photicTheme, ColorTrace.Retrace(ColorTrace.Ui, authored), "under Photic the token is the theme's");
            Assert.AreEqual(photicTheme, ColorTrace.Retrace(ColorTrace.Ui, nocturneTheme));
        }

        [Test]
        public void Retrace_LeavesTheWornColourExactlyAsItIs()
        {
            var photic = SetWith(("world:FF8000", new Color(1f, 0.5f, 0f, 1f)));
            var nocturne = SetWith(("world:FF8000", new Color(230 / 255f, 153 / 255f, 26 / 255f, 1f)));
            ColorTrace.Wear(nocturne, photic);
            var held = new Color(230 / 255f + 0.0005f, 153 / 255f, 26 / 255f - 0.0005f, 0.25f);   // the worn colour, to its 8-bit id
            Assert.IsTrue(ColorTrace.TryRetrace(ColorTrace.World, held, out var worn));
            Assert.AreEqual(held, worn, "a colour the worn set already gives is left exactly as it is");

            ColorTrace.Wear(null, photic);
            var baked = new Color(1f, 128 / 255f + 0.0005f, 0f, 1f);   // one of several floats the id world:FF8000 groups
            Assert.IsTrue(ColorTrace.TryRetrace(ColorTrace.World, baked, out var same));
            Assert.AreEqual(baked, same, "wearing Photic leaves a baked colour alone");
        }

        [Test]
        public void Retrace_FindsWhatASetWornLongAgoPainted()
        {
            var photic = SetWith(("world:FF8000", new Color(1f, 0.5f, 0f, 1f)));
            var a = SetWith(("world:FF8000", new Color(0.9f, 0.6f, 0.1f, 1f)));
            var b = SetWith(("world:FF8000", new Color(1f, 0.8f, 0.6f, 1f)));
            var c = SetWith(("world:FF8000", new Color(0.7f, 0.2f, 0.9f, 1f)));
            ColorTrace.Wear(a, photic);
            ColorTrace.Wear(b, photic);
            ColorTrace.Wear(c, photic);
            Assert.AreEqual(new Color(0.7f, 0.2f, 0.9f, 1f), ColorTrace.Retrace(ColorTrace.World, new Color(0.9f, 0.6f, 0.1f, 1f)),
                "a copy that kept the first set's colour is still found two wears later");
        }

        [Test]
        public void EveryShippedSet_FindsWhatEveryOtherSetPainted()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ColorSetCatalogSO>(CatalogPath);
            Assert.IsNotNull(catalog);
            var photic = ColorTrace.FindAuthored(catalog);
            Assert.IsNotNull(photic);
            var sets = new List<SO_ColorSet>();
            for (int i = 0; i < catalog.Count; i++) sets.Add(catalog.Get(i).colorSet);
            foreach (var set in sets) ColorTrace.Wear(set, photic);       // every set worn once this session
            int checkedCount = 0;
            foreach (var worn in sets)
            {
                ColorTrace.Wear(worn, photic);
                foreach (var painter in sets)
                    foreach (var t in painter.TracedColors)
                    {
                        if (!t.Id.StartsWith(ColorTrace.Ui + ":") && !t.Id.StartsWith(ColorTrace.World + ":")) continue;
                        string role = t.Id.Substring(0, t.Id.IndexOf(':'));
                        Assert.IsTrue(ColorTrace.TryRetrace(role, t.Color, out var got), $"{painter.name}'s {t.Id} is not found");
                        Assert.IsTrue(worn.TryGetTracedColor(t.Id, out var want));
                        Assert.AreEqual(ColorTrace.ValueId(role, want), ColorTrace.ValueId(role, got),
                            $"{painter.name}'s {t.Id} wearing {worn.name} is read as another colour");
                        checkedCount++;
                    }
            }
            Assert.Greater(checkedCount, 0);
        }

        [Test]
        public void EveryShippedSet_CarriesEveryTracedColour_AndPhoticsValuesAreTheirIds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ColorSetCatalogSO>(CatalogPath);
            Assert.IsNotNull(catalog);
            var photic = catalog.Get(catalog.IndexOfId(ColorTrace.AuthoredSetId));
            Assert.IsNotNull(photic, "the catalog names no Photic");
            var ids = new HashSet<string>(photic.colorSet.TracedColors.Select(t => t.Id));
            Assert.Greater(ids.Count, 0, "Photic carries no traced colours: run python3 Tools/Build/colour_trace.py --write-photic");
            foreach (var t in photic.colorSet.TracedColors)
            {
                if (!t.Id.StartsWith(ColorTrace.Ui + ":") && !t.Id.StartsWith(ColorTrace.World + ":")) continue;
                // Photic holds the values the game was authored in, so each packs to its own id, the way a painter finds
                // it on screen. The one exception is decided: the old sky-blue selection is the style guide's Jade (D3).
                if (!string.IsNullOrEmpty(t.Note) && t.Note.StartsWith("UI theme token Selection")) continue;
                string role = t.Id.Substring(0, t.Id.IndexOf(':'));
                Assert.AreEqual(t.Id, ColorTrace.ValueId(role, t.Color), $"Photic's {t.Id} does not pack to its own id");
            }
            for (int i = 0; i < catalog.Count; i++)
            {
                var entry = catalog.Get(i);
                var got = new HashSet<string>(entry.colorSet.TracedColors.Select(t => t.Id));
                Assert.IsTrue(ids.SetEquals(got), $"{entry.id} does not carry the same traced colours as Photic: run " +
                                                  "python3 Tools/Build/author_colour_set_assets.py");
            }
        }
    }
}
#endif
