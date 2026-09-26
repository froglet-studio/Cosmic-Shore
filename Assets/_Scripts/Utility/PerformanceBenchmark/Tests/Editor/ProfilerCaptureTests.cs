using System.Collections.Generic;
using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    /// <summary>
    /// The pure half of the <c>prof</c> console command. The load-bearing tests are the two
    /// negative controls: the Profiler's GC column is INCLUSIVE, so ranking it raw names
    /// <c>PlayerLoop</c> as the allocator every time (the report must name the code that
    /// allocated instead); and an average must be per CAPTURED frame, or a marker that runs one
    /// frame in ten reads ten times cheaper than the frame budget it actually costs.
    /// </summary>
    [TestFixture]
    public class ProfilerCaptureTests
    {
        const string S = ProfilerCapture.PathSeparator;

        struct N
        {
            public string Path;
            public float Total, Self, Gc, Calls;
            public N(string path, float total, float self, float gc = 0f, float calls = 1f)
            { Path = path; Total = total; Self = self; Gc = gc; Calls = calls; }
        }

        /// <summary>Feeds one frame, parent-first, the way the reader walks it.</summary>
        static void AddFrame(ProfilerCapture.Accumulator acc, int index, float frameMs, params N[] nodes)
        {
            acc.BeginFrame(index, frameMs);
            foreach (var n in nodes)
            {
                int cut = n.Path.LastIndexOf(S, System.StringComparison.Ordinal);
                string parent = cut < 0 ? "" : n.Path.Substring(0, cut);
                string name = cut < 0 ? n.Path : n.Path.Substring(cut + S.Length);
                int depth = 0;
                for (int i = n.Path.IndexOf(S, System.StringComparison.Ordinal); i >= 0;
                     i = n.Path.IndexOf(S, i + S.Length, System.StringComparison.Ordinal)) depth++;
                acc.AddNode(n.Path, parent, name, depth, n.Total, n.Self, n.Gc, n.Calls);
            }
        }

        static string P(params string[] parts) => string.Join(S, parts);

        /// <summary>A frame with a real allocator two levels under PlayerLoop, plus editor time.</summary>
        static N[] TypicalNodes() => new[]
        {
            new N(P("PlayerLoop"), 10f, 0f, 3072f),
            new N(P("PlayerLoop", "UpdateScene"), 8f, 0.5f, 3072f),
            new N(P("PlayerLoop", "UpdateScene", "Foo.Update()"), 5f, 1f, 2048f),
            new N(P("PlayerLoop", "UpdateScene", "Foo.Update()", "GC.Alloc"), 0.01f, 0.01f, 2048f, 12f),
            new N(P("PlayerLoop", "UpdateScene", "Bar.Update()"), 2.5f, 2.5f, 1024f),
            new N(P("PlayerLoop", "Render"), 1.5f, 1.5f),
            new N(P("EditorLoop"), 3f, 3f),
        };

        static ProfilerCapture.Report BuildFrom(ProfilerCapture.Accumulator acc, ProfilerCapture.Options o,
                                                ProfilerCapture.ThreadAccumulator threads = null)
        {
            var r = new ProfilerCapture.Report { completed = true };
            ProfilerCapture.Build(r, acc, threads ?? new ProfilerCapture.ThreadAccumulator(), null, null, o);
            return r;
        }

        static ProfilerCapture.Options Opts(params string[] args)
        {
            Assert.IsTrue(ProfilerCapture.TryParse(args, out var o, out string error), error);
            return o;
        }

        static ProfilerCapture.Row Find(List<ProfilerCapture.Row> rows, string name)
        {
            foreach (var r in rows) if (r.name == name) return r;
            return null;
        }

        #region Parse

        [Test]
        public void TryParse_Empty_UsesDefaults()
        {
            var o = Opts();
            Assert.AreEqual("", o.label);
            Assert.AreEqual(ProfilerCapture.DefaultFrames, o.frames);
            Assert.AreEqual(ProfilerCapture.SortKey.Total, o.sortKey);
            Assert.AreEqual(ProfilerCapture.DefaultMinMs, o.minMs, 1e-6f);
            Assert.AreEqual(ProfilerCapture.DefaultMinGcKB, o.minGcKB, 1e-6f);
            Assert.AreEqual(ProfilerCapture.DefaultDepth, o.depth);
            Assert.AreEqual(ProfilerCapture.DefaultTop, o.top);
        }

        [Test]
        public void TryParse_EveryOption_InAnyOrder()
        {
            var o = Opts("sort=self", "S2_Lattice", "root=UpdateScene", "300", "min=0.1", "mingc=4", "depth=5", "top=10");
            Assert.AreEqual("S2_Lattice", o.label);
            Assert.AreEqual(300, o.frames);
            Assert.AreEqual("UpdateScene", o.root);
            Assert.AreEqual(ProfilerCapture.SortKey.Self, o.sortKey);
            Assert.AreEqual("self", o.sort);
            Assert.AreEqual(0.1f, o.minMs, 1e-6f);
            Assert.AreEqual(4f, o.minGcKB, 1e-6f);
            Assert.AreEqual(5, o.depth);
            Assert.AreEqual(10, o.top);
        }

        [Test]
        public void TryParse_ClampsToTheRange()
        {
            Assert.AreEqual(ProfilerCapture.MaxFrames, Opts("99999").frames);
            Assert.AreEqual(ProfilerCapture.MinFrames, Opts("3").frames);
            Assert.AreEqual(ProfilerCapture.MaxDepth, Opts("depth=999").depth);
            Assert.AreEqual(ProfilerCapture.MaxTop, Opts("top=99999").top);
        }

        static IEnumerable<TestCaseData> RejectedInputs()
        {
            yield return new TestCaseData((object)new[] { "sort=fast" }).SetName("TryParse_UnknownSort_Fails");
            yield return new TestCaseData((object)new[] { "min=-1" }).SetName("TryParse_NegativeMin_Fails");
            yield return new TestCaseData((object)new[] { "mingc=lots" }).SetName("TryParse_NonNumericMinGc_Fails");
            yield return new TestCaseData((object)new[] { "depth=0" }).SetName("TryParse_ZeroDepth_Fails");
            yield return new TestCaseData((object)new[] { "bogus=1" }).SetName("TryParse_UnknownKey_Fails");
            yield return new TestCaseData((object)new[] { "root=" }).SetName("TryParse_EmptyValue_Fails");
            yield return new TestCaseData((object)new[] { "a", "b" }).SetName("TryParse_TwoLabels_Fails");
            yield return new TestCaseData((object)new[] { "100", "200" }).SetName("TryParse_TwoFrameCounts_Fails");
            yield return new TestCaseData((object)new[] { "0" }).SetName("TryParse_ZeroFrames_Fails");
        }

        [TestCaseSource(nameof(RejectedInputs))]
        public void TryParse_Rejects(string[] tokens)
        {
            Assert.IsFalse(ProfilerCapture.TryParse(tokens, out _, out string error));
            StringAssert.Contains("usage", error);
        }

        #endregion

        #region Averages and allocation

        /// <summary>
        /// NEGATIVE CONTROL for per-frame averaging. A marker present in one frame of two costs
        /// half its time per frame. Dividing by the frames it appeared in would report it at full
        /// cost; this asserts the per-captured-frame number and how often it appeared.
        /// </summary>
        [Test]
        public void Averages_ArePerCapturedFrame_IncludingFramesTheRowWasAbsentFrom()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 10f, new N(P("PlayerLoop"), 10f, 5f), new N(P("PlayerLoop", "Burst()"), 5f, 5f));
            AddFrame(acc, 2, 5f, new N(P("PlayerLoop"), 5f, 5f));

            var burst = Find(BuildFrom(acc, Opts()).tree, "Burst()");
            Assert.AreEqual(2.5f, burst.avgTotalMs, 1e-5f, "5 ms once in 2 frames is 2.5 ms per frame");
            Assert.AreEqual(50f, burst.presentPct, 1e-4f);
            Assert.AreEqual(5f, burst.maxTotalMs, 1e-5f);
        }

        [Test]
        public void SelfAllocation_IsInclusiveMinusChildren()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 13f, TypicalNodes());
            var r = BuildFrom(acc, Opts());

            Assert.AreEqual(0f, Find(r.tree, "Foo.Update()").avgSelfGcKB, 1e-5f, "all of Foo's allocation is in its GC.Alloc child");
            Assert.AreEqual(2f, Find(r.tree, "GC.Alloc").avgSelfGcKB, 1e-5f);
            Assert.AreEqual(1f, Find(r.tree, "Bar.Update()").avgSelfGcKB, 1e-5f, "Bar allocates in its own body");
            Assert.AreEqual(0f, Find(r.tree, "PlayerLoop").avgSelfGcKB, 1e-5f);
        }

        /// <summary>
        /// NEGATIVE CONTROL for the allocation ranking. Sorting the Profiler's (inclusive) GC
        /// column puts PlayerLoop on top - that is the naive answer, asserted here so the test
        /// proves the fixture can produce it. The report must name the real allocator instead.
        /// </summary>
        [Test]
        public void TopGc_NamesTheAllocator_NotTheLoopThatContainsIt()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 13f, TypicalNodes());
            var r = BuildFrom(acc, Opts());

            ProfilerCapture.Row naive = null;
            foreach (var row in r.tree) if (naive == null || row.avgGcKB > naive.avgGcKB) naive = row;
            Assert.AreEqual("PlayerLoop", naive.name, "the negative control must fire: inclusive GC ranks the loop first");

            Assert.AreEqual("GC.Alloc", r.topGc[0].name);
            Assert.AreEqual("Foo.Update()", ProfilerCapture.OwnerOf(r.topGc[0].path));
            Assert.AreEqual("Bar.Update()", r.topGc[1].name);
            foreach (var row in r.topGc) Assert.AreNotEqual("PlayerLoop", row.name);
        }

        [Test]
        public void TopSelf_SumsOneMarkerAcrossEveryPathItAppearsOn()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 5f,
                new N(P("PlayerLoop"), 5f, 0f),
                new N(P("PlayerLoop", "A"), 2f, 1f),
                new N(P("PlayerLoop", "A", "Mesh.Update()"), 1f, 1f),
                new N(P("PlayerLoop", "B"), 3f, 1.5f),
                new N(P("PlayerLoop", "B", "Mesh.Update()"), 1.5f, 1.5f));

            var r = BuildFrom(acc, Opts());
            Assert.AreEqual("Mesh.Update()", r.topSelf[0].name);
            Assert.AreEqual(2.5f, r.topSelf[0].avgSelfMs, 1e-5f);
        }

        [Test]
        public void EditorTime_IsFlagged_AndLeftOutOfTheConsoleLine()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 13f, TypicalNodes());
            var r = BuildFrom(acc, Opts());

            Assert.AreEqual("EditorLoop", r.topSelf[0].name, "EditorLoop really is the largest self time in this frame");
            Assert.IsTrue(r.topSelf[0].editorOnly);

            string line = ProfilerCapture.Summarize(r);
            StringAssert.DoesNotContain("EditorLoop", line);
            StringAssert.Contains("Bar.Update() 2.50", line);
            StringAssert.Contains("top alloc: Foo.Update() 2.0 KB/f", line);
        }

        #endregion

        #region Tree shape

        [Test]
        public void Tree_PrunesByTimeOrAllocation_AndNeverOrphansAChild()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 13f, TypicalNodes());

            var kept = BuildFrom(acc, Opts("min=1")).tree;
            Assert.IsNotNull(Find(kept, "Render"), "1.5 ms passes min=1");
            Assert.IsNotNull(Find(kept, "GC.Alloc"), "0.01 ms but 2 KB passes mingc=1");

            var tighter = BuildFrom(acc, Opts("min=2", "mingc=4")).tree;
            Assert.IsNull(Find(tighter, "Render"));
            Assert.IsNull(Find(tighter, "GC.Alloc"));
            Assert.IsNotNull(Find(tighter, "Foo.Update()"));
        }

        [Test]
        public void Tree_SortsSiblingsByTheChosenColumn()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 13f, TypicalNodes());

            var byTotal = BuildFrom(acc, Opts()).tree;
            Assert.Less(byTotal.IndexOf(Find(byTotal, "Foo.Update()")), byTotal.IndexOf(Find(byTotal, "Bar.Update()")));

            var bySelf = BuildFrom(acc, Opts("sort=self")).tree;
            Assert.Less(bySelf.IndexOf(Find(bySelf, "Bar.Update()")), bySelf.IndexOf(Find(bySelf, "Foo.Update()")),
                        "Bar's 2.5 ms of self time outranks Foo's 1 ms");
        }

        [Test]
        public void Tree_Root_StartsAtTheMatchedNode_WithRelativeDepth()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 13f, TypicalNodes());
            var r = BuildFrom(acc, Opts("root=updatescene"));

            Assert.AreEqual("UpdateScene", r.tree[0].name);
            Assert.AreEqual(0, r.tree[0].depth);
            Assert.AreEqual(P("PlayerLoop", "UpdateScene"), r.rootsMatched[0]);
            Assert.IsNull(Find(r.tree, "PlayerLoop"));
            Assert.IsNull(Find(r.tree, "EditorLoop"));
            Assert.IsNull(Find(r.tree, "Render"));
            Assert.AreEqual("Foo.Update()", ProfilerCapture.OwnerOf(r.topGc[0].path), "top lists follow the root too");
        }

        [Test]
        public void Tree_DepthLimit_IsRelativeToTheRoot()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 13f, TypicalNodes());
            var r = BuildFrom(acc, Opts("root=UpdateScene", "depth=1"));

            Assert.IsNotNull(Find(r.tree, "Foo.Update()"));
            Assert.IsNull(Find(r.tree, "GC.Alloc"), "GC.Alloc is two below the root");
        }

        [Test]
        public void Tree_RootThatMatchesNothing_SaysSo()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 13f, TypicalNodes());
            var r = BuildFrom(acc, Opts("root=NoSuchMarker"));

            Assert.AreEqual(0, r.tree.Count);
            StringAssert.Contains("matched nothing", string.Join(" ", r.notes));
        }

        [Test]
        public void FrameTrees_AreSingleFrameValues_RootedLikeTheMergedTree()
        {
            var root = new ProfilerCapture.FrameNode { name = "<frame>" };
            var loop = new ProfilerCapture.FrameNode { name = "PlayerLoop", totalMs = 30f };
            var scene = new ProfilerCapture.FrameNode { name = "UpdateScene", totalMs = 25f, selfMs = 1f };
            scene.children.Add(new ProfilerCapture.FrameNode { name = "Small()", totalMs = 4f, selfMs = 4f });
            scene.children.Add(new ProfilerCapture.FrameNode { name = "Grow()", totalMs = 20f, selfMs = 8f, gcBytes = 4096f });
            loop.children.Add(scene);
            root.children.Add(loop);

            var r = new ProfilerCapture.Report();
            ProfilerCapture.Build(r, new ProfilerCapture.Accumulator(), null, root, null, Opts("root=UpdateScene"));

            Assert.AreEqual("UpdateScene", r.typicalFrameTree[0].name);
            Assert.AreEqual("Grow()", r.typicalFrameTree[1].name, "sorted by total");
            Assert.AreEqual(4f, r.typicalFrameTree[1].gcKB, 1e-5f);
            Assert.AreEqual(1, r.typicalFrameTree[1].depth);
            Assert.AreEqual(0, r.spikeFrameTree.Count);
        }

        #endregion

        #region Frames and threads

        [Test]
        public void PickTypicalAndSpike_MedianNearestAndSlowest()
        {
            ProfilerCapture.PickTypicalAndSpike(new List<float> { 10f, 30f, 12f, 11f, 50f }, out int typical, out int spike);
            Assert.AreEqual(2, typical, "the median of 10,11,12,30,50 is 12");
            Assert.AreEqual(4, spike);

            ProfilerCapture.PickTypicalAndSpike(new List<float>(), out typical, out spike);
            Assert.AreEqual(-1, typical);
            Assert.AreEqual(-1, spike);
        }

        /// <summary>
        /// NEGATIVE CONTROL for spike selection. Frame 2 is the slowest WHOLE frame only because
        /// the Editor repainted; frame 3 is the one where the game itself was slow. Picking by the
        /// whole frame (asserted, so the fixture proves it can mislead) would blame the Editor.
        /// </summary>
        [Test]
        public void SelectionSeries_PicksBySpikesInTheGame_NotInTheEditor()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 16f, new N(P("PlayerLoop"), 12f, 12f), new N(P("EditorLoop"), 4f, 4f));
            AddFrame(acc, 2, 104f, new N(P("PlayerLoop"), 11f, 11f), new N(P("EditorLoop"), 86f, 86f));
            AddFrame(acc, 3, 40f, new N(P("PlayerLoop"), 35f, 35f), new N(P("EditorLoop"), 5f, 5f));
            AddFrame(acc, 4, 17f, new N(P("PlayerLoop"), 13f, 13f), new N(P("EditorLoop"), 4f, 4f));

            ProfilerCapture.PickTypicalAndSpike(acc.FrameMs, out _, out int naiveSpike);
            Assert.AreEqual(1, naiveSpike, "the negative control must fire: by whole frame, the Editor repaint wins");

            Assert.IsTrue(acc.HasPlayerLoop);
            ProfilerCapture.PickTypicalAndSpike(acc.SelectionSeries, out int typical, out int spike);
            Assert.AreEqual(2, spike, "the game's own slowest frame");
            Assert.AreEqual(3, typical, "median of 11,12,13,35 is 13");

            var r = BuildFrom(acc, Opts());
            Assert.AreEqual("PlayerLoop", r.framesPickedBy);
            Assert.AreEqual(35f, r.playerLoopMs.maxMs, 1e-5f);
            Assert.AreEqual(104f, r.mainThreadFrameMs.maxMs, 1e-5f);
        }

        [Test]
        public void SelectionSeries_FallsBackToTheWholeFrame_WithoutAPlayerLoop()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 10f, new N(P("Work"), 10f, 10f));
            AddFrame(acc, 2, 30f, new N(P("Work"), 30f, 30f));
            Assert.IsFalse(acc.HasPlayerLoop);
            Assert.AreSame(acc.FrameMs, acc.SelectionSeries);
            Assert.AreEqual("frame", BuildFrom(acc, Opts()).framesPickedBy);
        }

        [Test]
        public void Percentile_IsNearestRank()
        {
            var v = new List<float> { 5f, 1f, 4f, 2f, 3f };
            Assert.AreEqual(3f, ProfilerCapture.Percentile(v, 0.5f));
            Assert.AreEqual(5f, ProfilerCapture.Percentile(v, 0.99f));
            Assert.AreEqual(1f, ProfilerCapture.Percentile(v, 0f));
            Assert.AreEqual(0f, ProfilerCapture.Percentile(new List<float>(), 0.5f));
        }

        [Test]
        public void Build_FrameStats()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 7, 10f, new N(P("PlayerLoop"), 10f, 10f));
            AddFrame(acc, 8, 20f, new N(P("PlayerLoop"), 20f, 20f));
            AddFrame(acc, 9, 30f, new N(P("PlayerLoop"), 30f, 30f));
            var r = BuildFrom(acc, Opts());

            Assert.AreEqual(3, r.framesRead);
            Assert.AreEqual(20f, r.mainThreadFrameMs.avgMs, 1e-5f);
            Assert.AreEqual(20f, r.mainThreadFrameMs.p50Ms, 1e-5f);
            Assert.AreEqual(30f, r.mainThreadFrameMs.maxMs, 1e-5f);
            Assert.AreEqual(10f, r.mainThreadFrameMs.minMs, 1e-5f);
            Assert.AreEqual(7, r.firstFrame);
            Assert.AreEqual(9, r.lastFrame);
        }

        [TestCase("Idle", true)]
        [TestCase("idle", true)]
        [TestCase("Semaphore.WaitForSignal", true)]
        [TestCase("Gfx.WaitForPresentOnGfxThread", true)]
        [TestCase("RenderLoop", false)]
        [TestCase("ExecuteRenderQueueJob", false)]
        [TestCase("GfxTask_ReadValue", true)]
        [TestCase("GfxTask_Execute", false)]
        public void IsWait_ClassifiesTopLevelThreadSamples(string name, bool wait)
        {
            Assert.AreEqual(wait, ProfilerCapture.IsWait(name));
        }

        [Test]
        public void Threads_AverageBusyAndWaitPerSampledFrame_BusiestFirst()
        {
            var th = new ProfilerCapture.ThreadAccumulator();
            for (int f = 0; f < 2; f++)
            {
                th.BeginFrame();
                th.AddThread("Worker 0", "Job", new List<ProfilerCapture.ThreadItemSample>
                {
                    new ProfilerCapture.ThreadItemSample("Idle", 3f),
                    new ProfilerCapture.ThreadItemSample("CullJob", 1f),
                });
                th.AddThread("Render Thread", "", new List<ProfilerCapture.ThreadItemSample>
                {
                    new ProfilerCapture.ThreadItemSample("Semaphore.WaitForSignal", 5f),
                    new ProfilerCapture.ThreadItemSample("RenderLoop", 2f),
                });
            }

            var r = BuildFrom(new ProfilerCapture.Accumulator(), Opts(), th);
            Assert.AreEqual(2, r.threadSampledFrames);
            Assert.AreEqual("Render Thread", r.threads[0].name, "2 ms busy beats 1 ms");
            Assert.AreEqual(2f, r.threads[0].avgBusyMs, 1e-5f);
            Assert.AreEqual(5f, r.threads[0].avgWaitMs, 1e-5f);
            Assert.AreEqual(100f * 2f / 7f, r.threads[0].busyPct, 1e-3f);
            Assert.AreEqual("Worker 0", r.threads[1].name);
            Assert.AreEqual(25f, r.threads[1].busyPct, 1e-3f);
            Assert.AreEqual("Idle", r.threads[1].top[0].name);
        }

        #endregion

        #region Output

        [Test]
        public void OwnerOf_IsTheParentName()
        {
            Assert.AreEqual("Foo.Update()", ProfilerCapture.OwnerOf(P("PlayerLoop", "UpdateScene", "Foo.Update()", "GC.Alloc")));
            Assert.AreEqual("PlayerLoop", ProfilerCapture.OwnerOf(P("PlayerLoop", "UpdateScene")));
            Assert.AreEqual("", ProfilerCapture.OwnerOf("PlayerLoop"));
        }

        [Test]
        public void Summarize_MarksAnIncompleteOrDeepProfiledCapture()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 13f, TypicalNodes());
            var r = new ProfilerCapture.Report { completed = false, deepProfiling = true };
            ProfilerCapture.Build(r, acc, null, null, null, Opts());

            string line = ProfilerCapture.Summarize(r, "x.json");
            StringAssert.Contains("[INCOMPLETE]", line);
            StringAssert.Contains("DEEP PROFILE", line);
            StringAssert.Contains("saved x.json", line);
            StringAssert.Contains("Deep Profile was ON", string.Join(" ", r.notes));
        }

        [Test]
        public void BuildText_CarriesEverySection()
        {
            var acc = new ProfilerCapture.Accumulator();
            AddFrame(acc, 1, 13f, TypicalNodes());
            var r = BuildFrom(acc, Opts("S1"));
            r.scene = "Menu_Main";

            string text = ProfilerCapture.BuildText(r);
            foreach (string section in new[] { "TOP SELF TIME", "TOP SELF ALLOCATION", "THREADS", "MERGED TREE", "TYPICAL FRAME", "SPIKE FRAME" })
                StringAssert.Contains(section, text);
            StringAssert.Contains("Foo.Update()  <-", text);
        }

        #endregion
    }
}
