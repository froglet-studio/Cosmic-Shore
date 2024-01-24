using VContainer.Unity;

namespace CosmicShore.Integrations.VContainer
{
    public class TestPresenter : IStartable
    {
        private readonly IService _testService;
        private readonly TestMenu _testMenu;

        public TestPresenter(IService testService, TestMenu testMenu)
        {
            _testService = testService;
            _testMenu = testMenu;
        }

        public void Start()
        {
            // _testService.TestService1();
            _testMenu.testButton.onClick.AddListener(() => _testService.Call());
        }
    }
}
