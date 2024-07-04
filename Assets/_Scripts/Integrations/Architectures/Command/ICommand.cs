using UnityEngine;

namespace CosmicShore.Integrations.Architectures.Command
{
    public interface ICommand
    {
        public void Run()
        {
            Debug.Log($"Command is running {GetType().Name}");
        }
        
        public static ICommand Null { get; } = new NullCommand();
        
        public class NullCommand: ICommand
        {
            public void Run()
            {
                Debug.Log("Null command does nothing here.");
            }
        }
        public static T Create<T>() where T : ICommand, new()
        {
            return new T();
        }
    }
    
    public class CommandOne : ICommand{}
    public class CommandTwo : ICommand{}
}
