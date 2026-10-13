using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FishGame.Tests.EditMode
{
    public class EventIsolationTests
    {
        [Test]
        public void EventIsolation_ThrowingSubscriber_OthersStillRun()
        {
            var record = new List<string>();
            Action<int, int> evt = null;
            evt += (a, b) => record.Add($"first({a},{b})");
            evt += (a, b) => throw new InvalidOperationException("isolation-boom");
            evt += (a, b) => record.Add($"third({a},{b})");

            LogAssert.Expect(LogType.Exception, new Regex("isolation-boom"));
            EventIsolation.Invoke(evt, 4, 7);

            Assert.That(record, Is.EqualTo(new[] { "first(4,7)", "third(4,7)" }));
        }

        [Test]
        public void EventIsolation_NullEvent_IsNoOp()
        {
            Assert.DoesNotThrow(() => EventIsolation.Invoke((Action)null));
            Assert.DoesNotThrow(() => EventIsolation.Invoke<int>(null, 1));
            Assert.DoesNotThrow(() => EventIsolation.Invoke<int, int>(null, 1, 2));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EventIsolation_ActionAndActionOfT_ThrowingSubscriber_OthersStillRun()
        {
            var record = new List<string>();

            Action plain = null;
            plain += () => record.Add("p1");
            plain += () => throw new InvalidOperationException("plain-boom");
            plain += () => record.Add("p3");

            Action<string> one = null;
            one += s => record.Add("a1:" + s);
            one += s => throw new InvalidOperationException("one-boom");
            one += s => record.Add("a3:" + s);

            LogAssert.Expect(LogType.Exception, new Regex("plain-boom"));
            EventIsolation.Invoke(plain);
            LogAssert.Expect(LogType.Exception, new Regex("one-boom"));
            EventIsolation.Invoke(one, "x");

            Assert.That(record, Is.EqualTo(new[] { "p1", "p3", "a1:x", "a3:x" }));
        }
    }
}
