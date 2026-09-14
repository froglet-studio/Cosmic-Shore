using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Paints every texture a character wears. The SKIN canvas is one pass over its pixels:
    /// human skin from <see cref="SkinBaseLayer"/>, then each clade covering composited by its
    /// coverage (heaviest clade last, so it wins where two reach), detail from
    /// <see cref="SurfaceDetailLayer"/> into colour and height, then the landmark-keyed features
    /// from <see cref="FaceDetailLayer"/>. Alpha carries smoothness. The eye, keratin and hair
    /// canvases are their own layers. Pure; runs offline.
    /// </summary>
    public static class CharacterTexturePainter
    {
        public static CharacterTextures Paint(CharacterModel model, CharacterGenerationConfigSO config, SO_ColorSet colorSet, int skinSizeOverride = 0)
        {
            var bp = model.Blueprint;
            var g = bp.Genome;
            var ctx = new PaintContext
            {
                Blueprint = bp, Landmarks = model.Landmarks, Config = config,
                Accent = CharacterPaletteBinding.Resolve(g.Domain, colorSet),
                Seed = g.Seed,
                HumanSkin = SkinBaseLayer.HumanBase(config, g),
                HairColor = HairTextureLayer.HairColor(config, g),
            };
            ctx.BrowColor = Color.Lerp(ctx.HairColor, Color.black, 0.35f);
            if (ctx.Accent.SkinTint > 0f)
                ctx.HumanSkin = Color.Lerp(ctx.HumanSkin, ctx.Accent.Accent, ctx.Accent.SkinTint);

            int size = skinSizeOverride > 0 ? skinSizeOverride : Mathf.Max(64, config.SkinTextureSize);
            var textures = new CharacterTextures();
            var skin = new TextureCanvas(size, size);
            var height = new TextureCanvas(size, size, 0.5f, 0.5f, 0.5f, 1f);
            PaintSkin(ctx, model, skin, height);
            textures.Skin = skin;
            textures.SkinNormal = TextureCanvas.NormalFromHeight(height, config.DetailNormalStrength);

            int eyeSize = Mathf.Max(32, config.EyeTextureSize);
            textures.Eye = new TextureCanvas(eyeSize, eyeSize);
            IrisTextureLayer.Paint(textures.Eye, bp.Eye ?? new EyeParams(), g.IrisKey, ctx.Accent, g.Seed);

            int small = Mathf.Max(32, config.SmallTextureSize);
            textures.Keratin = new TextureCanvas(small, small);
            float gape = 0.42f;
            foreach (var f in bp.Features) if (f.Kind == FeatureKind.Beak) { gape = f.Params.Beak.GapeHeight; break; }
            KeratinTextureLayer.Paint(textures.Keratin, bp.KeratinA, bp.KeratinB, bp.HasBeak, gape, g.Seed);
            textures.KeratinSmoothness = bp.HasBeak || bp.HasMandibles ? 0.62f : 0.5f;

            textures.Hair = new TextureCanvas(small, small);
            Color hair = ctx.HairColor;
            // A corvid crest is feather-black with a blue sheen; any other hair-slot part is hair.
            foreach (var f in bp.Features)
                if (f.Kind == FeatureKind.Crest) { hair = new Color(0.05f, 0.05f, 0.07f); break; }
            HairTextureLayer.Paint(textures.Hair, hair, hair + new Color(0.06f, 0.08f, 0.16f), g.Seed);
            return textures;
        }

        static void PaintSkin(PaintContext ctx, CharacterModel model, TextureCanvas skin, TextureCanvas height)
        {
            var bp = ctx.Blueprint;
            var surface = model.Landmarks.Surface;
            // Clade layers heaviest LAST so the heavier covering wins where both reach.
            var layers = new List<CoveringLayer>();
            foreach (var l in bp.Coverings) if (!l.IsHuman) layers.Add(l);
            layers.Sort((a, b) => a.Weight.CompareTo(b.Weight));
            var humanLayer = bp.Coverings.Find(l => l.IsHuman);
            var humanRecipe = humanLayer?.Recipe ?? new CoveringRecipe();

            int w = skin.Width, h = skin.Height;
            var px = new PaintContext.Pixel();
            for (int y = 0; y < h; y++)
            {
                px.V = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    px.U = (x + 0.5f) / w;
                    Vector3 dir = surface.Direction(new Vector2(px.U, px.V));
                    GeometryKit.ToAngles(dir, out float theta, out float phi);
                    px.ThetaDeg = theta * Mathf.Rad2Deg;
                    px.Phi = phi;

                    Color color = SkinBaseLayer.Evaluate(ctx, px);
                    float lum = SurfaceDetailLayer.Evaluate(CoveringKind.Skin, humanRecipe.DetailStrength, bp.Genome.Age, px, ctx.Seed, out float hgt);
                    float smooth = humanRecipe.Smoothness;
                    float humanMask = 1f;
                    bool browsAllowed = humanRecipe.PaintsBrows;

                    for (int i = 0; i < layers.Count; i++)
                    {
                        var layer = layers[i];
                        float cov = MarkingsLayer.Coverage(layer, px, ctx.Seed + i * 17);
                        if (cov <= 0.002f) continue;
                        Color lc = MarkingsLayer.Base(layer.Recipe, bp.Genome.MarkingKey, px, ctx.Accent, ctx.Seed + i * 17);
                        if (ctx.Accent.SkinTint > 0f) lc = Color.Lerp(lc, ctx.Accent.Accent, ctx.Accent.SkinTint);
                        float llum = SurfaceDetailLayer.Evaluate(layer.Recipe.Kind, layer.Recipe.DetailStrength, bp.Genome.Age, px, ctx.Seed + i * 17, out float lh);
                        color = Color.Lerp(color, lc, cov);
                        lum = Mathf.Lerp(lum, llum, cov);
                        hgt = Mathf.Lerp(hgt, lh, cov);
                        smooth = Mathf.Lerp(smooth, layer.Recipe.Smoothness, cov);
                        humanMask *= 1f - cov;
                        if (cov > 0.5f) browsAllowed = layer.Recipe.PaintsBrows;
                    }

                    color *= lum;
                    FaceDetailLayer.Apply(ctx, px, humanMask, browsAllowed, ref color, ref hgt);

                    color.r = Mathf.Clamp01(color.r); color.g = Mathf.Clamp01(color.g); color.b = Mathf.Clamp01(color.b);
                    color.a = Mathf.Clamp01(smooth);
                    skin.Set(x, y, color);
                    height.R[height.Index(x, y)] = Mathf.Clamp01(hgt);
                }
            }
        }
    }
}
