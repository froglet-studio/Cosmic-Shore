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
                // a UI theme token's value is the theme's (Photic's too); every other Photic value IS the baked colour,
                // so it packs to its own id, the way a painter finds it on screen
                if (!string.IsNullOrEmpty(t.Note) && t.Note.StartsWith("UI theme token")) continue;
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
