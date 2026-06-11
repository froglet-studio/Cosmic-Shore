using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.App.Systems.CloudData
{
    /// <summary>
    /// Base repository with debounced save logic.
    /// Open/Closed: derive a new repository per data domain — no modifications needed here.
    /// Single Responsibility: handles only load/save lifecycle and debouncing.
    /// Liskov Substitution: any derived repository can substitute for this base.
    /// </summary>
    public abstract class CloudDataRepository<T> : ICloudDataRepository<T> where T : class, new()
    {
        readonly ICloudSaveProvider _provider;
        readonly float _debounceSecs;

        bool _dirty;
        bool _saveInFlight;

        protected T _data;

        public T Data => _data;
        public bool IsLoaded { get; private set; }
        public abstract string CloudKey { get; }
        public event Action OnDataChanged;

        protected CloudDataRepository(ICloudSaveProvider provider, float debounceSecs = 1.5f)
        {
            _provider = provider;
            _debounceSecs = debounceSecs;
            _data = new T();
        }

        public async Task LoadAsync(CancellationToken ct = default)
        {
            var cloudData = await _provider.LoadAsync<T>(CloudKey, ct);
            if (cloudData != null)
            {
                _data = cloudData;
                OnAfterLoad(_data);
            }

            IsLoaded = true;
            RaiseDataChanged();
        }

        public void MarkDirty()
        {
            _dirty = true;
            if (!_saveInFlight)
                _ = DebouncedSaveLoop();
        }

        public async Task SaveAsync(CancellationToken ct = default)
        {
            await _provider.SaveAsync(CloudKey, _data, ct);
        }

        /// <summary>
        /// Resets data to a fresh default instance and saves.
        /// </summary>
        public async Task ResetAsync(CancellationToken ct = default)
        {
            _data = new T();
            OnAfterLoad(_data);
            await SaveAsync(ct);
            RaiseDataChanged();
        }

        /// <summary>
        /// Hook for derived classes to fix up null collections after deserialization.
        /// </summary>
        protected virtual void OnAfterLoad(T data) { }

        protected void RaiseDataChanged()
        {
            OnDataChanged?.Invoke();
        }

        async Task DebouncedSaveLoop()
        {
            if (_saveInFlight) return;
            _saveInFlight = true;

            try
            {
                await Task.Delay((int)(_debounceSecs * 1000));

                while (_dirty)
                {
                    _dirty = false;
                    await SaveAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{GetType().Name}] Debounced save failed: {e.Message}");
            }
            finally
            {
                _saveInFlight = false;
                if (_dirty)
                    _ = DebouncedSaveLoop();
            }
        }
    }
}
