using System;
using System.Text.RegularExpressions;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.EditMode
{
    public class EventBusTests
    {
        [Test]
        public void EventBus_DisposedSubscription_StopsReceiving()
        {
            var bus = new EventBus();
            int count = 0;
            IDisposable subscription = bus.Subscribe<int>(_ => count++);

            bus.Publish(1);
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, bus.SubscriberCount<int>());

            subscription.Dispose();
            subscription.Dispose(); // idempotent
            bus.Publish(2);

            Assert.AreEqual(1, count);
            Assert.AreEqual(0, bus.SubscriberCount<int>());
        }

        [Test]
        public void EventBus_ThrowingHandler_DoesNotBlockOthers()
        {
            var bus = new EventBus();
            int secondCalls = 0;
            bus.Subscribe<int>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<int>(_ => secondCalls++);

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            bus.Publish(7);

            Assert.AreEqual(1, secondCalls);
        }

        [Test]
        public void EventBus_Publish_DeliversOnlyToMatchingType()
        {
            var bus = new EventBus();
            int intCalls = 0;
            int toastCalls = 0;
            ToastRequested received = default;
            bus.Subscribe<int>(_ => intCalls++);
            bus.Subscribe<ToastRequested>(t => { toastCalls++; received = t; });

            bus.Publish(new ToastRequested("toast.join_code", "ABC123"));

            Assert.AreEqual(0, intCalls);
            Assert.AreEqual(1, toastCalls);
            Assert.AreEqual("toast.join_code", received.Key);
            Assert.AreEqual("ABC123", received.Arg);
            Assert.AreEqual(4f, received.Seconds, 1e-6f);

            Assert.Throws<ArgumentNullException>(() => bus.Subscribe<int>(null));

            bus.Clear();
            Assert.AreEqual(0, bus.SubscriberCount<int>());
            Assert.AreEqual(0, bus.SubscriberCount<ToastRequested>());
            bus.Publish(3);
            Assert.AreEqual(0, intCalls);
        }
    }
}
