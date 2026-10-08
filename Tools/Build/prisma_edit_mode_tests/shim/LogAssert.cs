// Scratch stand-in for Unity Test Framework's LogAssert, with Unity's rules: an Error, Assert or
// Exception log a test did not Expect fails that test; an Expect'ed log that never arrives fails it.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: CosmicShore.Engine.TestTools.UnityLogRules]

namespace CosmicShore.Engine.TestTools
{
    public static class LogAssert
    {
        internal sealed class Expectation { public LogType Type; public string Text; public Regex Rx; }
        internal static readonly List<Expectation> Pending = new();
        internal static readonly List<string> Unexpected = new();
        static bool s_hooked;
        public static bool ignoreFailingMessages { get; set; }

        internal static void Begin()
        {
            Pending.Clear(); Unexpected.Clear(); ignoreFailingMessages = false;
            if (!s_hooked) { Application.logMessageReceived += OnLog; s_hooked = true; }
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            for (int i = 0; i < Pending.Count; i++)
            {
                var e = Pending[i];
                if (e.Type != type) continue;
                if (e.Rx != null ? e.Rx.IsMatch(condition ?? "") : condition == e.Text) { Pending.RemoveAt(i); return; }
            }
            if (type is LogType.Error or LogType.Exception or LogType.Assert) Unexpected.Add($"{type}: {condition}");
        }

        public static void Expect(LogType type, string message) => Pending.Add(new Expectation { Type = type, Text = message });
        public static void Expect(LogType type, Regex message) => Pending.Add(new Expectation { Type = type, Rx = message });
        public static void Expect(string message) => Expect(LogType.Log, message);
        public static void Expect(Regex message) => Expect(LogType.Log, message);
        public static void NoUnexpectedReceived()
        {
            if (Unexpected.Count > 0 && !ignoreFailingMessages) Assert.Fail("Unexpected log: " + Unexpected[0]);
        }

        internal static void End()
        {
            if (Pending.Count > 0)
                Assert.Fail($"Expected log not received: {Pending[0].Type} \"{Pending[0].Text ?? Pending[0].Rx?.ToString()}\"");
            NoUnexpectedReceived();
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class UnityLogRulesAttribute : System.Attribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Test;
        public void BeforeTest(ITest test) => LogAssert.Begin();
        public void AfterTest(ITest test)
        {
            if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Passed) LogAssert.End();
        }
    }
}
