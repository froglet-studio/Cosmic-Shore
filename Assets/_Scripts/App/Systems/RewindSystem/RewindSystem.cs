using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.App.Systems.RewindSystem
{
    public class RewindSystem : MonoBehaviour
    {
 
        /// <summary>
        /// Property defining how much into the past should be tracked. 
        /// You can edit this value to your preference
        /// </summary>
        [field:SerializeField] public float TrackSeconds { get; private set; } = 12;

        /// <summary>
        /// This property returns how many seconds are currently available for rewind
        /// </summary>
        public float AvailableSeconds { get; private set; }

        /// <summary>
        /// Tells you if scene is currently being rewound
        /// </summary>
        public bool IsRewound { get; private set; }

        /// <summary>
        /// Singleton instance of RewindManager
        /// </summary>
        public static RewindSystem Instance { get; private set; }

        /// <summary>
        /// Property defining if Circular buffers should be written to, when the system is not rewinding and should normally be tracking values.
        /// </summary>
        public bool TrackingEnabled { get; set; } = true;


        private float _rewindSeconds;
        private List<RewindBase> _rewoundObjects;

        /// <summary>
        /// Call this method to rewind time by specified seconds instantly without snapshot preview. Usefull for one time instant rewinds.
        /// </summary>
        /// <param name="seconds">Parameter defining how many seconds should object rewind to from now (Parameter must be >=0).</param>
        public void InstantRewindTimeBySeconds(float seconds)
        {
            if(seconds>AvailableSeconds)
            {
                Debug.LogError("Not enough stored tracked value!!! Reaching on wrong index. Called rewind should be less than HowManySecondsAvailableForRewind property");
                return;
            }
            if(seconds<0)
            {
                Debug.LogError("Parameter in RewindTimeBySeconds() must have positive value!!!");
                return;
            }
            _rewoundObjects.ForEach(x => x.Rewind(seconds));
            AvailableSeconds -= seconds;
            BuffersRestore?.Invoke(seconds);
        }
        /// <summary>
        /// Call this method if you want to start rewinding time with ability to preview snapshots. After done rewinding, StopRewindTimeBySeconds() must be called!!!. To update snapshot preview between, call method SetTimeSecondsInRewind().
        /// </summary>
        /// <param name="seconds">Parameter defining how many seconds before should the rewind preview rewind to (Parameter must be >=0)</param>
        /// <returns></returns>
        public void StartRewindTimeBySeconds(float seconds)
        {
            if (IsRewound)
                Debug.LogError("The previous rewind must be stopped by calling StopRewindTimeBySeconds() before you start another rewind");

            if (CheckReachingOutOfBounds(seconds))
            {
                _rewindSeconds = seconds;
                IsRewound = true;
            }
        }

        /// <summary>
        /// Call this method to update rewind preview while rewind is active (StartRewindTimeBySeconds() method was called before)
        /// </summary>
        /// <param name="seconds">Parameter defining how many seconds should the rewind preview move to (Parameter must be >=0)</param>
        public void SetTimeSecondsInRewind(float seconds)
        {
            if (CheckReachingOutOfBounds(seconds))
            {
                _rewindSeconds = seconds;
            }
        }
        /// <summary>
        /// Call this method to stop previewing rewind state and resume normal game flow
        /// </summary>
        public void StopRewindTimeBySeconds()
        {
            if (!IsRewound)
                Debug.LogError("Rewind must be started before you try to stop it. StartRewindTimeBySeconds() must be called first");

            AvailableSeconds -= _rewindSeconds;
            BuffersRestore?.Invoke(_rewindSeconds);
            IsRewound = false;
        }
        /// <summary>
        /// Call if you want to restart the whole tracking system
        /// </summary>
        public void RestartTracking()
        {
            if (IsRewound)
                StopRewindTimeBySeconds();

            AvailableSeconds = 0;
            TrackingEnabled = true;
        }
        private bool CheckReachingOutOfBounds(float seconds)
        {
            if (Mathf.Round(seconds*100) > Mathf.Round(AvailableSeconds*100))
            {
                Debug.LogError("Not enough stored tracked value!!! Reaching on wrong index. Called rewind should be less than HowManySecondsAvailableForRewind property");
                return false;
            }
            if (seconds < 0)
            {
                Debug.LogError("Parameter in StartRewindTimeBySeconds() must have positive value!!!");
                return false;
            }

            return true;
        }
        private void Awake()
        {
            _rewoundObjects = FindObjectsOfType<RewindBase>().ToList();

            if (Instance != null && Instance != this)
            {
                Destroy(Instance);
            }

            Instance = this;

            _rewoundObjects.ForEach(x => x.Init());
        }
        private void OnEnable()
        {
            AvailableSeconds = 0;
        }
        private  void FixedUpdate()
        {   
            if (IsRewound)
            {
                _rewoundObjects.ForEach(x => x.Rewind(_rewindSeconds));
            }
            else 
            {
                _rewoundObjects.ForEach(x => x.Track());

                if(TrackingEnabled)
                    AvailableSeconds = Mathf.Min(AvailableSeconds + Time.fixedDeltaTime, TrackSeconds);
            }
        }

        /// <summary>
        /// This action is not meant to be used by users. CircularBuffers listens to it
        /// </summary>
        public static Action<float> BuffersRestore { get; }
    }
}