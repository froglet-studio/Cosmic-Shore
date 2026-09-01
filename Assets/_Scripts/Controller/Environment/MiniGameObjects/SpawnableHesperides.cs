using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Hesperides" - the GARDEN cell (~12k authored prisms). The freestyle seven are worlds you
    /// fly through; this one is a world that <b>grows</b>. It authors only the garden's
    /// architecture - a five-terrace bowl of planting beds inside a walled rim, radial pergola
    /// arcades, trellis towers, an aqueduct ring feeding cascades into a central pool, a ring of
    /// hanging baskets, a vine-dome of meridian ribs, a super-shielded orchard gate, shielded
    /// fruit lanterns, and two bramble arcs of true danger prisms - and then <b>sows planting
    /// sites</b> (<see cref="FloraPlantingSite"/>) all over it: along every terrace, at every
    /// pergola foot and trellis foot, inside every hanging basket (normal pointing DOWN, so what
    /// roots there trails), and around the pool rim. Each site is TAGGED with its ground kind
    /// (<see cref="FloraSiteKind"/>) so species plant where they belong - reeds at the water,
    /// climbers at the column feet, bells in the baskets.
    ///
    /// The Cell's ordinary flora spawner plants into those sites through the ordinary spawn path,
    /// so the garden's canopy is made of LIVING flora - grazeable, joustable, starvable,
    /// crystal-dropping food-web citizens - not laid scenery. Authored bones (~12k) plus a
    /// mature planting (~21k) is the ~34k of <see cref="SpawnableYggdra"/>, but reached by growth
    /// instead of by lay: the cell arrives sparse and fills, and it stays full only while the food
    /// web lets it. Nothing here is on a clock; nothing decays. A garden the fauna strip back is
    /// a correct outcome, and the beds are still prepared ground when the pressure lifts.
    /// </summary>
    public class SpawnableHesperides : CellEnvironmentSpawnableBase
    {
        const float RimR = 420f;          // outer wall radius
        const float Floor = -150f;        // pool surface
        const float Rim = 120f;           // wall crown height
        const int Terraces = 5;

        protected override int DefaultSeed => 137;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableHesperides), 1);
        protected override int LayCapacity => 18000;

        /// <summary>Terrace i's ring radius - the bowl steps outward and upward from the pool.</summary>
        static float TerraceR(int i) => 120f + i * 62f;

        /// <summary>Terrace i's bed height - the bowl rises toward the rim.</summary>
        static float TerraceY(int i) => Floor + 22f + i * 34f;

        protected override void BuildEnvironment()
        {
            Terracing();
            OuterWall();
            Pergolas();
            TrellisTowers();
            Aqueduct();
            Pool();
            HangingBaskets();
            VineDome();
            OrchardGate();
            Brambles();
            Pollen();
        }

        // ── The beds ──────────────────────────────────────────────────────────

        /// <summary>
        /// Five concentric terraces: a five-course soil band with a retaining kerb on its outer
        /// edge. This is where most of the planting happens, so the sites are sown here first -
        /// the Cell shuffles them before dealing, so the first seeding batch spreads over the
        /// whole bowl rather than filling one terrace solid.
        /// </summary>
        void Terracing()
        {
            for (int t = 0; t < Terraces; t++)
            {
                float r = TerraceR(t);
                float y = TerraceY(t);
                int count = Scaled(Mathf.RoundToInt(r * 0.5f));

                for (int i = 0; i < count; i++)
                {
                    float a = 2f * Mathf.PI * i / count;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));

                    // Three radial courses of bed, chained tangentially so the terrace reads as a
                    // continuous band rather than dotted tiles. Deliberately THIN slabs: the bed
                    // is the stage, and volume is the spine - authored mass that reads as ground
                    // should not eat the headroom the planting is supposed to fill.
                    for (int c = -1; c <= 1; c++)
                    {
                        float rr = r + c * 13f;
                        float bump = 2.2f * (N01(rr * 0.02f, y * 0.02f, a, 3) - 0.5f);
                        Emit(new Vector3(radial.x * rr, y + bump, radial.z * rr),
                            SpawnPoint.LookRotation(tangent, Vector3.up),
                            Jit(new Vector3(6.4f, 1.1f, 6.2f)), Domains.Jade);
                    }

                    // Retaining kerb on the outer lip - the readable step between terraces.
                    Emit(new Vector3(radial.x * (r + 24f), y + 5f, radial.z * (r + 24f)),
                        SpawnPoint.LookRotation(tangent, Vector3.up),
                        Jit(new Vector3(3.2f, 5.5f, 6.4f)), Domains.Gold);

                    // Sow every other bed position, alternating the inner and outer course so
                    // plantings stagger across the band instead of forming one line.
                    if (i % 2 == 0)
                    {
                        float rr = r + ((i / 2) % 2 == 0 ? -12f : 12f);
                        Sow(new Vector3(radial.x * rr, y + 3f, radial.z * rr), Vector3.up,
                            FloraSiteKind.Bed);
                    }
                }
            }
        }

        /// <summary>The walled rim - the garden is enclosed, which is what makes it a garden.</summary>
        void OuterWall()
        {
            int count = Scaled(200);
            for (int i = 0; i < count; i++)
            {
                float a = 2f * Mathf.PI * i / count;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                float baseY = TerraceY(Terraces - 1) + 8f;

                for (int c = 0; c < 8; c++)
                {
                    // Crenellated crown: the top two courses drop out on a slow beat, so the wall
                    // has gaps to fly through instead of being a sealed drum.
                    if (c >= 6 && (i % 9) < 3) continue;
                    float y = baseY + c * (Rim / 8f);
                    Emit(new Vector3(radial.x * RimR, y, radial.z * RimR),
                        SpawnPoint.LookRotation(tangent, Vector3.up),
                        Jit(new Vector3(2.6f, 4.2f, 6.6f)), Domains.Jade);
                }
            }
        }

        // ── The built garden ──────────────────────────────────────────────────

        /// <summary>
        /// Twelve radial pergola arcades climbing the terraces - paired columns carrying a run of
        /// arches you fly under. Every column foot is prepared ground: this is where the climbers
        /// go, and a pergola with a vine on it is the whole point of a pergola.
        /// </summary>
        void Pergolas()
        {
            const int arcades = 12;
            for (int p = 0; p < arcades; p++)
            {
                float a = 2f * Mathf.PI * p / arcades + 0.13f;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));

                for (int bay = 0; bay < 8; bay++)
                {
                    float t = bay / 7f;
                    float r = 150f + t * 240f;
                    float y = Floor + 30f + t * (TerraceY(Terraces - 1) - Floor - 10f);
                    Vector3 foot = radial * r;

                    // Two columns straddling the walkway.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 basePos = new Vector3(foot.x, y, foot.z) + tangent * (s * 17f);
                        for (int c = 0; c < 6; c++)
                            Emit(basePos + Vector3.up * (c * 7.5f), Quaternion.identity,
                                Jit(new Vector3(2.8f, 7.2f, 2.8f)), Domains.Gold);

                        // Prepared ground at the column foot, growing straight up the column.
                        Sow(basePos + Vector3.up * 2f, Vector3.up, FloraSiteKind.Climb);
                    }

                    // The arch over the bay (fly under it).
                    Vector3 crown = new Vector3(foot.x, y + 45f, foot.z);
                    for (int i = 0; i < 14; i++)
                    {
                        float u = i / 13f;
                        float ang = Mathf.PI * u;
                        Vector3 pos = crown + tangent * (Mathf.Cos(ang) * 17f) +
                                      Vector3.up * (Mathf.Sin(ang) * 13f - 13f);
                        Vector3 along = tangent * -Mathf.Sin(ang) + Vector3.up * Mathf.Cos(ang);
                        Emit(pos, SpawnPoint.LookRotation(along, radial),
                            Jit(new Vector3(2.4f, 2.4f, 6.2f)), Domains.Gold);
                    }

                    // A shielded fruit lantern hangs from every other arch.
                    if (bay % 2 == 0)
                        Emit(crown + Vector3.up * 2f, Quaternion.identity,
                            new Vector3(4.4f, 4.4f, 4.4f), Domains.Ruby, PrismKind.Shielded);
                }
            }
        }

        /// <summary>
        /// Nine lattice towers on golden-angle bearings - four uprights laced with rungs. Sown at
        /// the foot and again at half height, so a climber can start partway up an old tower.
        /// </summary>
        void TrellisTowers()
        {
            const int towers = 9;
            for (int k = 0; k < towers; k++)
            {
                float a = k * GoldenAngle;
                int terrace = 1 + k % (Terraces - 1);
                float r = TerraceR(terrace) - 6f;
                float y0 = TerraceY(terrace) + 4f;
                float height = 96f + 28f * Hash01(k * 31 + _noiseSeed);
                Vector3 foot = new Vector3(Mathf.Cos(a) * r, y0, Mathf.Sin(a) * r);
                float half = 9f;

                for (int u = 0; u < 4; u++)
                {
                    float ua = a + Mathf.PI * 0.25f + u * Mathf.PI * 0.5f;
                    Vector3 offset = new Vector3(Mathf.Cos(ua), 0f, Mathf.Sin(ua)) * half;
                    for (int i = 0; i < 22; i++)
                    {
                        // Uprights twist slightly as they climb - a woven trellis, not a cage.
                        float t = i / 21f;
                        Vector3 twisted = Quaternion.AngleAxis(t * 26f, Vector3.up) * offset;
                        Emit(foot + twisted + Vector3.up * (t * height), Quaternion.identity,
                            Jit(new Vector3(1.8f, 5.4f, 1.8f)), Domains.Gold);
                    }
                }

                for (int rung = 0; rung < 12; rung++)
                {
                    float t = (rung + 0.5f) / 12f;
                    float ry = y0 + t * height;
                    for (int i = 0; i < 8; i++)
                    {
                        float ra = 2f * Mathf.PI * i / 8f + t * 0.45f;
                        var tangent = new Vector3(-Mathf.Sin(ra), 0f, Mathf.Cos(ra));
                        Emit(foot + new Vector3(Mathf.Cos(ra), 0f, Mathf.Sin(ra)) * (half * 1.05f) +
                             Vector3.up * (ry - y0),
                            SpawnPoint.LookRotation(tangent, Vector3.up),
                            Jit(new Vector3(1.5f, 1.5f, 5.4f)), Domains.Jade);
                    }
                }

                Sow(foot + Vector3.up * 3f, Vector3.up, FloraSiteKind.Climb);
                Sow(foot + Vector3.up * (height * 0.5f) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * half,
                    new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a)), FloraSiteKind.Climb);
                Sow(foot + Vector3.up * (height * 0.85f), Vector3.up, FloraSiteKind.Ledge);
            }
        }

        /// <summary>The aqueduct ring above the top terrace, with six cascades falling inward.</summary>
        void Aqueduct()
        {
            int count = Scaled(220);
            float y = TerraceY(Terraces - 1) + 62f;
            for (int i = 0; i < count; i++)
            {
                float a = 2f * Mathf.PI * i / count;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                for (int c = 0; c < 2; c++)
                    Emit(new Vector3(radial.x * 372f, y + c * 6f, radial.z * 372f),
                        SpawnPoint.LookRotation(tangent, Vector3.up),
                        Jit(new Vector3(2.6f, 3f, 6.6f)), Domains.Ruby);
            }

            for (int f = 0; f < 6; f++)
            {
                float a = 2f * Mathf.PI * f / 6f + 0.4f;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int i = 0; i < 60; i++)
                {
                    // The fall drifts inward as it descends and braids on the noise - water, not
                    // a plumb line.
                    float t = i / 59f;
                    float r = 372f - t * 250f;
                    float fy = y - t * (y - Floor - 4f);
                    float braid = 7f * (N01(t * 6f, f * 3f, 0f, 11) - 0.5f);
                    Vector3 pos = radial * r + Vector3.up * fy +
                                  new Vector3(-radial.z, 0f, radial.x) * braid;
                    Emit(pos, SpawnPoint.LookRotation(Vector3.down + radial * 0.6f, Vector3.up),
                        Jit(new Vector3(2.2f, 2.2f, 6.8f)), Domains.Ruby);
                }
            }
        }

        /// <summary>The central pool the cascades feed - a flat disc of still water at the floor.</summary>
        void Pool()
        {
            int count = Scaled(500);
            for (int i = 0; i < count; i++)
            {
                // Phyllotaxis disc: even coverage with no ring banding.
                float t = (i + 0.5f) / count;
                float r = 112f * Mathf.Sqrt(t);
                float a = i * GoldenAngle;
                Vector3 pos = new Vector3(Mathf.Cos(a) * r, Floor + 1.5f * (N01(r * 0.05f, a, 0f, 7) - 0.5f),
                    Mathf.Sin(a) * r);
                Emit(pos, SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up),
                    Jit(new Vector3(6.5f, 0.9f, 6.5f)), Domains.Ruby);
            }

            // Rim planting - reeds at the water's edge.
            for (int i = 0; i < 24; i++)
            {
                float a = 2f * Mathf.PI * i / 24f;
                Sow(new Vector3(Mathf.Cos(a) * 118f, Floor + 3f, Mathf.Sin(a) * 118f), Vector3.up,
                    FloraSiteKind.Water);
            }
        }

        /// <summary>
        /// A ring of baskets suspended from the vine dome. Their planting normal points DOWN, so
        /// what roots in them grows toward the floor - the trailing half of the garden. This is
        /// the whole reason a site carries a normal at all.
        /// </summary>
        void HangingBaskets()
        {
            const int baskets = 14;
            float y = TerraceY(Terraces - 1) + 118f;
            for (int b = 0; b < baskets; b++)
            {
                float a = 2f * Mathf.PI * b / baskets + 0.22f;
                float r = 190f + 70f * Hash01(b * 53 + _noiseSeed);
                Vector3 centre = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);

                for (int i = 0; i < 20; i++)
                {
                    float ha = 2f * Mathf.PI * i / 20f;
                    var tangent = new Vector3(-Mathf.Sin(ha), 0f, Mathf.Cos(ha));
                    Emit(centre + new Vector3(Mathf.Cos(ha), 0f, Mathf.Sin(ha)) * 15f,
                        SpawnPoint.LookRotation(tangent, Vector3.up),
                        Jit(new Vector3(1.8f, 2.6f, 5.4f)), Domains.Gold);
                }

                for (int s = 0; s < 3; s++)
                {
                    float ha = 2f * Mathf.PI * s / 3f;
                    Vector3 anchor = centre + new Vector3(Mathf.Cos(ha), 0f, Mathf.Sin(ha)) * 15f;
                    for (int i = 0; i < 10; i++)
                        Emit(Vector3.Lerp(anchor, centre + Vector3.up * 40f, i / 9f), Quaternion.identity,
                            Jit(new Vector3(1.2f, 4f, 1.2f)), Domains.Gold);
                }

                Sow(centre + Vector3.down * 3f, Vector3.down, FloraSiteKind.Basket);
            }
        }

        /// <summary>Meridian ribs arcing from the wall crown to a crown ring - the frame a mature
        /// garden's climbers eventually roof over.</summary>
        void VineDome()
        {
            const int ribs = 10;
            float baseY = TerraceY(Terraces - 1) + Rim;
            float apexY = baseY + 150f;
            for (int k = 0; k < ribs; k++)
            {
                float a = 2f * Mathf.PI * k / ribs;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int i = 0; i < 60; i++)
                {
                    float t = i / 59f;
                    float r = RimR * Mathf.Cos(t * Mathf.PI * 0.5f);
                    float y = baseY + (apexY - baseY) * Mathf.Sin(t * Mathf.PI * 0.5f);
                    Vector3 pos = radial * r + Vector3.up * y;
                    Vector3 along = radial * -Mathf.Sin(t * Mathf.PI * 0.5f) +
                                    Vector3.up * Mathf.Cos(t * Mathf.PI * 0.5f);
                    Emit(pos, SpawnPoint.LookRotation(along, radial),
                        Jit(new Vector3(2f, 2f, 7f)), Domains.Jade);
                }
            }

            for (int i = 0; i < 40; i++)
            {
                float a = 2f * Mathf.PI * i / 40f;
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                Emit(new Vector3(Mathf.Cos(a) * 34f, apexY, Mathf.Sin(a) * 34f),
                    SpawnPoint.LookRotation(tangent, Vector3.up),
                    Jit(new Vector3(2.4f, 2.4f, 6f)), Domains.Gold);
            }
        }

        /// <summary>
        /// The orchard gate - the garden's permanent bones. Super-shielded, so no force in the
        /// food web can take it: the gate is still standing whatever the fauna do to the planting.
        /// Kept small (96 prisms) because super-shielded mass carries an always-on MeshCollider.
        /// </summary>
        void OrchardGate()
        {
            float y = TerraceY(Terraces - 1) + 10f;
            for (int i = 0; i < 56; i++)
            {
                float t = i / 55f;
                float ang = Mathf.PI * t;
                Vector3 pos = new Vector3(RimR * Mathf.Cos(0f), y + Mathf.Sin(ang) * 78f, 0f) +
                              new Vector3(0f, 0f, Mathf.Cos(ang) * 62f);
                Vector3 along = new Vector3(0f, Mathf.Cos(ang), -Mathf.Sin(ang));
                Emit(pos, SpawnPoint.LookRotation(along, Vector3.right),
                    new Vector3(2.4f, 2.4f, 7.4f), Domains.Gold, PrismKind.SuperShielded);
            }

            for (int p = 0; p < 4; p++)
            {
                float z = (p < 2 ? -1f : 1f) * 62f;
                float x = RimR + (p % 2 == 0 ? -14f : 14f);
                for (int c = 0; c < 10; c++)
                    Emit(new Vector3(x, y + c * 7f, z), Quaternion.identity,
                        new Vector3(3.2f, 6.6f, 3.2f), Domains.Gold, PrismKind.SuperShielded);
            }
        }

        /// <summary>
        /// Two bramble arcs of TRUE danger prisms on the lowest terraces. A garden has thorns;
        /// danger prisms are not domain-safe, so these bite whoever brushes them - and per the
        /// diet rules they are ordinary food for a herbivore that finds them, unlike the gate.
        /// </summary>
        void Brambles()
        {
            for (int arc = 0; arc < 2; arc++)
            {
                float a0 = arc * Mathf.PI * 0.8f + 0.6f;
                float r = TerraceR(arc) + 30f;
                float y = TerraceY(arc) + 10f;
                for (int i = 0; i < 160; i++)
                {
                    float t = i / 159f;
                    float a = a0 + t * 1.5f;
                    // A tangled hedge: the thorn wanders in radius and height on the noise.
                    float rr = r + 16f * (N01(t * 9f, arc * 5f, 0f, 21) - 0.5f);
                    float yy = y + 18f * N01(t * 7f, arc * 2f, 1f, 22);
                    var tangent = new Vector3(-Mathf.Sin(a), 0.3f, Mathf.Cos(a));
                    Emit(new Vector3(Mathf.Cos(a) * rr, yy, Mathf.Sin(a) * rr),
                        SpawnPoint.LookRotation(tangent, Vector3.up),
                        Jit(new Vector3(1.6f, 1.6f, 5.2f)), Domains.Ruby, PrismKind.Danger);
                }
            }
        }

        /// <summary>Pollen: sparse motes drifting through the bowl volume - the air is not empty.</summary>
        void Pollen()
        {
            int count = Scaled(900);
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                float r = RimR * 0.94f * Mathf.Pow(t, 0.42f);
                float a = i * GoldenAngle;
                float y = Floor + 20f + RangeF(0f, TerraceY(Terraces - 1) + Rim - Floor);
                // Nudged along the curl field so the motes read as drifting currents, not a
                // uniform speckle.
                Vector3 pos = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                pos += Curl(pos * 0.004f, 1f, 31) * 22f;
                Emit(pos, Quaternion.Euler(RangeF(0f, 360f), RangeF(0f, 360f), RangeF(0f, 360f)),
                    Jit(new Vector3(1.3f, 1.3f, 1.3f), 0.35f), i % 3 == 0 ? Domains.Gold : Domains.Jade);
            }
        }
    }
}
