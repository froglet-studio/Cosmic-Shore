using VContainer.Unity;

namespace CosmicShore.Integrations.VContainer
{
    public class TestPresenter : IStartable
    {
        private readonly TestService _testService;
        private readonly TestMenu _testMenu;

        public TestPresenter(TestService testService, TestMenu testMenu)
        {
            _testService = testService;
            _testMenu = testMenu;
        }

        public void Start()
        {
            // _testService.TestService1();
            _testMenu.testButton.onClick.AddListener(() => _testService.TestService1());
        }
    }
}
