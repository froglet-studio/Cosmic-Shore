// using System;
// using System.Collections;
// using System.Collections.Generic;
// using NUnit.Framework;
// using UnityEngine;
// using UnityEngine.TestTools;
//
// public class MessageSystemTests
// {
//      struct EmptyMessage { }
//
//         int m_NbMessagesReceived;
//
//         IDisposable SubscribeToChannel(MessagesSystemV2<EmptyMessage> channel, int nbSubscribers)
//         {
//             var subscriptions = new DisposableGroup();
//             for (int i = 0; i < nbSubscribers; i++)
//             {
//                 subscriptions.Add(channel.Subscribe(Subscription));
//             }
//
//             return subscriptions;
//         }
//
//         void Subscription(EmptyMessage message)
//         {
//             m_NbMessagesReceived++;
//         }
//
//         void PublishMessages(MessageChannel<EmptyMessage> channel, int nbMessages)
//         {
//             for (int i = 0; i < nbMessages; i++)
//             {
//                 channel.Publish(new EmptyMessage());
//             }
//         }
//
//         [SetUp]
//         public void Setup()
//         {
//             m_NbMessagesReceived = 0;
//         }
//
//         [Test]
//         [TestCase(2, 1)]
//         [TestCase(3, 2)]
//         [TestCase(5, 3)]
//         public void MessagePublishedIsReceivedByAllSubscribers(int nbSubscribers, int nbMessages)
//         {
//             var messageChannel = new MessageChannel<EmptyMessage>();
//             var subscriptions = SubscribeToChannel(messageChannel, nbSubscribers);
//
//             PublishMessages(messageChannel, nbMessages);
//             Assert.AreEqual(nbSubscribers * nbMessages, m_NbMessagesReceived);
//             subscriptions.Dispose();
//         }
//
//         [Test]
//         [TestCase(2, 1)]
//         [TestCase(3, 2)]
//         [TestCase(5, 3)]
//         public void MessagePublishedIsNotReceivedByAllSubscribersAfterUnsubscribing(int nbSubscribers, int nbMessages)
//         {
//             var messageChannel = new MessageChannel<EmptyMessage>();
//             var subscriptions = SubscribeToChannel(messageChannel, nbSubscribers);
//
//             PublishMessages(messageChannel, nbMessages);
//             Assert.AreEqual(nbSubscribers * nbMessages, m_NbMessagesReceived);
//
//             m_NbMessagesReceived = 0;
//
//             subscriptions.Dispose();
//
//             PublishMessages(messageChannel, nbMessages);
//             Assert.AreEqual(0, m_NbMessagesReceived);
//         }
//
//         [Test]
//         [TestCase(2, 1)]
//         [TestCase(3, 2)]
//         [TestCase(5, 3)]
//         public void MessagePublishedIsReceivedByAllSubscribersAfterResubscribing(int nbSubscribers, int nbMessages)
//         {
//             var messageChannel = new MessageChannel<EmptyMessage>();
//             var subscriptions = SubscribeToChannel(messageChannel, nbSubscribers);
//
//             PublishMessages(messageChannel, nbMessages);
//             Assert.AreEqual(nbSubscribers * nbMessages, m_NbMessagesReceived);
//
//             m_NbMessagesReceived = 0;
//
//             subscriptions.Dispose();
//             subscriptions = SubscribeToChannel(messageChannel, nbSubscribers);
//
//             PublishMessages(messageChannel, nbMessages);
//             Assert.AreEqual(nbSubscribers * nbMessages, m_NbMessagesReceived);
//             subscriptions.Dispose();
//         }
// }
