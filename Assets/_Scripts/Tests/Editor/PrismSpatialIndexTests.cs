#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for <see cref="PrismSpatialIndex"/>'s query views (Docs/SPATIAL_INDEX.md):
    ///
    ///   - QuerySphere gathers live prisms by stored position (the Phase 2
    ///     replacement for Physics.OverlapSphere against prisms)
    ///   - IsAnyPrismWithin boolean probe, including the wide-radius linear-scan
    ///     fallback path
    ///   - Lifecycle visibility: MarkDestroyed / MarkRestored / Unregister /
    ///     UpdatePosition (the movers contract fauna bodies and bond steering
    ///     rely on)
    ///   - Reservation claims: TryReserve / IsPositionOccupied / ReleaseReservation
    ///
    /// Edit mode doesn't run MonoBehaviour lifecycle callbacks for regular
    /// scripts, so Awake/OnDestroy are invoked explicitly to allocate and
    /// dispose the index's persistent native containers.
    /// </summary>
    [TestFixture]
    public class PrismSpatialIndexTests
    {
        GameObject _indexGo;
        PrismSpatialIndex _index;
        readonly List<GameObject> _spawned = new();
        readonly List<Prism> _results = new();

        [SetUp]
        public void SetUp()
        {
            _indexGo = new GameObject("[PrismSpatialIndex under test]");
            _index = _indexGo.AddComponent<PrismSpatialIndex>();
            _index.Awake(); // edit mode: AddComponent does not invoke Awake
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go) Object.DestroyImmediate(go);
            _spawned.Clear();

            // Dispose the persistent NativeArrays - OnDestroy doesn't run for
            // regular scripts in edit mode, and leaked Persistent allocations
            // spam the console across test runs.
            typeof(PrismSpatialIndex)
                .GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_index, null);
            Object.DestroyImmediate(_indexGo);
        }

        Prism SpawnRegisteredPrism(Vector3 position)
        {
            var go = new GameObject("test prism");
            go.transform.position = position;
            var prism = go.AddComponent<Prism>();
            prism.SpatialIndexId = _index.Register(prism);
            _spawned.Add(go);
            return prism;
        }

        /// <summary>
        /// Queries pick bucket-walk vs linear-scan by comparing the probe's bucket
        /// AABB volume against the slot high-water mark, so a near-empty test index
        /// routes everything to the linear path. Registering distant padding raises
        /// the mark enough that a tight-radius probe (radius 4 → 2³ = 8 buckets)
        /// takes the bucket walk.
        /// </summary>
        void SpawnPaddingPrisms(int count)
        {
            for (int i = 0; i < count; i++)
                SpawnRegisteredPrism(new Vector3(2000f + 30f * i, 0f, 0f));
        }

        [Test]
        public void QuerySphere_EmptyIndex_ReturnsZero()
        {
            int count = _index.QuerySphere(Vector3.zero, 50f, _results);

            Assert.AreEqual(0, count);
            Assert.IsEmpty(_results);
        }

        [Test]
        public void QuerySphere_FindsPrismWithinRadius_ExcludesPrismOutside()
        {
            var near = SpawnRegisteredPrism(new Vector3(3f, 0f, 0f));
            SpawnRegisteredPrism(new Vector3(100f, 0f, 0f));

            int count = _index.QuerySphere(Vector3.zero, 10f, _results);

            Assert.AreEqual(1, count);
            Assert.AreSame(near, _results[0]);
        }

        [Test]
        public void QuerySphere_BucketWalkPath_FindsPrismWithinRadius()
        {
            SpawnPaddingPrisms(10); // raise the slot count above the 8-bucket AABB
            var near = SpawnRegisteredPrism(new Vector3(3f, 0f, 0f));
            SpawnRegisteredPrism(new Vector3(100f, 0f, 0f));

            int count = _index.QuerySphere(Vector3.zero, 4f, _results);

            Assert.AreEqual(1, count);
            Assert.AreSame(near, _results[0]);
        }

        [Test]
        public void QuerySphere_ClearsResultsBetweenCalls()
        {
            SpawnRegisteredPrism(Vector3.zero);

            _index.QuerySphere(Vector3.zero, 10f, _results);
            int count = _index.QuerySphere(new Vector3(500f, 0f, 0f), 10f, _results);

            Assert.AreEqual(0, count);
            Assert.IsEmpty(_results);
        }

        [Test]
        public void QuerySphere_ExcludesDestroyed_IncludesAfterRestore()
        {
            var prism = SpawnRegisteredPrism(Vector3.zero);

            _index.MarkDestroyed(prism.SpatialIndexId);
            Assert.AreEqual(0, _index.QuerySphere(Vector3.zero, 10f, _results),
                "Destroyed mass must not appear in neighborhood queries.");

            _index.MarkRestored(prism.SpatialIndexId);
            Assert.AreEqual(1, _index.QuerySphere(Vector3.zero, 10f, _results),
                "Restored mass must re-enter neighborhood queries.");
        }

        [Test]
        public void QuerySphere_ExcludesUnregistered()
        {
            var prism = SpawnRegisteredPrism(Vector3.zero);

            _index.Unregister(prism.SpatialIndexId);

            Assert.AreEqual(0, _index.QuerySphere(Vector3.zero, 10f, _results));
        }

        [Test]
        public void UpdatePosition_MovesPrismBetweenBuckets()
        {
            // The movers contract: bond steering and swimming fauna bodies push
            // fresh positions in; queries must see the prism where it IS, not
            // where it registered. 50m guarantees a bucket-key change (8m buckets);
            // padding forces the bucket-walk path so rebucketing is what's tested.
            SpawnPaddingPrisms(10);
            var prism = SpawnRegisteredPrism(Vector3.zero);
            var newPosition = new Vector3(50f, 0f, 0f);
            prism.transform.position = newPosition;

            _index.UpdatePosition(prism.SpatialIndexId, newPosition);

            Assert.AreEqual(0, _index.QuerySphere(Vector3.zero, 4f, _results),
                "Old site must read empty after the move.");
            Assert.AreEqual(1, _index.QuerySphere(newPosition, 4f, _results),
                "New site must contain the moved prism.");
        }

        [Test]
        public void QuerySphere_WideRadius_LinearScanPath_FindsAll()
        {
            // Radius 500 spans far more 8m buckets than the index has slots,
            // forcing the linear hot-array fallback. Results must match the
            // bucket-walk path's.
            var a = SpawnRegisteredPrism(new Vector3(10f, 0f, 0f));
            var b = SpawnRegisteredPrism(new Vector3(0f, 200f, 0f));
            SpawnRegisteredPrism(new Vector3(0f, 0f, 5000f)); // outside even the wide probe

            int count = _index.QuerySphere(Vector3.zero, 500f, _results);

            Assert.AreEqual(2, count);
            CollectionAssert.AreEquivalent(new[] { a, b }, _results);
        }

        [Test]
        public void IsAnyPrismWithin_RespectsRadius_OnBothStrategies()
        {
            SpawnPaddingPrisms(10); // tight radii now take the bucket walk
            SpawnRegisteredPrism(new Vector3(3f, 0f, 0f));

            // Bucket-walk path (radius 4 → 8-bucket AABB ≤ slot count).
            Assert.IsTrue(_index.IsAnyPrismWithin(Vector3.zero, 4f));
            Assert.IsFalse(_index.IsAnyPrismWithin(new Vector3(0f, 50f, 0f), 4f));

            // Linear-scan path (radius 500 → AABB far exceeds slot count).
            Assert.IsTrue(_index.IsAnyPrismWithin(Vector3.zero, 500f));
            Assert.IsFalse(_index.IsAnyPrismWithin(new Vector3(0f, 50000f, 0f), 500f));
        }

        [Test]
        public void QuerySphere_SkipsDestroyedManagedRefs()
        {
            // A prism destroyed without Unregister (no lifecycle callbacks in
            // edit mode) leaves a fake-null managed ref - queries must skip it
            // rather than hand callers a dead object.
            var prism = SpawnRegisteredPrism(Vector3.zero);
            Object.DestroyImmediate(prism.gameObject);

            int count = _index.QuerySphere(Vector3.zero, 10f, _results);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void TryReserve_ClaimsSite_SecondClaimFails_ReleaseFrees()
        {
            var site = new Vector3(4f, 4f, 4f);

            Assert.IsTrue(_index.TryReserve(site, 3f), "First claim on a free site must succeed.");
            Assert.IsFalse(_index.TryReserve(site, 3f), "Second claim on the same site must fail.");
            Assert.IsTrue(_index.IsPositionOccupied(site, 3f));

            _index.ReleaseReservation(site);

            Assert.IsFalse(_index.IsPositionOccupied(site, 3f));
            Assert.IsTrue(_index.TryReserve(site, 3f), "Released site must be claimable again.");
        }

        [Test]
        public void TryReserve_FailsOnSiteOccupiedByLivePrism()
        {
            SpawnRegisteredPrism(Vector3.zero);

            Assert.IsFalse(_index.TryReserve(new Vector3(1f, 0f, 0f), 3f),
                "A live prism within clearRadius must block the claim.");
            Assert.IsTrue(_index.TryReserve(new Vector3(50f, 0f, 0f), 3f),
                "A site beyond clearRadius must remain claimable.");
        }

        // ---- Cell density view (Phase 3) ------------------------------------
        // Binding against a REAL cell needs Cell's runtime grid builder + config
        // and is play-mode territory; what edit mode can lock down is that the
        // cell-view hooks riding every lifecycle call are robust with no cells
        // present (Cell.ActiveCells only populates via OnEnable at runtime, so
        // every Register/MarkDestroyed/MarkRestored/Unregister above already
        // exercises BindCell/UnbindCell with a null binding) and that the
        // domain-change forwarder rejects bad indices.

        [Test]
        public void ForwardDomainChangeToCell_InvalidOrUnboundIndex_DoesNotThrow()
        {
            var prism = SpawnRegisteredPrism(Vector3.zero);

            Assert.DoesNotThrow(() => _index.ForwardDomainChangeToCell(-1));
            Assert.DoesNotThrow(() => _index.ForwardDomainChangeToCell(9999));
            // Valid slot, but no cell bound (open space) - must be a clean no-op.
            Assert.DoesNotThrow(() => _index.ForwardDomainChangeToCell(prism.SpatialIndexId));
        }

        [Test]
        public void FullLifecycle_WithNoCellsPresent_KeepsQueryViewsConsistent()
        {
            // Register → destroy → restore → unregister, end to end, with the
            // cell-view hooks firing at every step against an empty cell registry.
            var prism = SpawnRegisteredPrism(Vector3.zero);

            _index.MarkDestroyed(prism.SpatialIndexId);
            Assert.IsFalse(_index.IsAnyPrismWithin(Vector3.zero, 10f));

            _index.MarkRestored(prism.SpatialIndexId);
            Assert.IsTrue(_index.IsAnyPrismWithin(Vector3.zero, 10f));

            _index.Unregister(prism.SpatialIndexId);
            Assert.IsFalse(_index.IsAnyPrismWithin(Vector3.zero, 10f));
            Assert.AreEqual(0, _index.QuerySphere(Vector3.zero, 10f, _results));
        }

        // ---- Cell-volume summation view (CellVolumeSumJob) -------------------
        // Binding is normally written only by Cell.AddBlock/RemoveBlock; edit mode
        // has no live cells, so these tests drive SetCellBinding/UpdateCellVolume
        // directly to lock down the job's filter + accumulation semantics.

        float[] NewResults() => new float[PrismSpatialIndex.CellVolumeResultCount];

        Prism SpawnBoundPrism(Vector3 position, short cellId, bool envMass, Domains domain, float volume)
        {
            var prism = SpawnRegisteredPrism(position);
            _index.SetCellBinding(prism.SpatialIndexId, cellId, envMass, domain);
            _index.UpdateCellVolume(prism.SpatialIndexId, volume);
            return prism;
        }

        [Test]
        public void SumCellVolumes_SumsOnlyTheBoundCell_ByDomainSlot()
        {
            SpawnBoundPrism(Vector3.zero, 7, true, Domains.Jade, 10f);
            SpawnBoundPrism(new Vector3(5f, 0f, 0f), 7, true, Domains.Ruby, 20f);
            SpawnBoundPrism(new Vector3(10f, 0f, 0f), 8, true, Domains.Jade, 40f); // other cell
            SpawnRegisteredPrism(new Vector3(15f, 0f, 0f));                        // unbound

            var results = NewResults();
            Assert.IsTrue(_index.SumCellVolumes(7, Vector3.zero, 0f, results));

            Assert.AreEqual(10f, results[PrismSpatialIndex.CellVolumeBySlot + 0], 1e-4f); // Jade
            Assert.AreEqual(20f, results[PrismSpatialIndex.CellVolumeBySlot + 1], 1e-4f); // Ruby
            Assert.AreEqual(30f, results[PrismSpatialIndex.CellVolumeTotal], 1e-4f);
            Assert.AreEqual(30f, results[PrismSpatialIndex.CellEnvVolumeTotal], 1e-4f);
            // No nucleus zone → the whole cell is the feeding ground.
            Assert.AreEqual(30f, results[PrismSpatialIndex.CellExteriorEnvVolumeTotal], 1e-4f);

            Assert.IsTrue(_index.SumCellVolumes(8, Vector3.zero, 0f, results));
            Assert.AreEqual(40f, results[PrismSpatialIndex.CellVolumeTotal], 1e-4f);
        }

        [Test]
        public void SumCellVolumes_FaunaBody_IsVolumeOnly()
        {
            SpawnBoundPrism(Vector3.zero, 7, false, Domains.Gold, 12f); // fauna body

            var results = NewResults();
            _index.SumCellVolumes(7, Vector3.zero, 0f, results);

            Assert.AreEqual(12f, results[PrismSpatialIndex.CellVolumeBySlot + 2], 1e-4f);
            Assert.AreEqual(12f, results[PrismSpatialIndex.CellVolumeTotal], 1e-4f);
            Assert.AreEqual(0f, results[PrismSpatialIndex.CellEnvVolumeTotal], 1e-4f,
                "Fauna bodies feed volume but must not read as environment mass.");
            Assert.AreEqual(0f, results[PrismSpatialIndex.CellExteriorEnvVolumeTotal], 1e-4f);
        }

        [Test]
        public void SumCellVolumes_SplitsEnvironmentMassAcrossTheNucleusBoundary()
        {
            SpawnBoundPrism(new Vector3(2f, 0f, 0f), 7, true, Domains.Jade, 10f);   // inside r=5
            SpawnBoundPrism(new Vector3(50f, 0f, 0f), 7, true, Domains.Ruby, 20f);  // outside

            var results = NewResults();
            _index.SumCellVolumes(7, Vector3.zero, 25f, results); // radiusSqr = 5²

            Assert.AreEqual(10f, results[PrismSpatialIndex.CellNucleusEnvVolumeBySlot + 0], 1e-4f,
                "Interior environment mass is the territorial claim.");
            Assert.AreEqual(0f, results[PrismSpatialIndex.CellNucleusEnvVolumeBySlot + 1], 1e-4f);
            Assert.AreEqual(20f, results[PrismSpatialIndex.CellExteriorEnvVolumeTotal], 1e-4f,
                "Exterior environment mass is the contested feeding ground.");
            Assert.AreEqual(30f, results[PrismSpatialIndex.CellEnvVolumeTotal], 1e-4f);
        }

        [Test]
        public void SumCellVolumes_ExcludesDestroyed_AndFreedSlots()
        {
            var prism = SpawnBoundPrism(Vector3.zero, 7, true, Domains.Jade, 10f);
            var results = NewResults();

            _index.MarkDestroyed(prism.SpatialIndexId);
            _index.SumCellVolumes(7, Vector3.zero, 0f, results);
            Assert.AreEqual(0f, results[PrismSpatialIndex.CellVolumeTotal], 1e-4f,
                "Destroyed mass weighs nothing.");

            _index.MarkRestored(prism.SpatialIndexId);
            _index.SumCellVolumes(7, Vector3.zero, 0f, results);
            Assert.AreEqual(10f, results[PrismSpatialIndex.CellVolumeTotal], 1e-4f,
                "Restored mass weighs again (binding was set manually — no cells in edit mode).");

            _index.Unregister(prism.SpatialIndexId);
            _index.SumCellVolumes(7, Vector3.zero, 0f, results);
            Assert.AreEqual(0f, results[PrismSpatialIndex.CellVolumeTotal], 1e-4f,
                "A freed slot must not keep contributing through the free-list window.");
        }

        [Test]
        public void ClearCellBinding_OnlyReleasesTheOwningCell()
        {
            var prism = SpawnBoundPrism(Vector3.zero, 7, true, Domains.Jade, 10f);
            var results = NewResults();

            _index.ClearCellBinding(prism.SpatialIndexId, 8); // not the owner — no-op
            _index.SumCellVolumes(7, Vector3.zero, 0f, results);
            Assert.AreEqual(10f, results[PrismSpatialIndex.CellVolumeTotal], 1e-4f);

            _index.ClearCellBinding(prism.SpatialIndexId, 7);
            _index.SumCellVolumes(7, Vector3.zero, 0f, results);
            Assert.AreEqual(0f, results[PrismSpatialIndex.CellVolumeTotal], 1e-4f);
        }

        [Test]
        public void TryScheduleCellVolumeSum_MatchesSyncResults()
        {
            // The async path (snapshot + worker-thread job) must produce exactly
            // the sums the sync .Run() path does — same job, point-in-time copy.
            SpawnBoundPrism(new Vector3(2f, 0f, 0f), 7, true, Domains.Jade, 10f);   // inside r=5 nucleus
            SpawnBoundPrism(new Vector3(50f, 0f, 0f), 7, true, Domains.Ruby, 20f);  // outside
            SpawnBoundPrism(new Vector3(60f, 0f, 0f), 7, false, Domains.Gold, 30f); // fauna body

            var sync = NewResults();
            Assert.IsTrue(_index.SumCellVolumes(7, Vector3.zero, 25f, sync));

            var native = new NativeArray<float>(PrismSpatialIndex.CellVolumeResultCount, Allocator.Persistent);
            try
            {
                Assert.IsTrue(_index.TryScheduleCellVolumeSum(7, Vector3.zero, 25f, native, out var handle));
                handle.Complete();
                for (int i = 0; i < PrismSpatialIndex.CellVolumeResultCount; i++)
                    Assert.AreEqual(sync[i], native[i], 1e-4f, $"result slot {i} diverged between sync and async paths");
            }
            finally
            {
                native.Dispose();
            }
        }

        [Test]
        public void ClearAllCellBindings_ReleasesEveryBindingOfThatCellOnly()
        {
            SpawnBoundPrism(Vector3.zero, 7, true, Domains.Jade, 10f);
            SpawnBoundPrism(new Vector3(5f, 0f, 0f), 7, false, Domains.Ruby, 20f);
            SpawnBoundPrism(new Vector3(10f, 0f, 0f), 8, true, Domains.Gold, 40f);

            _index.ClearAllCellBindings(7);

            var results = NewResults();
            _index.SumCellVolumes(7, Vector3.zero, 0f, results);
            Assert.AreEqual(0f, results[PrismSpatialIndex.CellVolumeTotal], 1e-4f);
            _index.SumCellVolumes(8, Vector3.zero, 0f, results);
            Assert.AreEqual(40f, results[PrismSpatialIndex.CellVolumeTotal], 1e-4f,
                "Another cell's bindings must survive the bulk clear.");
        }
    }
}
#endif
