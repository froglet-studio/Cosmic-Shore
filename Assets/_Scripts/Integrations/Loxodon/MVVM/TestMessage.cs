using Loxodon.Framework.Messaging;


namespace CosmicShore.Integrations.Loxodon.MVVM
{
    public class TestMessage : MessageBase
    {
        public string Content { get; }
        public TestMessage(object sender, string content) : base(sender)
        {
            Content = content;
        }
    }
}
