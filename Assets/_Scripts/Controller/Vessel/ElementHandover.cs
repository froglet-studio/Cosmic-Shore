using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A hull's element levels travel with it when it changes machines (Garrett, 2026-10-27: "that issue is true for any
    /// vessel swapping situation ... it should be resolved"). Element levels are simulated on the machine that OWNS a hull,
    /// so a pilot swap, a crew seat change or a Cellular Duel swap that hands the hull to another machine used to keep the
    /// levels the NEW owner's replica happened to hold - the crystals the old owner had collected did not come along.
    ///
    /// <para><b>What travels is the BASE level</b> (<c>ResourceSystem.BaseLevel</c>): the petals the hull holds. The owner
    /// publishes it (<c>R_VesselActionHandler.NetElementBase</c>, owner-write), and the machine that takes the hull over
    /// adopts it before it simulates or publishes anything (<c>R_VesselElementalAbilityHandler</c>). Nothing else is
    /// petals, so nothing else travels: a temporary buff ends (a buff is never a transfer, <c>ELEMENTAL_ECONOMY.md</c>),
    /// the comeback bonus is recomputed by the new owner, and a pending fraction of a petal not yet knocked loose is
    /// forgotten, as a re-seed already forgets it. None of those holds a petal, so the hand-over neither mints nor loses
    /// one. The latched level-5 unlocks travel too (from <c>NetElementUnlocks</c>), so an upgrade held at a level below its
    /// unlock stays held.</para>
    ///
    /// <para>The pure rules, edit-mode tested by <c>ElementHandoverTests</c>; the components order is the replicated
    /// nibbles' (Charge, Mass, Space, Time).</para>
    /// </summary>
    public static class ElementHandover
    {
        /// <summary>The smallest change of a base level that is published: a hundredth of a petal. Base levels drift
        /// continuously only outside the resting band (the overcharge drain, the deficit recovery), and this keeps that
        /// drift from dirtying the variable every frame.</summary>
        public const float PublishStep = 0.001f;

        /// <summary>A gain of ownership is a HAND-OVER (adopt the published levels) only once the hull has spawned and only
        /// when this machine did not already own it. At spawn the owner's own seeded levels are the truth.</summary>
        public static bool IsHandover(bool spawned, bool wasOwner) => spawned && !wasOwner;

        /// <summary>Whether the owner should write <paramref name="local"/> over <paramref name="published"/>.</summary>
        public static bool ShouldPublish(Vector4 published, Vector4 local, bool everPublished)
        {
            if (!everPublished) return true;
            return Mathf.Abs(published.x - local.x) > PublishStep || Mathf.Abs(published.y - local.y) > PublishStep ||
                   Mathf.Abs(published.z - local.z) > PublishStep || Mathf.Abs(published.w - local.w) > PublishStep;
        }

        /// <summary>The component of <paramref name="levels"/> that holds <paramref name="element"/>; 0 for any other.</summary>
        public static float Get(Vector4 levels, Element element) => element switch
        {
            Element.Charge => levels.x,
            Element.Mass => levels.y,
            Element.Space => levels.z,
            Element.Time => levels.w,
            _ => 0f,
        };

        /// <summary>Four base levels in the replicated order.</summary>
        public static Vector4 Pack(float charge, float mass, float space, float time) => new(charge, mass, space, time);
    }
}
