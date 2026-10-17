using CosmicShore.Content;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

/// <summary>
/// <c>Application.CanStreamedLevelBeLoaded</c>: the original contract is "an enabled entry of the
/// build list", which is narrower than what this engine can load (any project scene). The load
/// sweep asks it to skip a card a Unity player could not launch, so the answer has to be the
/// player's, read from the content runtime's build list.
/// </summary>
public class BuildListTests
{
    [Fact]
    public void ContentRuntime_AnswersFromTheEnabledBuildList()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        using var loop = new GameLoop("BuildListTests");
        var runtime = new ContentRuntime(ContentYamlTests.ProjectRoot, new[] { typeof(GameObject).Assembly });

        Assert.True(runtime.IsInBuildList("Menu_Main"), "an enabled entry, by bare name");
        Assert.True(runtime.IsInBuildList("menu_main"), "names match case-insensitively, as the scene lookup does");
        Assert.True(runtime.IsInBuildList("Assets/_Scenes/Menu_Main.unity"), "an enabled entry, by path");
        Assert.False(runtime.IsInBuildList("PhotoBooth"), "listed but disabled: a player cannot load it");
        Assert.False(runtime.IsInBuildList("NiceVibrationsDemo"), "a project scene outside the build list");
        Assert.False(runtime.IsInBuildList("MinigameBlockBandit"), "a retired card's scene that no longer exists");
        Assert.False(runtime.IsInBuildList(""));
        Assert.False(runtime.IsInBuildList(null));
    }

    [Fact]
    public void Application_EmptyNameIsNeverLoadable()
    {
        Assert.False(Application.CanStreamedLevelBeLoaded(null));
        Assert.False(Application.CanStreamedLevelBeLoaded(""));
    }
}
