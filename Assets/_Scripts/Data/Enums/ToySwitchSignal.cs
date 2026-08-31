namespace CosmicShore.Data
{
    /// <summary>
    /// What a <b>switch</b>'s shader says it will do.
    ///
    /// <para>A switch is a ring you thread, and threading it activates something
    /// (CLAUDE.md, "Switch"). Every switch is drawn in the <b>prism shader</b> - the same
    /// material family the painted trail wears - so the one channel left to carry meaning is
    /// WHICH prism it is painted as. That channel is this enum.</para>
    ///
    /// <para><b>The reservation:</b> a switch wearing a playable DOMAIN's colour is reserved -
    /// inside the freestyle toybox it means <i>threading me makes your trail that domain</i>, and
    /// nothing else may wear one. Everything else is <see cref="Neutral"/>, painted in
    /// <c>Domains.Blue</c> - the platform's existing "no team / neutral entity" sentinel - so a
    /// neutral switch cannot be painted a playable domain even by mistake: the signal, not the
    /// caller, picks the colour.</para>
    ///
    /// <para>Two members is the whole vocabulary today, deliberately. Adding a verb is adding a
    /// member here plus its row in <c>ToyFactory.SwitchMaterial</c> - one place, so the language
    /// can grow without any switch builder learning about it.</para>
    /// </summary>
    public enum ToySwitchSignal
    {
        /// <summary>
        /// <i>Thread me and something happens.</i> Makes no claim about domain, and is painted
        /// <c>Domains.Blue</c> whatever domain a caller passes. The default for every switch.
        /// </summary>
        Neutral = 0,

        /// <summary>
        /// <i>This switch's colour names a DOMAIN.</i> In the toybox that is reserved to the
        /// things that hand you one - the Domain Changer's slots and the painting's stroke-start
        /// gates (both route through <c>Player.RequestSetDomain_ServerRpc</c>).
        ///
        /// <para>The one wearer outside the toybox is the Scarab's placed switch, where the
        /// colour names the domain the switch BELONGS to rather than one it grants. Nothing in
        /// that mode changes your domain, so the two readings never share a screen - but do not
        /// add a third toybox wearer without settling which reading wins.</para>
        /// </summary>
        Domain = 1,
    }
}
