using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.Architectures.MessageSystem
{
    /// <summary>
    /// A class lets a list of disposables auto-managing by GC 
    /// </summary>
    public class Disposables : IDisposable
    {
        /// <summary>
        /// A list of disposables references to be managed
        /// </summary>
        private readonly List<IDisposable> _disposables = new();

        /// <summary>
        /// IDisposable implementation, the disposables eventually being auto disposed by GC and the list gets cleared.
        /// </summary>
        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
            _disposables.Clear();
        }

        /// <summary>
        /// Add a disposable to the list, basically a wrapper method for list Add
        /// </summary>
        /// <param name="disposable"></param>
        public void Add(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }
    }
}
