using CosmicShore.Data;
using CosmicShore.Gameplay;
using System;
﻿
namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public struct CrystalProperties
    {
        public Crystal crystal;
        public float fuelAmount;
        public int scoreAmount;
        public float tailLengthIncreaseAmount;
        public float speedBuffAmount;
        public Element Element;
        public float crystalValue;

        public readonly bool IsElemental => Element == Element.Mass || Element == Element.Charge || Element == Element.Space || Element == Element.Time;
    }
}
