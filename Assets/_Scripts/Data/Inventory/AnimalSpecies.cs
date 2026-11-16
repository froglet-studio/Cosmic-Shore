using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// The animals a pilot is part of (Docs/Pilots/INVENTORY.md §3.2). Every pilot is human plus two of these
    /// (Garrett, 2026-11-07: "a fun animal combo (human, species 1, species 2)").
    ///
    /// <para>Values are pinned and saved in pilot records: never reuse or renumber one; retire it by leaving it out of
    /// <see cref="Species.Rollable"/>. The list leaves out every vessel class and callsign word (Squirrel, Manta,
    /// Hornet, Doe...), so "a heron-axolotl pilot" never reads as a hull or a sight.</para>
    /// </summary>
    public enum AnimalSpecies
    {
        None = 0,
        Axolotl = 1,
        Heron = 2,
        Otter = 3,
        Pangolin = 4,
        Gecko = 5,
        Lynx = 6,
        Narwhal = 7,
        Capybara = 8,
        Octopus = 9,
        Raven = 10,
        Tortoise = 11,
        Fennec = 12,
        Mantis = 13,
        Walrus = 14,
        Ibis = 15,
        Tapir = 16,
        Jellyfish = 17,
        Badger = 18,
        Chameleon = 19,
        Owl = 20,
        Seahorse = 21,
        Wolf = 22,
        Lemur = 23,
        Cuttlefish = 24,
        Moose = 25,
        Hummingbird = 26,
        Armadillo = 27,
        Koi = 28,
        Hyena = 29,
        Platypus = 30,
        Crane = 31,
        Beetle = 32,
        Bison = 33,
        Newt = 34,
        Puffin = 35,
        Jackal = 36,
        Sloth = 37,
        Lobster = 38,
        Ocelot = 39,
        Pelican = 40,
        Hedgehog = 41,
        Mole = 42,
        Toucan = 43,
        Yak = 44,
        Tardigrade = 45,
        Firefly = 46,
        Orangutan = 47,
        Eel = 48,

        // The animals of the 32 pilots painted before pilots were rolled (Docs/Pilots/PORTRAITS.md §2, the painted crew:
        // Docs/Pilots/art/scenes/people.json). Painted only: past LastRollable, so no new pilot rolls one until the
        // pilot-art kit (Tools/Build/pilot_art/brief.py) has its lines and LastRollable moves. Orca and Butterfly are
        // hulls, so the art's orca is a KillerWhale and its butterfly a Swallowtail.
        Snail = 49,
        HornedLizard = 50,
        Clownfish = 51,
        TreeFrog = 52,
        Scorpion = 53,
        Mouse = 54,
        Cheetah = 55,
        Oryx = 56,
        KillerWhale = 57,
        Eagle = 58,
        Coyote = 59,
        Turkey = 60,
        Hare = 61,
        Skunk = 62,
        Swallowtail = 63,
        Ostrich = 64,
        Porcupine = 65,
        Zebra = 66,
        Lionfish = 67,
        Egret = 68,
        Caracal = 69,
        RedPanda = 70,
        Flamingo = 71,
        MantisShrimp = 72,
        Shoebill = 73,
        Crocodile = 74,
        Tiger = 75,
        Parrot = 76,
        Basilisk = 77,
        Hawk = 78,
        Booby = 79,
        Mandrill = 80,
        Peacock = 81,
        Opossum = 82,
        Fox = 83,
        Ram = 84,
        Deer = 85,
        Kangaroo = 86,
    }

    /// <summary>Names and combos for <see cref="AnimalSpecies"/>.</summary>
    public static class Species
    {
        static AnimalSpecies[] _rollable;

        /// <summary>The last species a new pilot can roll: the ones after it are painted pilots' alone.</summary>
        public const AnimalSpecies LastRollable = AnimalSpecies.Eel;

        /// <summary>Every species a new pilot can roll (None and the painted-only ones after <see cref="LastRollable"/>
        /// excluded).</summary>
        public static AnimalSpecies[] Rollable
        {
            get
            {
                if (_rollable != null) return _rollable;
                var values = (AnimalSpecies[])Enum.GetValues(typeof(AnimalSpecies));
                Array.Sort(values, (a, b) => ((int)a).CompareTo((int)b));
                _rollable = Array.FindAll(values, v => v != AnimalSpecies.None && v <= LastRollable);
                return _rollable;
            }
        }

        /// <summary>The species as players read it, e.g. "Axolotl", "Red panda", "Killer whale".</summary>
        public static string Name(AnimalSpecies s)
        {
            if (s == AnimalSpecies.None) return string.Empty;
            string word = s.ToString();
            var sb = new System.Text.StringBuilder(word.Length + 2);
            for (int i = 0; i < word.Length; i++)
            {
                char c = word[i];
                if (i > 0 && char.IsUpper(c)) sb.Append(' ').Append(char.ToLowerInvariant(c));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>The combo as players read it: "Human / Axolotl / Heron".</summary>
        public static string Combo(AnimalSpecies first, AnimalSpecies second) =>
            "Human / " + Name(first) + " / " + Name(second);

        /// <summary>
        /// The fun one-word blend of the two animals: the front of the first and the back of the second
        /// (Axolotl + Heron = "Axoleron"). Deterministic, so the same pilot always reads the same.
        /// </summary>
        public static string Blend(AnimalSpecies first, AnimalSpecies second)
        {
            string a = Name(first).Replace(" ", string.Empty), b = Name(second).Replace(" ", string.Empty);
            if (a.Length == 0) return b;
            if (b.Length == 0) return a;
            string front = a.Substring(0, Math.Max(2, (a.Length + 1) / 2));
            string back = b.Substring(b.Length / 2);
            // Avoid a doubled letter at the seam ("Otte" + "ron").
            if (front.Length > 0 && back.Length > 0 && char.ToLowerInvariant(front[front.Length - 1]) == char.ToLowerInvariant(back[0]))
                back = back.Substring(1);
            return front + back.ToLowerInvariant();
        }
    }
}
