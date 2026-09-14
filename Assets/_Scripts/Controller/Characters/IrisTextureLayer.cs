using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The eye texture: iris fibres, pupil (round / slit / bar / all-dark), limbal ring, sclera
    /// with faint veins — or the hexagonal facet field of a compound eye. The eyeball's UVs are
    /// polar about its FRONT pole (v = 1), so the iris is the region near v = 1.
    /// This is the file for "the eyes look dead" when the cause is the IRIS rather than the lids.
    /// </summary>
    public static class IrisTextureLayer
    {
        public static void Paint(TextureCanvas c, EyeParams p, float irisKey, in CharacterPaletteBinding.DomainAccent accent, int seed)
        {
            bool compound = p.Pupil == PupilKind.Compound;
            Color irisBase = Color.Lerp(p.IrisA, p.IrisB, Mathf.Clamp01(irisKey));
            // The domain accent lives in the OUTER ring of the iris (a natural iris keeps its colour
            // at the pupil and shifts toward the limbus), so the eye reads as an eye first.
            Color irisRing = Color.Lerp(irisBase, accent.Accent * 0.6f + irisBase * 0.4f, accent.IrisTint);
            Color pupil = new Color(0.02f, 0.015f, 0.015f);
            float irisR = Mathf.Clamp(p.IrisFraction, 0.2f, 1f);
            float pupilR = irisR * Mathf.Clamp(p.PupilSize, 0.05f, 0.9f);
            for (int y = 0; y < c.Height; y++)
            {
                float v = y / (float)(c.Height - 1);
                float alpha = (1f - v) * Mathf.PI;               // polar angle from the front pole
                float rad = Mathf.Sin(Mathf.Min(alpha, Mathf.PI * 0.5f)); // front-view radius (0 at pole, 1 at the equator)
                bool back = alpha > Mathf.PI * 0.5f;
                for (int x = 0; x < c.Width; x++)
                {
                    float u = x / (float)c.Width;
                    float ang = u * GeometryKit.Tau;
                    float px = rad * Mathf.Cos(ang), py = rad * Mathf.Sin(ang);
                    Color col;
                    if (compound)
                    {
                        // Facets: a jittered hex-ish lattice, each cell shaded darker at its rim.
                        float d = Noise.Worley(px * 22f + 50f, py * 22f + 50f, seed + 3, out float id);
                        float rim = GeometryKit.SmoothStep(0.30f, 0.50f, d);
                        Color facet = Color.Lerp(p.CompoundFacet, p.CompoundFacet * 1.6f + new Color(0.03f, 0.03f, 0.02f), id);
                        facet = Color.Lerp(facet, accent.Accent * 0.35f + facet * 0.65f, accent.IrisTint * 0.5f * id);
                        col = Color.Lerp(facet, p.CompoundFacet * 0.5f, rim);
                        if (back || rad > 0.97f) col = Color.Lerp(col, p.CompoundRim, GeometryKit.SmoothStep(0.93f, 1.0f, rad));
                    }
                    else if (back || rad > irisR)
                    {
                        // Sclera: not pure white — warm, with a faint vein field and a darker back.
                        float veins = GeometryKit.SmoothStep(0.62f, 0.75f, Noise.Fbm(px * 9f + 7f, py * 9f, 3, 2f, 0.6f, seed + 5)) * 0.35f;
                        col = p.Sclera;
                        col.r -= veins * 0.05f; col.g -= veins * 0.22f; col.b -= veins * 0.22f;
                        float shade = back ? 0.55f : GeometryKit.SmoothStep(irisR + 0.02f, 1f, rad) * 0.25f;
                        col *= 1f - shade;
                        // Limbal ring: a soft dark edge just outside the iris.
                        float limbal = GeometryKit.Bell((rad - irisR - 0.015f) / 0.045f);
                        col = Color.Lerp(col, irisBase * 0.35f, limbal * 0.8f);
                    }
                    else
                    {
                        float t = rad / irisR;                      // 0 pupil edge → 1 iris edge
                        bool inPupil;
                        switch (p.Pupil)
                        {
                            case PupilKind.VerticalSlit:
                                inPupil = Mathf.Abs(px) < pupilR * 0.28f * (1f - 0.55f * (py * py) / (irisR * irisR)) && Mathf.Abs(py) < irisR * 0.94f;
                                break;
                            case PupilKind.HorizontalBar:
                                inPupil = Mathf.Abs(py) < pupilR * 0.42f && Mathf.Abs(px) < irisR * 0.85f;
                                break;
                            case PupilKind.Dark:
                                inPupil = rad < irisR * 0.995f;
                                break;
                            default:
                                inPupil = rad < pupilR;
                                break;
                        }
                        if (inPupil)
                        {
                            col = pupil;
                            if (p.Pupil == PupilKind.Dark)
                            {
                                // An all-dark eye still needs a hint of an iris to read as an eye, not a bead.
                                float ring = GeometryKit.Bell((rad - irisR * 0.62f) / (irisR * 0.3f));
                                col = Color.Lerp(col, irisBase * 0.5f, ring * 0.35f);
                            }
                        }
                        else
                        {
                            float fibres = Noise.Fbm(ang * 6f, t * 3f, 3, 2f, 0.5f, seed + 8);
                            float crypts = Noise.Value(ang * 14f, t * 6f + 3f, seed + 9);
                            Color inner = irisBase * 1.25f + new Color(0.06f, 0.05f, 0.02f);
                            Color outer = irisRing * 0.7f;
                            col = Color.Lerp(inner, outer, GeometryKit.SafePow(t, 1.4f));
                            col *= 0.82f + 0.36f * fibres;
                            col = Color.Lerp(col, col * 0.7f, GeometryKit.SmoothStep(0.7f, 0.9f, crypts) * 0.5f);
                            // Pupil edge softens, outer edge darkens.
                            float edge = GeometryKit.SmoothStep(0.85f, 1f, t);
                            col = Color.Lerp(col, irisRing * 0.3f, edge * 0.7f);
                            float pupilEdge = 1f - GeometryKit.SmoothStep(0f, 0.12f, t);
                            col = Color.Lerp(col, pupil, pupilEdge * 0.5f);
                        }
                    }
                    col.a = 1f;
                    c.Set(x, y, col);
                }
            }
        }
    }
}
