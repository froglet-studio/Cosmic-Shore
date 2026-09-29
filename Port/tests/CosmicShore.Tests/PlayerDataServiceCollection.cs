using Xunit;

namespace CosmicShore.Tests;

/// <summary>
/// Test classes that create a <c>PlayerDataService</c> share its static <c>Instance</c>
/// singleton (a second Awake destroys itself), so they must not run in parallel.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PlayerDataServiceCollection
{
    public const string Name = "PlayerDataService singleton";
}
