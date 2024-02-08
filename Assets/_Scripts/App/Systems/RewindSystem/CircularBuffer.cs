using System.Linq;

namespace CosmicShore.App.Systems.RewindSystem
{
    public class CircularBuffer<T>
    {
        T[] _dataArray;
        public void WriteLastValue(T valuesToWrite)
        {
            // TODO: Write last value logic here
        }

        public T ReadFromBuffer(float seconds)
        {
            // TODO: Read from buffer logic here
            return _dataArray.First();
        }

        public T ReadLastValue()
        {
            // TODO: Read last value logic here
            return _dataArray.Last();
        }
    }
}