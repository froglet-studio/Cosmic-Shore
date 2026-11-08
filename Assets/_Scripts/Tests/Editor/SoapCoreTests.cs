using System.Collections.Generic;
using CosmicShore.Soap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The behaviour the game relies on from our SOAP (Assets/Plugins/CosmicShoreSoap).</summary>
    public class SoapCoreTests
    {
        public class IntList : ScriptableList<int> { }

        public class Subscriber : ScriptableObject
        {
            public int Hits;
            public void OnRaised() => Hits++;
        }

        readonly List<Object> _made = new();

        T Make<T>() where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            _made.Add(made);
            return made;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var made in _made) Object.DestroyImmediate(made);
            _made.Clear();
        }

        [Test]
        public void Variable_NotifiesOnlyWhenTheValueChanges()
        {
            var variable = Make<IntVariable>();
            var seen = new List<int>();
            variable.OnValueChanged += seen.Add;

            variable.Value = 3;
            variable.Value = 3;
            variable.Value = 5;

            CollectionAssert.AreEqual(new[] { 3, 5 }, seen);
            Assert.AreEqual(5, (int)variable, "a variable reads as its value");
        }

        [Test]
        public void Variable_PreviousValue_IsTheOldValueWhileSubscribersRun()
        {
            var variable = Make<StringVariable>();
            variable.Value = "first";
            string previousDuringCallback = null;
            variable.OnValueChanged += _ => previousDuringCallback = variable.PreviousValue;

            variable.Value = "second";

            Assert.AreEqual("first", previousDuringCallback);
        }

        [Test]
        public void IntVariable_Clamps_WhenClampedInTheAsset()
        {
            var variable = Make<IntVariable>();
            var so = new SerializedObject(variable);
            so.FindProperty("_isClamped").boolValue = true;
            so.FindProperty("_minMax").vector2IntValue = new Vector2Int(0, 10);
            so.ApplyModifiedPropertiesWithoutUndo();

            variable.Value = 42;

            Assert.AreEqual(10, variable.Value);
        }

        [Test]
        public void List_AddRemoveAndClear_RaiseTheirEvents()
        {
            var list = Make<IntList>();
            var log = new List<string>();
            list.OnItemCountChanged += () => log.Add("count");
            list.OnItemAdded += i => log.Add("added " + i);
            list.OnItemRemoved += i => log.Add("removed " + i);
            list.OnCleared += () => log.Add("cleared");

            list.Add(1);
            list.Add(2);
            Assert.IsTrue(list.Remove(1));
            Assert.IsFalse(list.Remove(9));
            list.Clear();

            CollectionAssert.AreEqual(
                new[] { "count", "added 1", "count", "added 2", "count", "removed 1", "cleared" }, log);
            Assert.IsTrue(list.IsEmpty);
        }

        [Test]
        public void List_TryAdd_SkipsAnItemAlreadyThere()
        {
            var list = Make<IntList>();

            Assert.IsTrue(list.TryAdd(7));
            Assert.IsFalse(list.TryAdd(7));
            Assert.AreEqual(1, list.Count);
            Assert.IsTrue(list.Contains(7));
        }

        [Test]
        public void List_IndexerSet_RaisesNothing()
        {
            var list = Make<IntList>();
            list.Add(1);
            int events = 0;
            list.OnItemAdded += _ => events++;
            list.OnItemRemoved += _ => events++;
            list.OnItemCountChanged += () => events++;

            list[0] = 2;

            Assert.AreEqual(2, list[0]);
            Assert.AreEqual(0, events, "HostConnectionService relies on RemoveAt + Insert for events, and on the indexer for none");
        }

        [Test]
        public void Event_GetAllObjects_ListsEachSubscribingObjectOnce()
        {
            var channel = Make<ScriptableEventNoParam>();
            var subscriber = Make<Subscriber>();

            channel.OnRaised += subscriber.OnRaised;
            channel.OnRaised += subscriber.OnRaised;
            CollectionAssert.AreEqual(new Object[] { subscriber }, channel.GetAllObjects());

            channel.OnRaised -= subscriber.OnRaised;
            channel.OnRaised -= subscriber.OnRaised;
            CollectionAssert.IsEmpty(channel.GetAllObjects());
        }

        [Test]
        public void Event_RaisedOutsidePlayMode_DoesNothing()
        {
            var channel = Make<ScriptableEventInt>();
            int heard = 0;
            channel.OnRaised += _ => heard++;

            channel.Raise(1);

            Assert.AreEqual(0, heard);
        }
    }
}
