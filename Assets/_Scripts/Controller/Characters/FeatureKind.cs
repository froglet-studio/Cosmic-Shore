namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The discrete (categorical) geometry a trait can swap in. One generator file per kind,
    /// dispatched by <see cref="FeatureCatalog"/>. <see cref="None"/> is a legitimate value: a
    /// trait that claims a slot with no geometry is a REMOVAL (external-ear absence), and a
    /// trait whose whole effect is texture and axis overrides also carries it.
    /// </summary>
    public enum FeatureKind
    {
        None = 0,
        VertebrateEye = 1,
        CompoundEye = 2,
        Pinna = 3,
        Beak = 4,
        Mandibles = 5,
        NoseLeaf = 6,
        Antennae = 7,
        Crest = 8,
        Blowhole = 9,
        Whiskers = 10,
        HairCap = 11,
        Fangs = 12,
        Trunk = 13,
        Goggles = 14,   // gear, not a clade trait: placed from the genome's GearKind
    }

    /// <summary>
    /// The regions of a head a trait can claim. A slot holds at most ONE feature; the resolver
    /// decides the winner. Human defaults fill Crown / Eyes / Ears / Nose / Mouth and lose to any
    /// expressed clade trait; a clade trait that claims a slot nobody else wants is simply added.
    /// </summary>
    public enum FeatureSlot
    {
        Crown = 0,
        Eyes = 1,
        Ears = 2,
        Nose = 3,
        Mouth = 4,
        Brow = 5,
        Cheeks = 6,
        Neck = 7,
    }

    /// <summary>
    /// Pilot gear a genome wears — the space-pilot dressing every shipped avatar illustration
    /// carries. Not a clade trait: the resolver places it from <c>CharacterGenome.Gear</c>.
    /// </summary>
    public enum GearKind
    {
        None = 0,
        GogglesUp = 1,   // goggles pushed up onto the forehead / hairline
        GogglesOn = 2,   // goggles worn over the eyes
    }

    public enum PupilKind
    {
        Round = 0,
        VerticalSlit = 1,
        HorizontalBar = 2,
        Dark = 3,      // iris and pupil one dark disc (corvid, cetacean)
        Compound = 4,  // no pupil — hexagonal facet field
    }

    public enum CoveringKind
    {
        Skin = 0,
        Fur = 1,
        Feather = 2,
        Scale = 3,
        Chitin = 4,
        Hide = 5,      // cetacean: smooth, wet, countershaded
    }

    public enum MarkingKind
    {
        None = 0,
        Stripes = 1,      // tabby
        Countershade = 2, // pale below, dark above
        Scutes = 3,       // plate cells with seams
        Sheen = 4,        // iridescent gradient (chitin, corvid)
        Ridges = 5,       // bat pinna / nose leaf ribbing
        Blaze = 6,        // a coloured stripe down the nose (MarkingColor) with cheek flanks in BaseB (mandrill)
    }
}
