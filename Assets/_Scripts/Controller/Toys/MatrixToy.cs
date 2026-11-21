using System.Collections.Generic;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A toy that offers <b>a list of choices</b> - the Cell Selector, the Connect-the-Dots gallery,
    /// the Vessel Changer, the Element Charger, the Spawn Matrix, Wander. It used to unfold into a
    /// matrix of fly-through stations; since 2026-11-01 its choices live only in the app shell's
    /// Toy Box window, which reads them from here.
    ///
    /// <para><b>ONE DECLARATION.</b> A matrix toy declares its choices exactly once, in
    /// <see cref="BuildOptions"/>, and this class answers
    /// <see cref="IToyShellSurface.BuildShellOptions"/> with that call and nothing else - so a
    /// subclass has nowhere to put a second opinion about which choices exist or what one does.</para>
    /// </summary>
    public abstract class MatrixToy : Toy, IToyShellSurface
    {
        /// <summary>
        /// Fill <paramref name="into"/> (already cleared) with every choice this toy offers.
        /// <b>The only place a matrix toy names its choices.</b>
        ///
        /// <para>Set <see cref="ToyShellOption.Payload"/> to the toy's own subject (the config, the
        /// class, the definition) when its preview or a deeper layer needs it.</para>
        /// </summary>
        protected abstract void BuildOptions(List<ToyShellOption> into);

        ToyDefinitionSO IToyShellSurface.ShellDefinition => Definition;

        /// <summary>
        /// False while the toy cannot answer - mid cell-swap, mid vessel-swap. Default true;
        /// override where "what am I flying / where am I" has no settled answer for a moment.
        /// </summary>
        protected virtual bool ShellAvailable => true;

        bool IToyShellSurface.ShellAvailable => ShellAvailable;

        /// <summary>
        /// The window's option list is <see cref="BuildOptions"/> and nothing else. Deliberately
        /// NOT virtual: a subclass that could override this could reintroduce a second enumeration.
        /// </summary>
        void IToyShellSurface.BuildShellOptions(List<ToyShellOption> into) => BuildOptions(into);
    }
}
