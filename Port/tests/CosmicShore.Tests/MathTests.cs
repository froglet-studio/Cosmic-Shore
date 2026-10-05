using CosmicShore.Engine;

namespace CosmicShore.Tests;

public class Vector3Tests
{
    const float Tolerance = 1e-4f;

    static void AssertApprox(Vector3 expected, Vector3 actual)
    {
        Assert.True((expected - actual).magnitude < Tolerance,
            $"Expected {expected}, got {actual}");
    }

    [Fact]
    public void Cross_FollowsEngineHandedness()
    {
        // right × up = forward — the convention every tuned maneuver in the game relies on.
        AssertApprox(Vector3.forward, Vector3.Cross(Vector3.right, Vector3.up));
        AssertApprox(Vector3.up, Vector3.Cross(Vector3.forward, Vector3.right));
        AssertApprox(Vector3.right, Vector3.Cross(Vector3.up, Vector3.forward));
    }

    [Fact]
    public void Normalized_UnitLength()
    {
        var v = new Vector3(3f, 4f, 12f);
        Assert.Equal(1f, v.normalized.magnitude, 4);
        Assert.Equal(13f, v.magnitude, 4);
    }

    [Fact]
    public void Normalized_ZeroVector_IsZero()
        => AssertApprox(Vector3.zero, Vector3.zero.normalized);

    [Fact]
    public void Lerp_ClampsT()
    {
        AssertApprox(Vector3.one, Vector3.Lerp(Vector3.zero, Vector3.one, 2f));
        AssertApprox(Vector3.zero, Vector3.Lerp(Vector3.zero, Vector3.one, -1f));
        AssertApprox(new Vector3(0.5f, 0.5f, 0.5f), Vector3.Lerp(Vector3.zero, Vector3.one, 0.5f));
    }

    [Fact]
    public void Angle_KnownValues()
    {
        Assert.Equal(90f, Vector3.Angle(Vector3.right, Vector3.up), 3);
        Assert.Equal(180f, Vector3.Angle(Vector3.right, Vector3.left), 3);
        Assert.Equal(0f, Vector3.Angle(Vector3.right, Vector3.right), 3);
    }

    [Fact]
    public void SignedAngle_SignFollowsAxis()
    {
        Assert.Equal(90f, Vector3.SignedAngle(Vector3.forward, Vector3.right, Vector3.up), 3);
        Assert.Equal(-90f, Vector3.SignedAngle(Vector3.right, Vector3.forward, Vector3.up), 3);
    }

    [Fact]
    public void Project_OntoAxis()
        => AssertApprox(new Vector3(3f, 0f, 0f), Vector3.Project(new Vector3(3f, 4f, 0f), Vector3.right));

    [Fact]
    public void Reflect_OffPlane()
        => AssertApprox(new Vector3(1f, 1f, 0f), Vector3.Reflect(new Vector3(1f, -1f, 0f), Vector3.up));

    [Fact]
    public void ClampMagnitude_LimitsLength()
    {
        var clamped = Vector3.ClampMagnitude(new Vector3(10f, 0f, 0f), 2f);
        Assert.Equal(2f, clamped.magnitude, 4);
        AssertApprox(new Vector3(1f, 0f, 0f), Vector3.ClampMagnitude(new Vector3(1f, 0f, 0f), 2f));
    }

    [Fact]
    public void MoveTowards_DoesNotOvershoot()
    {
        AssertApprox(new Vector3(1f, 0f, 0f), Vector3.MoveTowards(Vector3.zero, new Vector3(5f, 0f, 0f), 1f));
        AssertApprox(new Vector3(5f, 0f, 0f), Vector3.MoveTowards(Vector3.zero, new Vector3(5f, 0f, 0f), 10f));
    }
}

public class QuaternionTests
{
    const float Tolerance = 1e-3f;

    static void AssertApprox(Vector3 expected, Vector3 actual)
    {
        Assert.True((expected - actual).magnitude < Tolerance,
            $"Expected {expected}, got {actual}");
    }

    [Fact]
    public void Identity_RotatesNothing()
        => AssertApprox(Vector3.forward, Quaternion.identity * Vector3.forward);

    [Fact]
    public void Euler_Yaw90_RotatesForwardToRight()
        => AssertApprox(Vector3.right, Quaternion.Euler(0f, 90f, 0f) * Vector3.forward);

    [Fact]
    public void Euler_Pitch90_RotatesForwardToDown()
        => AssertApprox(Vector3.down, Quaternion.Euler(90f, 0f, 0f) * Vector3.forward);

    [Fact]
    public void Euler_Roll90_RotatesUpToLeft()
        => AssertApprox(Vector3.left, Quaternion.Euler(0f, 0f, 90f) * Vector3.up);

    [Fact]
    public void EulerAngles_RoundTrips()
    {
        var euler = new Vector3(30f, 60f, 45f);
        var result = Quaternion.Euler(euler).eulerAngles;
        AssertApprox(euler, result);
    }

    [Fact]
    public void EulerAngles_NegativeInput_WrapsTo0To360()
    {
        var result = Quaternion.Euler(-30f, -60f, -45f).eulerAngles;
        AssertApprox(new Vector3(330f, 300f, 315f), result);
    }

    [Fact]
    public void LookRotation_PointsForwardAtTarget()
    {
        var directions = new[]
        {
            Vector3.right, Vector3.left, Vector3.up,
            new Vector3(1f, 2f, 3f).normalized, new Vector3(-0.5f, 0.1f, 0.8f).normalized
        };
        foreach (var dir in directions)
        {
            var rotated = Quaternion.LookRotation(dir) * Vector3.forward;
            AssertApprox(dir, rotated);
        }
    }

