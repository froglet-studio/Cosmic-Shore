using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Base repository with debounced save logic.
    /// Open/Closed: derive a new repository per data domain - no modifications needed here.
    /// Single Responsibility: handles only load/save lifecycle and debouncing.
    /// Liskov Substitution: any derived repository can substitute for this base.
    /// </summary>
    public abstract class CloudDataRepository<T> : ICloudDataRepository<T> where T : class, new()
    {
        readonly ICloudSaveProvider _provider;
        readonly float _debounceSecs;

        bool _dirty;
        bool _saveInFlight;

        /// <summary>
        /// True when the last cloud load FAILED (as opposed to the key being missing). While set,
        /// <see cref="_data"/> is a local snapshot or fresh defaults, never the player's real cloud
        /// record, so uploading it could overwrite that record. Cloud writes are blocked until a
        /// load gets a definite answer.
        /// </summary>
        bool _cloudLoadFailed;

        protected T _data;

        public T Data => _data;
        public bool IsLoaded { get; private set; }

        /// <summary>
        /// True when <see cref="Data"/> came from the cloud or the local last-known-good snapshot -
        /// i.e. somebody once SAVED it. False while it is the fresh <c>new T()</c> default a missing
        /// key falls back to. Consumers that merge cloud state over local state (GameSetting) need
        /// the distinction: a default nobody wrote must never overwrite a value the player chose.
        /// </summary>
        public bool HasPersistedData { get; private set; }
        public bool IsDirty => _dirty;
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
            var loadResult = await _provider.TryLoadAsync<T>(CloudKey, ct);
            _cloudLoadFailed = loadResult.Status == CloudLoadStatus.Failed;
            var cloudData = loadResult.Data;
            if (cloudData != null)
            {
                _data = cloudData;
                HasPersistedData = true;
                OnAfterLoad(_data);

                // Refresh the last-known-good local snapshot so the NEXT launch can
                // restore this key even with no network (Steam offline mode).
                LocalCloudDataCache.Save(CloudKey, _data);
            }
            else
            {
                // Cloud failed (offline / not signed in / error) or key missing - fall back
                // to the last-known-good local snapshot so the player still gets their
                // profile, unlocks, episodes and settings. Cloud always wins when it
                // answers; this branch only runs when it did not.
                var cached = LocalCloudDataCache.TryLoad<T>(CloudKey);
                if (cached != null)
                {
                    _data = cached;
                    HasPersistedData = true;
                    OnAfterLoad(_data);
                }
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

        public async Task<bool> SaveAsync(CancellationToken ct = default)
        {
            // Mirror to the local snapshot FIRST, unconditionally - a save that fails
            // upstream (offline) still lands on disk, so progress made this session
            // survives a quit and is readable on the next offline launch.
            LocalCloudDataCache.Save(CloudKey, _data);
            HasPersistedData = true;

            // The load failed, so _data is a local stand-in, not the cloud record. Uploading it
            // could overwrite the real record with defaults or stale progress. Settle the question
            // first; stay dirty (return false) until the cloud gives a definite answer.
            if (_cloudLoadFailed)
            {
                switch (await TryResolveFailedLoadAsync(ct))
                {
                    case FailedLoadOutcome.StillBlocked: return false;
                    case FailedLoadOutcome.AdoptedCloud: return true;   // nothing left to upload
                }
            }

            return await _provider.SaveAsync(CloudKey, _data, ct);
        }

        enum FailedLoadOutcome { StillBlocked, SafeToWrite, AdoptedCloud }

        /// <summary>
        /// Retries a load that failed earlier: <see cref="FailedLoadOutcome.SafeToWrite"/> when the
        /// cloud confirmed there is no record, <see cref="FailedLoadOutcome.AdoptedCloud"/> when the
        /// real record turned up and replaced the local stand-in, otherwise
        /// <see cref="FailedLoadOutcome.StillBlocked"/>.
        /// </summary>
        async Task<FailedLoadOutcome> TryResolveFailedLoadAsync(CancellationToken ct)
        {
            // Offline: nothing to ask. Stay quiet - the debounce loop calls this every few seconds.
            if (!_provider.IsAvailable) return FailedLoadOutcome.StillBlocked;

            var retry = await _provider.TryLoadAsync<T>(CloudKey, ct);
            switch (retry.Status)
            {
                case CloudLoadStatus.Missing:
                    // The cloud answered: there is no record to protect. Writing is safe.
                    _cloudLoadFailed = false;
                    return FailedLoadOutcome.SafeToWrite;

                case CloudLoadStatus.Loaded:
                    // The real record exists. Cloud wins, exactly as on a normal load; the pending
                    // local changes were made on top of a stand-in and are dropped rather than
                    // written over it.
                    Debug.LogWarning($"[{GetType().Name}] Cloud record for '{CloudKey}' loaded after an earlier failure - adopting it instead of uploading local data.");
                    _cloudLoadFailed = false;
                    _dirty = false;
                    _data = retry.Data;
                    HasPersistedData = true;
                    OnAfterLoad(_data);
                    LocalCloudDataCache.Save(CloudKey, _data);
                    RaiseDataChanged();
                    return FailedLoadOutcome.AdoptedCloud;

                default:
                    return FailedLoadOutcome.StillBlocked;   // still failing
            }
        }

        /// <summary>
        /// Resets data to a fresh default instance and saves.
        /// </summary>
        public async Task ResetAsync(CancellationToken ct = default)
        {
            _data = new T();
            OnAfterLoad(_data);
            // A deliberate wipe: the caller WANTS the cloud record replaced, so a failed earlier
            // load must not turn it into "adopt the old record".
            _cloudLoadFailed = false;
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
                    bool saved = await SaveAsync();
                    if (!saved)
                    {
                        // Provider already retried with backoff. Keep the data dirty so
                        // the finally-reloop, the next mutation, network recovery, or an
                        // app-pause flush retries it - never drop a pending change.
                        _dirty = true;
                        break;
                    }
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
