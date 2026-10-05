using System;
using System.Reflection;
using CosmicShore.Engine.Services;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// The UGS-core error surface (engine RequestFailedException with ErrorCode) and
// the two classifiers it un-carried: NetworkDiagnostics.ClassifyException's
// typed request-layer arm, and HostConnectionService.IsDefiniteSessionGoneException's
// HTTP-404 arm (incl. the inner-exception walk).
// ─────────────────────────────────────────────────────────────────────────────

public class UgsErrorSurfaceTests
{
    [Theory]
    [InlineData(429, "RateLimit")]
    [InlineData(404, "SessionGone")]
    [InlineData(500, "Transient")]
    [InlineData(503, "Transient")]
    [InlineData(-1, "Offline")]
    [InlineData(0, "Offline")]
    [InlineData(403, "Transient")] // any other client error → Transient (upstream default arm)
    public void ClassifyException_MapsRequestFailedErrorCodes(int errorCode, string expected)
        => Assert.Equal(expected, NetworkDiagnostics.ClassifyException(
            new RequestFailedException(errorCode, "boom")));

    [Fact]
    public void ClassifyException_UnwrapsOneAggregateLayer()
        => Assert.Equal("SessionGone", NetworkDiagnostics.ClassifyException(
            new AggregateException(new RequestFailedException(404, "gone"))));

    static readonly MethodInfo SessionGone = typeof(HostConnectionService).GetMethod(
        "IsDefiniteSessionGoneException", BindingFlags.Static | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData(404, true)]
    [InlineData(500, false)] // non-404 falls to the message match, which "boom" never satisfies
    public void SessionGoneClassifier_ReadsHttp404RequestFailures(int errorCode, bool expected)
        => Assert.Equal(expected, (bool)SessionGone.Invoke(
            null, new object[] { new RequestFailedException(errorCode, "boom") }));

    [Fact]
    public void SessionGoneClassifier_WalksTheInnerChain()
    {
        var wrapped = new Exception("outer", new RequestFailedException(404, "gone"));
        Assert.True((bool)SessionGone.Invoke(null, new object[] { wrapped }));
    }
}
