using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Blueprint + base head → <see cref="CharacterModel"/>: evaluates the head at the resolved
    /// shape, resolves every feature's site (left and mirrored right for bilateral sites),
    /// generates the feature in site space, asserts the attachment contract, and places it in
    /// head space — seam parts scaled by the site radius with their base ring SNAPPED onto the
    /// sampled surface ring, embedded parts placed rigidly in head units. Skin-slot parts get
    /// their UVs re-projected through the head's own mapping so they are colour-continuous.
    /// This is the file for "the feature is in the wrong place / the wrong size / floating".
    /// </summary>
    public static class CharacterAssembler
    {
        public static CharacterModel Assemble(CharacterBlueprint bp, IBaseHead head)
        {
            if (bp == null) throw new ArgumentNullException(nameof(bp));
            if (head == null) throw new ArgumentNullException(nameof(head));

            var model = new CharacterModel { Blueprint = bp, BaseHead = head, Shape = bp.Shape };
            var topo = head.Topology;
            var verts = head.Evaluate(bp.Shape);
            if (verts.Length != topo.VertexCount)
                throw new InvalidOperationException($"Base head evaluated {verts.Length} vertices against a topology of {topo.VertexCount}.");

            var headPart = new MeshPart("Head", CharacterMaterialSlot.Skin);
            for (int i = 0; i < verts.Length; i++) headPart.AddVertex(verts[i], topo.Uvs[i]);
            headPart.Tris.AddRange(topo.Triangles);
            GeometryKit.RecalculateNormals(headPart);
            if (GeometryKit.AnyNaN(headPart)) throw new InvalidOperationException("Base head produced a NaN vertex.");
            model.Head = headPart;

            var surface = head.Surface(bp.Shape);
            model.Landmarks = BuildLandmarks(head, surface, bp.Shape);

            foreach (var feature in bp.Features)
            {
                if (!HeadSiteResolver.TryFindSpec(head, feature.Site, out var spec))
                    throw new InvalidOperationException(
                        $"Trait '{feature.SourceKey}.{feature.TraitId}' wants site '{feature.Site}', which this head does not declare.");

                int instances = spec.Bilateral ? 2 : 1;
                for (int inst = 0; inst < instances; inst++)
                {
                    bool mirrored = inst == 1;
                    var site = HeadSiteResolver.Resolve(head, surface, bp.Shape, spec, mirrored,
                        feature.SitePitchDeg, feature.SiteYawDeg);
                    if (feature.SiteRollDeg != 0f)
                    {
                        float roll = feature.SiteRollDeg * Mathf.Deg2Rad * (mirrored ? -1f : 1f);
                        site.Right = GeometryKit.Rotate(site.Right, site.Normal, roll);
                        site.Up = GeometryKit.Rotate(site.Up, site.Normal, roll);
                    }

                    var ctx = new FeatureContext
                    {
                        Site = site, Surface = surface, Params = feature.Params, Shape = bp.Shape,
                        Rng = new CharacterRandom(bp.Genome.Seed ^ StableHash(feature.TraitId) ^ (mirrored ? 0x5A5A : 0)),
                        HairVolume = bp.Genome.HairVolume, Age = bp.Genome.Age,
                        TraitId = $"{feature.SourceKey}.{feature.TraitId}{(spec.Bilateral ? (mirrored ? ".R" : ".L") : string.Empty)}",
                    };
                    FeatureCatalog.Generate(feature.Kind, ctx);

                    foreach (var part in ctx.Output)
                    {
                        AttachmentContract.AssertSeamFeature(part, site);
                        Place(part, site, surface);
                        if (GeometryKit.AnyNaN(part))
                            throw new InvalidOperationException($"Feature '{part.Name}' produced a NaN vertex.");
                        model.Parts.Add(part);
                    }
                }
            }
            return model;
        }

        static void Place(MeshPart part, AttachmentSite site, IHeadSurface surface)
        {
            bool seam = part.SeamRingCount > 0;
            float scale = seam ? site.Radius : 1f;
            if (!part.IsHeadSpace)
                for (int i = 0; i < part.Verts.Count; i++)
                {
                    if (seam && i < part.SeamRingCount) part.Verts[i] = site.Ring[i];
                    else part.Verts[i] = site.ToHead(part.Verts[i] * scale);
                    part.Normals[i] = site.DirToHead(part.Normals[i]).normalized;
                }
            if (site.Mirrored) part.FlipWinding();
            if (part.ProjectUvsOntoHead)
                for (int i = 0; i < part.Verts.Count; i++)
                    part.Uvs[i] = surface.Uv(part.Verts[i] - surface.Centre);
            // Seam snapping bends the first ring; recompute so the shading agrees with the geometry.
            if (seam || site.Mirrored) GeometryKit.RecalculateNormals(part);
        }

        static FaceLandmarks BuildLandmarks(IBaseHead head, IHeadSurface surface, HeadShape shape)
        {
            var lm = new FaceLandmarks { Surface = surface };
            foreach (var spec in head.Sites)
            {
                int instances = spec.Bilateral ? 2 : 1;
                for (int inst = 0; inst < instances; inst++)
                {
                    bool mirrored = inst == 1;
                    Vector3 dir = head.SiteDirection(spec, shape);
                    if (mirrored) dir.x = -dir.x;
                    Vector2 uv = surface.Uv(dir);
                    float rho = spec.RingDeg * Mathf.Deg2Rad;
                    GeometryKit.Frame(dir, Vector3.up, out var e1, out var e2);
                    Vector2 uvR = surface.Uv(GeometryKit.Rotate(dir, e2, rho));
                    Vector2 uvU = surface.Uv(GeometryKit.Rotate(dir, e1, -rho));
                    var l = new Landmark
                    {
                        Name = spec.Name + (spec.Bilateral ? (mirrored ? ".R" : ".L") : string.Empty),
                        Uv = uv, Dir = dir, Mirrored = mirrored,
                        UvRadius = new Vector2(Mathf.Abs(WrapU(uvR.x - uv.x)), Mathf.Abs(uvU.y - uv.y)),
                    };
                    lm.Add(l);
                }
            }
            return lm;
        }

        static float WrapU(float du) => du > 0.5f ? du - 1f : (du < -0.5f ? du + 1f : du);

        public static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                if (s != null) foreach (char c in s) h = h * 31 + c;
                return h;
            }
        }
    }
}
