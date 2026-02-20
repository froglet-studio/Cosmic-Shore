using System;
using UnityEngine;

namespace CosmicShore.App.Systems.RewindSystem
{
    public class CircularBuffer <T>
    {
        private readonly T[] _dataStorage;
        private int _currentIndex = -1;
        private int _capacity;
        private float _recordsPerSecond;
    
        /// <summary>
        /// Use circular buffer structure for time rewinding
        /// </summary>
        public CircularBuffer()
        {
            try
            {
                _recordsPerSecond = 1 / Time.fixedDeltaTime;
                _capacity = (int)_recordsPerSecond;
                _dataStorage = new T[_capacity];
                // RewindManager.BuffersRestore += MoveLastBufferPosition;
            }
            catch
            {
                Debug.LogError("Circular buffer cannot use field initialization (Time.fixedDeltaTime is unknown yet). Initialize Circular buffer in Start() method!");
            }        
        }
        
        /// <summary>
        /// Write value to the last position of the buffer if Tracking is enabled
        /// </summary>
        /// <param name="val"></param>
        public void WriteLastValue(T val)
        {
            // if (RewindManager.Instance.TrackingEnabled)
            {
                _currentIndex++;
                if (_currentIndex >= _capacity)
                {
                    _currentIndex = 0;
                    _dataStorage[_currentIndex] = val;
                }
                else
                {
                    _dataStorage[_currentIndex] = val;
                }
            }
        }
        /// <summary>
        /// Read last value that was written to buffer
        /// </summary>
        /// <returns></returns>
        public T ReadLastValue()
        {
            return _dataStorage[_currentIndex];
        }
    
        /// <summary>
        /// Read specified value from circular buffer
        /// </summary>
        /// <param name="seconds">Variable defining how many seconds into the past should be read (eg. seconds=5 then function will return the values that tracked object had exactly 5 seconds ago)</param>
        /// <returns></returns>
        public T ReadFromBuffer(float seconds)
        {
            return _dataStorage[CalculateIndex(seconds)];
        }
        private void MoveLastBufferPosition(float seconds)
        {
            _currentIndex= CalculateIndex(seconds);    
        }
        private int CalculateIndex(float seconds)
        {
            double secondsRound = Math.Round(seconds, 2);
            int howManyBeforeLast = (int)(_recordsPerSecond * secondsRound);
    
            int moveBy = _currentIndex - howManyBeforeLast;
            if (moveBy < 0)
            {
                return _capacity + moveBy;
            }
            else
            {
                return _currentIndex- howManyBeforeLast;
            }
        }
    }
}