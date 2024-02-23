using CosmicShore.Utility.ClassExtensions;
using UnityEngine;

namespace CosmicShore.App.Systems.RewindSystem
{
    public abstract class RewindBase : MonoBehaviour
    {
        private CircularBuffer<bool> _trackedActiveStates;

        private CircularBuffer<TransformValues> _trackedTransforms;

        protected virtual void Start()
        {
            _trackedActiveStates = new();
            _trackedTransforms = new();
        }

        public void Init()
        {
            //TODO: Init is suppose to grab any relevant components on the game object.
            Debug.Log("RewindBase.Init() is called.");
        }

        #region ActiveState

        /// <summary>
        /// Call this method in Track() if you want to track object active state
        /// </summary>
        protected void TrackObjectActiveState()
        {
            _trackedActiveStates.WriteLastValue(gameObject.activeSelf);
        }

        /// <summary>
        /// Call this method in Rewind() to restore object active state
        /// </summary>
        /// <param name="seconds">Use seconds parameter from Rewind() method</param>
        protected void RestoreObjectActiveState(float seconds)
        {
            gameObject.SetActive(_trackedActiveStates.ReadFromBuffer(seconds));
        }

        #endregion

        #region Transform

        /// <summary>
        /// Call this method in Track() if you want to track object Transforms (position, rotation and scale)
        /// </summary>
        protected void TrackTransform()
        {
            TransformValues valuesToWrite = new(transform.position, transform.rotation, transform.localScale);
            _trackedTransforms.WriteLastValue(valuesToWrite);
        }
        /// <summary>
        /// Call this method in Rewind() to restore Transform
        /// </summary>
        /// <param name="seconds">Use seconds parameter from Rewind() method</param>
        protected void RestoreTransform(float seconds)
        {
            var valuesToRead = _trackedTransforms.ReadFromBuffer(seconds);
            transform.SetFullProperties(valuesToRead.position, valuesToRead.rotation, valuesToRead.scale);
        }
        #endregion


        #region Animator

        /// <summary>
        /// Call this method in Track() if you want to track Animator states
        /// </summary>
        protected void TrackAnimator()
        {

                   
        }

        protected void RestoreAnimator(float seconds)
        {
          
        }
        #endregion

        #region Audio


        protected void TrackAudio()
        {
          
        }


        protected void RestoreAudio(float seconds)
        {
           
        }
        #endregion



        public abstract void Track();

        public abstract void Rewind(float seconds);

    }
}