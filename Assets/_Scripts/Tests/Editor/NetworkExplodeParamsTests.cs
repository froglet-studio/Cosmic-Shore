using System;
using System.Reflection;
using CosmicShore.Gameplay;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="NetworkExplodeParams"/> is a DTO sitting between the two ends of the crystal
    /// explode RPC, and it is the ONLY thing that crosses the wire. A field added to
    /// <see cref="Crystal.ExplodeParams"/> and not to the DTO is silently reconstructed at its
    /// DEFAULT on the far side — including on the host, which runs the ClientRpc too — so the
    /// symptom is "my new flag does nothing, on every machine, with nothing in the log".
    ///
    /// That shipped once: <c>SuppressHusk</c> (the flag that stops a crystal spraying husks when a
    /// vessel is carrying its body onto something else) was added to the payload and not to the
    /// DTO, so every peer kept shattering a crystal that was supposed to be morphing.
    ///
    /// This test is written by REFLECTION rather than field by field, so it covers the next field
    /// too — nobody has to remember to extend it. A field of a type it cannot populate fails
    /// loudly, which is the right moment to be looking at this file.
    /// </summary>
    public class NetworkExplodeParamsTests
    {
        [Test]
        public void EveryExplodeParamsFieldSurvivesTheNetworkRoundTrip()
        {
            var type = typeof(Crystal.ExplodeParams);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotEmpty(fields, "ExplodeParams should have serialized fields");

            object boxed = default(Crystal.ExplodeParams);
            foreach (var f in fields)
                f.SetValue(boxed, DistinctValueFor(f));
            var sent = (Crystal.ExplodeParams)boxed;

            var got = NetworkExplodeParams.FromExplodeParams(sent).ToExplodeParams();

            object boxedGot = got;
            foreach (var f in fields)
            {
                object a = f.GetValue(boxed);
                object b = f.GetValue(boxedGot);
                Assert.AreEqual(a.ToString(), b.ToString(),
                    $"ExplodeParams.{f.Name} did not survive NetworkExplodeParams. Add it to the " +
                    "DTO's fields, its NetworkSerialize, its constructor, FromExplodeParams AND " +
                    "ToExplodeParams — all five, or it reads as its default on every peer.");
            }
        }

        /// <summary>A value that is NOT the field type's default, so a dropped field is detectable.
        /// An unknown type fails by name rather than being skipped — a silent skip would restore
        /// exactly the blind spot this test exists to close.</summary>
        static object DistinctValueFor(FieldInfo f)
        {
            if (f.FieldType == typeof(Vector3)) return new Vector3(1f, 2f, 3f);
            if (f.FieldType == typeof(float)) return 12.5f;
            if (f.FieldType == typeof(bool)) return true;
            if (f.FieldType == typeof(int)) return 7;
            if (f.FieldType == typeof(FixedString64Bytes)) return new FixedString64Bytes("test-pilot");
            if (f.FieldType == typeof(string)) return "test-pilot";
            if (f.FieldType.IsEnum) return Enum.GetValues(f.FieldType).GetValue(0);

            Assert.Fail($"ExplodeParams.{f.Name} is a {f.FieldType.Name}, which this test does not " +
                        "know how to populate. Teach it here — and while you are in this file, " +
                        "check the field was added to NetworkExplodeParams as well.");
            return null;
        }

        /// <summary>The flag the Scarab's crystal morph depends on, pinned explicitly as well as by
        /// the sweep above — a reflection test that silently found zero fields would still pass.</summary>
        [Test]
        public void SuppressHuskCrossesTheWire()
        {
            var sent = new Crystal.ExplodeParams { SuppressHusk = true };
            Assert.IsTrue(NetworkExplodeParams.FromExplodeParams(sent).ToExplodeParams().SuppressHusk,
                "a crystal being morphed onto something else must not also spray husks on remote peers");

            var normal = new Crystal.ExplodeParams { SuppressHusk = false };
            Assert.IsFalse(NetworkExplodeParams.FromExplodeParams(normal).ToExplodeParams().SuppressHusk,
                "an ordinary collect must still spray");
        }
    }
}