    [Fact]
    public void LookRotation_MatchesEulerYaw()
        => Assert.True(Quaternion.Angle(Quaternion.LookRotation(Vector3.right), Quaternion.Euler(0f, 90f, 0f)) < 0.01f);

    [Fact]
    public void FromToRotation_MapsFromOntoTo()
    {
        var from = Vector3.forward;
        var to = new Vector3(1f, 1f, 0f).normalized;
        AssertApprox(to, Quaternion.FromToRotation(from, to) * from);
    }

    [Fact]
    public void FromToRotation_OppositeVectors()
    {
        var rotated = Quaternion.FromToRotation(Vector3.forward, Vector3.back) * Vector3.forward;
        AssertApprox(Vector3.back, rotated);
    }

    [Fact]
    public void Inverse_UndoesRotation()
    {
        var q = Quaternion.Euler(25f, 130f, -40f);
        var v = new Vector3(1f, 2f, 3f);
        AssertApprox(v, Quaternion.Inverse(q) * (q * v));
    }

    [Fact]
    public void Slerp_Halfway()
    {
        var half = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(0f, 90f, 0f), 0.5f);
        AssertApprox((Vector3.forward + Vector3.right).normalized, half * Vector3.forward);
    }

    [Fact]
    public void Angle_KnownRotation()
        => Assert.Equal(90f, Quaternion.Angle(Quaternion.identity, Quaternion.Euler(0f, 90f, 0f)), 2);

    [Fact]
    public void RotateTowards_DoesNotOvershoot()
    {
        var target = Quaternion.Euler(0f, 90f, 0f);
        var stepped = Quaternion.RotateTowards(Quaternion.identity, target, 30f);
        Assert.Equal(30f, Quaternion.Angle(Quaternion.identity, stepped), 2);
        var full = Quaternion.RotateTowards(Quaternion.identity, target, 500f);
        Assert.True(Quaternion.Angle(full, target) < 0.01f);
    }

    [Fact]
    public void Multiplication_ComposesRotations()
    {
        // Yaw then pitch (intrinsic): forward → right (yaw 90), unaffected by pitch about new forward axis path.
        var yaw = Quaternion.Euler(0f, 90f, 0f);
        var roll = Quaternion.Euler(0f, 0f, 90f);
        var composed = yaw * roll;
        // roll first about z (up→left), then yaw about y (left→forward... left stays left under yaw? No: left → forward).
        AssertApprox(yaw * (roll * Vector3.up), composed * Vector3.up);
    }
}

public class MathfTests
{
    [Fact]
    public void Clamp_And_Clamp01()
    {
        Assert.Equal(5f, Mathf.Clamp(10f, 0f, 5f));
        Assert.Equal(0f, Mathf.Clamp(-10f, 0f, 5f));
        Assert.Equal(1f, Mathf.Clamp01(2f));
        Assert.Equal(0f, Mathf.Clamp01(-2f));
    }

    [Fact]
    public void Lerp_And_InverseLerp_Inverses()
    {
        Assert.Equal(7.5f, Mathf.Lerp(5f, 10f, 0.5f), 4);
        Assert.Equal(0.5f, Mathf.InverseLerp(5f, 10f, 7.5f), 4);
    }

    [Fact]
    public void Repeat_WrapsPositiveAndNegative()
    {
        Assert.Equal(1f, Mathf.Repeat(361f, 360f), 3);
        Assert.Equal(359f, Mathf.Repeat(-1f, 360f), 3);
    }

    [Fact]
    public void DeltaAngle_ShortestPath()
    {
        Assert.Equal(20f, Mathf.DeltaAngle(350f, 10f), 3);
        Assert.Equal(-20f, Mathf.DeltaAngle(10f, 350f), 3);
    }

    [Fact]
    public void PingPong_Reflects()
    {
        Assert.Equal(3f, Mathf.PingPong(3f, 5f), 3);
        Assert.Equal(4f, Mathf.PingPong(6f, 5f), 3);
    }

    [Fact]
    public void Approximately_NearbyFloats()
    {
        Assert.True(Mathf.Approximately(1f, 1f + 1e-7f));
        Assert.False(Mathf.Approximately(1f, 1.001f));
    }

    [Fact]
    public void SmoothDamp_ConvergesWithoutOvershoot()
    {
        float current = 0f, velocity = 0f, target = 10f;
        for (int i = 0; i < 200; i++)
        {
            current = Mathf.SmoothDamp(current, target, ref velocity, 0.3f, Mathf.Infinity, 1f / 60f);
            Assert.True(current <= target + 0.001f, $"Overshot at iteration {i}: {current}");
        }
        Assert.True(Mathf.Abs(current - target) < 0.01f, $"Did not converge: {current}");
    }

    [Fact]
    public void MoveTowardsAngle_TakesShortestPath_Unwrapped()
    {
        // Reference behavior: steps along the shortest arc but does NOT wrap the result
        // (350 + 15 toward 370 ⇒ 365, equivalent to 5°). Callers wrap when needed.
        Assert.Equal(365f, Mathf.MoveTowardsAngle(350f, 10f, 15f), 3);
        // Once within maxDelta, the original (wrapped) target is returned as-is.
        Assert.Equal(10f, Mathf.MoveTowardsAngle(350f, 10f, 30f), 3);
    }
}
