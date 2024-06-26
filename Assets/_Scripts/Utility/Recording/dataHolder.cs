#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace CosmicShore.Utility
{
    /// <summary>
    /// A simple container for the data that will be used by the Asset Recorder.
    /// There should be one instance of this components on an object whose name is
    /// set in the member <see cref="RecorderWindow.ManagerName" />.
    /// </summary>
    public class DataHolder : MonoBehaviour
    {
        #pragma warning disable 0414

        /// <summary>
        /// The overall manager for the timeline to record.
        /// </summary>
        [SerializeField]
        private PlayableDirector director;

        /// <summary>
        /// An asset (on disk) for the contents of the timeline.
        /// </summary>
        [SerializeField]
        private TimelineAsset timelineAsset;

        /// <summary>
        /// Where all items generated or expcted from this utility should be stored.
        /// The path is relative to the project and will usually start with "Assets/".
        /// </summary>
        [SerializeField]
        private string assetsPath = "Assets/Recorder";

        /// <summary>
        /// The game objects that this recorder will track.
        /// </summary>
        [SerializeField]
        private Animator[] objectsToTrack;

        /// <summary>
        /// The time that the recorder will wait for between each snapshot, in seconds.
        /// A larger number means a less detailed capture. A smaller number means
        /// a larger amount of data will be recorder.
        /// </summary>
        [SerializeField]
        private float recordingDelay = 1;

        /// <summary>
        /// An arbirtrary name to add to the recording.
        /// Will be applied to each new track. Cannot be empty.
        /// </summary>
        [SerializeField]
        private string trackName;

        #pragma warning restore 0414
    }
}
#endif