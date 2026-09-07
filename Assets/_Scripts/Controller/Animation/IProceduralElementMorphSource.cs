using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A component that morphs a vessel's hull for element levels WITHOUT blend shapes — the
    /// procedural path (the Scarab's <see cref="ScarabHullBuilder"/> re-blends baked geometry
    /// deltas instead of driving FBX shape keys). Exists so the fleet's morph accounting stays
    /// honest: FrogletTools &gt; Vessels &gt; Audit Vessel Elemental Morphs reports these sources
    /// as real coverage, and — the half that prevents a LIE — uses
    /// <see cref="HiddenLegacyModelRoot"/> to mark blend shapes that only exist on a hidden
    /// placeholder model as inert, rather than counting them as the vessel's morph surface.
    /// (The Scarab wraps the Sparrow FBX with its renderers switched off; without this, the
    /// audit would report the Scarab morph-complete via a model nobody can see.)
    /// </summary>
    public interface IProceduralElementMorphSource
    {
        /// <summary>The elements this source actually morphs the visible hull for.</summary>
        IReadOnlyList<Element> ProceduralMorphElements { get; }

        /// <summary>Root of the hidden legacy model whose renderers this source switched off —
        /// element blend shapes under it do not reach the screen. Null when there is none.</summary>
        Transform HiddenLegacyModelRoot { get; }
    }
}
