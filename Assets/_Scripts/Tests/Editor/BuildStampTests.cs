#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Editor;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The build stamp (<see cref="BuildStamp"/>): what a player says it is, which UGS environment it
    /// talks to, and how the settings line shows it. The environment rule is the one that matters
    /// most: a release player that silently talked to development would make every production
    /// figure (and the production-only debug-crystal refusal in economy.js) a lie, and a development
    /// build that talked to production would write test players into the live project.
    /// </summary>
    public class BuildStampTests
    {
        const string Sha = "1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d";

        static string Json(string environment = "production", string version = "0.3.0") =>
            JsonUtility.ToJson(new BuildStampData
            {
                version = version,
                buildNumber = "42",
                commit = Sha,
                gitRef = "launch/v1",
                channel = "playtest",
                ugsEnvironment = environment,
                builtAtUtc = "2026-11-05T12:00:00.0000000Z",
                development = false,
            });

        // ───────── parser ─────────

        [Test]
        public void Parse_ReadsEveryField()
        {
            Assert.IsTrue(BuildStamp.TryParse(Json(), out var s, out string error), error);
            Assert.AreEqual("0.3.0", s.version);
            Assert.AreEqual("42", s.buildNumber);
            Assert.AreEqual(Sha, s.commit);
            Assert.AreEqual("launch/v1", s.gitRef);
            Assert.AreEqual("playtest", s.channel);
            Assert.AreEqual("production", s.ugsEnvironment);
            Assert.AreEqual("2026-11-05T12:00:00.0000000Z", s.builtAtUtc);
            Assert.IsFalse(s.development);
        }

        [Test]
        public void Parse_NormalisesTheEnvironmentName()
        {
            Assert.IsTrue(BuildStamp.TryParse(Json(" Development "), out var s, out _));
            Assert.AreEqual("development", s.ugsEnvironment);
        }

        [Test]
        public void Parse_FillsMissingOptionalFieldsWithEmpty()
        {
            Assert.IsTrue(BuildStamp.TryParse("{\"version\":\"0.3.0\",\"ugsEnvironment\":\"development\"}", out var s, out _));
            Assert.AreEqual(string.Empty, s.commit);
            Assert.AreEqual(string.Empty, s.buildNumber);
            Assert.AreEqual(string.Empty, s.channel);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Parse_RefusesAnEmptyStamp(string json)
        {
            Assert.IsFalse(BuildStamp.TryParse(json, out var s, out string error));
            Assert.IsNull(s);
            StringAssert.Contains("empty", error);
        }

        [Test]
        public void Parse_RefusesMalformedJson() =>
            Assert.IsFalse(BuildStamp.TryParse("{not json", out _, out _));

        [Test]
        public void Parse_RefusesAStampWithNoVersion() =>
            Assert.IsFalse(BuildStamp.TryParse(Json(version: ""), out _, out _));

        [TestCase("staging")]
        [TestCase("")]
        [TestCase("prod")]
        public void Parse_RefusesAnUnknownEnvironment(string environment)
        {
            Assert.IsFalse(BuildStamp.TryParse(Json(environment), out _, out string error));
            StringAssert.Contains("environment", error);
        }

        [Test]
        public void EditorStamp_IsDevelopmentAndSaysEditor()
        {
            var s = BuildStamp.EditorStamp(null);
            Assert.AreEqual(BuildStamp.Development, s.ugsEnvironment);
            Assert.AreEqual(BuildStamp.EditorValue, s.commit);
            Assert.AreEqual(Application.version, s.version);
            Assert.IsTrue(s.development);
        }

        [Test]
        public void EditorStamp_HonoursAnExplicitEnvironment() =>
            Assert.AreEqual(BuildStamp.Production, BuildStamp.EditorStamp("production").ugsEnvironment);

        [Test]
        public void EditorStamp_IgnoresAnInvalidExplicitEnvironment()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("staging"));
            Assert.AreEqual(BuildStamp.Development, BuildStamp.EditorStamp("staging").ugsEnvironment);
        }

        // ───────── the environment rule ─────────

        [Test]
        public void Environment_ReleasePlayer_IsProduction() =>
            Assert.AreEqual("production", BuildStamp.ResolveUgsEnvironment(null, developmentBuild: false, isEditor: false));

        [Test]
        public void Environment_DevelopmentBuild_IsDevelopment() =>
            Assert.AreEqual("development", BuildStamp.ResolveUgsEnvironment(null, developmentBuild: true, isEditor: false));

        [Test]
        public void Environment_Editor_IsDevelopment() =>
            Assert.AreEqual("development", BuildStamp.ResolveUgsEnvironment(null, developmentBuild: false, isEditor: true));

        [Test]
        public void Environment_BlankExplicitValue_IsNotGiven() =>
            Assert.AreEqual("production", BuildStamp.ResolveUgsEnvironment("  ", developmentBuild: false, isEditor: false));

        [TestCase("production", true, "production")]
        [TestCase("PRODUCTION", true, "production")]
        [TestCase("development", false, "development")]
        [TestCase(" Development ", false, "development")]
        public void Environment_ExplicitValueWins(string given, bool developmentBuild, string expected) =>
            Assert.AreEqual(expected, BuildStamp.ResolveUgsEnvironment(given, developmentBuild, isEditor: false));

        [TestCase("staging")]
        [TestCase("prod")]
        [TestCase("dev")]
        public void Environment_UnknownExplicitValue_Throws(string given)
        {
            var ex = Assert.Throws<ArgumentException>(() => BuildStamp.ResolveUgsEnvironment(given, false, false));
            StringAssert.Contains(given, ex.Message);
        }

        [Test]
        public void Environment_TheBuildRefusesAnUnknownValue()
        {
            var ex = Assert.Throws<BuildFailedException>(() => BuildStampProcessor.ResolveEnvironmentOrRefuse("staging", development: false));
            StringAssert.Contains("staging", ex.Message);
        }

        [Test]
        public void Environment_TheBuildPassesTheRuleThrough()
        {
            Assert.AreEqual("production", BuildStampProcessor.ResolveEnvironmentOrRefuse(null, development: false));
            Assert.AreEqual("development", BuildStampProcessor.ResolveEnvironmentOrRefuse(null, development: true));
            Assert.AreEqual("production", BuildStampProcessor.ResolveEnvironmentOrRefuse("production", development: true));
        }

        // ───────── Android build number ─────────

        [Test]
        public void AndroidBuildNumber_NoneGiven_IsNull() =>
            Assert.IsNull(BuildStampProcessor.ParseAndroidBuildNumber(null));

        [Test]
        public void AndroidBuildNumber_PositiveWholeNumber_IsTheVersionCode() =>
            Assert.AreEqual(412826, BuildStampProcessor.ParseAndroidBuildNumber("412826"));

        [TestCase("0")]
        [TestCase("-3")]
        [TestCase("14.2")]
        [TestCase("abc")]
        public void AndroidBuildNumber_AnythingElse_FailsTheBuild(string given) =>
            Assert.Throws<BuildFailedException>(() => BuildStampProcessor.ParseAndroidBuildNumber(given));

        // ───────── inputs ─────────

        [Test]
        public void Clean_TrimsAndDropsControlCharacters()
        {
            Assert.AreEqual("abc def", BuildStampInputs.Clean("  abc\n def \r\n"));
            Assert.IsNull(BuildStampInputs.Clean("   "));
            Assert.AreEqual(128, BuildStampInputs.Clean(new string('x', 400)).Length);
        }

        // ───────── the settings line ─────────

        [Test]
        public void Fill_ReplacesEveryToken()
        {
            BuildStamp.TryParse(Json(), out var s, out _);
            Assert.AreEqual("Version 0.3.0 (42) 1a2b3c4d production playtest launch/v1",
                BuildStamp.Fill("Version {version} ({build}) {commit} {env} {channel} {ref}", s));
        }

        [Test]
        public void Fill_EmptyTemplate_ShowsNothing()
        {
            BuildStamp.TryParse(Json(), out var s, out _);
            Assert.AreEqual(string.Empty, BuildStamp.Fill("", s));
            Assert.AreEqual(string.Empty, BuildStamp.Fill(null, s));
        }

        [Test]
        public void Fill_EditorStamp_SaysEditor() =>
            Assert.AreEqual("editor development",
                BuildStamp.Fill("{commit} {env}", BuildStamp.EditorStamp(null)));

        [TestCase(Sha, "1a2b3c4d")]
        [TestCase("1a2b3c4d", "1a2b3c4d")]
        [TestCase("editor", "editor")]
        [TestCase("unknown", "unknown")]
        [TestCase("", "")]
        public void ShortCommit_ShortensOnlyAHexSha(string commit, string expected) =>
            Assert.AreEqual(expected, BuildStamp.ShortCommit(commit));

        // ───────── analytics ─────────

        [Test]
        public void PersonProperties_CarryTheBuild()
        {
            BuildStamp.TryParse(Json(), out var s, out _);
            var properties = new Dictionary<string, object>();
            AnalyticsServiceFacade.AddBuildProperties(properties, s);
            Assert.AreEqual("0.3.0", properties["build_version"]);
            Assert.AreEqual("42", properties["build_number"]);
            Assert.AreEqual(Sha, properties["build_commit"]);
            Assert.AreEqual("playtest", properties["release_channel"]);
            Assert.AreEqual("production", properties["ugs_environment"]);
            Assert.AreEqual(5, properties.Count);
        }
    }
}
#endif
