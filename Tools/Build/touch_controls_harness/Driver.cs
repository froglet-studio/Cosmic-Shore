// Runs the SHIPPED TiltMath.cs and TouchButtonLayout.cs (Docs/Reports/MOBILE_VESSELS.md section 8).
// The same assertions as TouchControlsTests, so the layout and the tilt signs are checked without
// Unity, plus negative controls that prove the overlap and sign checks can fail.
using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using UnityEngine;

static class Driver
{
    static int failures;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); if (!ok) failures++; }

    static Vector3 Gravity(float rollDeg, float pitchDeg)
    {
        float p = pitchDeg * Mathf.Deg2Rad, r = rollDeg * Mathf.Deg2Rad;
        float inPlane = Mathf.Cos(p);
        return new Vector3(-Mathf.Cos(r) * inPlane, -(Mathf.Sin(r) * inPlane), -Mathf.Sin(p));
    }

    static bool Overlapping(TouchButtonLayout.Result r, List<float> aspects)
    {
        float w = r.ButtonWidthPixels;
        for (int i = 0; i < r.Centres.Count; i++)
            for (int j = 0; j < i; j++)
            {
                var d = r.Centres[j] - r.Centres[i];
                if (Math.Abs(d.x) < w - 0.01f && Math.Abs(d.y) < w * (aspects[i] + aspects[j]) / 2 - 0.01f) return true;
            }
        return false;
    }

    static int Main()
    {
        Console.WriteLine("Tilt");
        var refPose = TiltMath.Angles(Gravity(0, 30));
        var right = TiltMath.Stick(TiltMath.Angles(Gravity(15, 30)), refPose, 25, 2.5f);
        Check(right.x > 0.5f && Math.Abs(right.y) < 0.05f, $"clockwise wheel turn reads right, no pitch {right}");
        var fwd = TiltMath.Stick(TiltMath.Angles(Gravity(0, 45)), refPose, 25, 2.5f);
        Check(fwd.y > 0.5f && Math.Abs(fwd.x) < 0.05f, $"tipping the top away reads forward, no turn {fwd}");
        Check(TiltMath.Stick(refPose, refPose, 25, 2.5f) == Vector2.zero, "the held pose is neutral");
        // Negative control: a frame with the roll sign flipped must read LEFT.
        var flipped = TiltMath.Stick(TiltMath.Angles(Gravity(-15, 30)), refPose, 25, 2.5f);
        Check(flipped.x < -0.5f, $"negative control: anticlockwise reads left {flipped}");

        Console.WriteLine("Layout");
        var settings = new TouchButtonLayout.Settings { ButtonMillimetres = 10, GapMillimetres = 1.5f,
            InnerArcMillimetres = 22, ArcSpacingMillimetres = 12, MaxHeightFraction = 0.55f, MaxWidthFraction = 0.36f };
        var six = new List<float> { 1, 1, 1.75f, 1.75f, 1.75f, 1.75f };
        foreach (var (name, w, h, dpi, minMm) in new[] { ("6.0in 1080p", 2400, 1080, 400f, 8.4f),
                                                         ("4.4in 720p", 1280, 720, 294f, 7.0f),
                                                         ("12.9in tablet", 2732, 2048, 264f, 9.9f) })
        {
            var r = TouchButtonLayout.Arrange(six, new Vector2(w, h), new Rect(0, 0, w, h), dpi, settings);
            float extentX = 0, extentY = 0;
            foreach (var c in r.Centres) { extentX = Math.Max(extentX, w - c.x); extentY = Math.Max(extentY, c.y); }
            Check(r.Fits && r.Centres.Count == 6 && r.ButtonMillimetres >= minMm && !Overlapping(r, six),
                  $"{name}: six seat at {r.ButtonMillimetres:F1} mm, no overlap, centres within " +
                  $"{extentX / dpi * 25.4f:F0} x {extentY / dpi * 25.4f:F0} mm of the corner");
        }
        var notchSafe = new Rect(0, 60, 2250, 1020);
        var four = new List<float> { 1.75f, 1.75f, 1.75f, 1.75f };
        var n = TouchButtonLayout.Arrange(four, new Vector2(2400, 1080), notchSafe, 400, settings);
        bool inside = true;
        foreach (var c in n.Centres)
            inside &= c.x + n.ButtonWidthPixels / 2 <= notchSafe.xMax + 0.5f && c.y - n.ButtonWidthPixels * 1.75f / 2 >= notchSafe.yMin - 0.5f;
        Check(n.Fits && inside, "a notched safe area moves the buttons with it");
        // Negative control: the overlap check fires on a layout forced into one spot.
        var fake = new TouchButtonLayout.Result { Centres = new List<Vector2> { new Vector2(100, 100), new Vector2(110, 100) }, ButtonWidthPixels = 50 };
        Check(Overlapping(fake, new List<float> { 1, 1 }), "negative control: two buttons 10 px apart overlap");

        Console.WriteLine(failures == 0 ? "touch_controls_harness: OK" : $"touch_controls_harness: {failures} FAILED");
        return failures == 0 ? 0 : 1;
    }
}
