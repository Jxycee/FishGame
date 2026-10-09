using System;
using System.Collections.Generic;
using FishGame.Runtime;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ServiceRegistryTests
    {
        private interface IGreeter { string Greet(); }
        private sealed class EnglishGreeter : IGreeter { public string Greet() => "hi"; }
        private sealed class FrenchGreeter : IGreeter { public string Greet() => "salut"; }

        private class RecordingDisposable : IDisposable
        {
            private readonly string _name;
            private readonly List<string> _log;
            public RecordingDisposable(string name, List<string> log) { _name = name; _log = log; }
            public void Dispose() { _log.Add(_name); }
        }

        private sealed class DisposableA : RecordingDisposable
        {
            public DisposableA(string name, List<string> log) : base(name, log) { }
        }

        private sealed class DisposableB : RecordingDisposable
        {
            public DisposableB(string name, List<string> log) : base(name, log) { }
        }

        [Test]
        public void ServiceRegistry_MissingService_Throws()
        {
            var registry = new ServiceRegistry();
            var ex = Assert.Throws<MissingServiceException>(() => registry.Get<IGreeter>());
            Assert.AreEqual(typeof(IGreeter), ex.ServiceType);
            StringAssert.StartsWith("Service not registered: ", ex.Message);
            Assert.IsInstanceOf<InvalidOperationException>(ex);
            Assert.IsFalse(registry.Contains<IGreeter>());
            Assert.IsFalse(registry.TryGet<IGreeter>(out IGreeter none));
            Assert.IsNull(none);
            registry.Dispose();
        }

        [Test]
        public void ServiceRegistry_DuplicateRegister_Throws()
        {
            var registry = new ServiceRegistry();
            registry.Register<IGreeter>(new EnglishGreeter());
            Assert.Throws<InvalidOperationException>(() => registry.Register<IGreeter>(new FrenchGreeter()));
            Assert.AreEqual("hi", registry.Get<IGreeter>().Greet());
            Assert.Throws<ArgumentNullException>(() => registry.Register<FrenchGreeter>(null));
            registry.Dispose();
        }

        [Test]
        public void ServiceRegistry_Replace_Overwrites()
        {
            var registry = new ServiceRegistry();
            registry.Replace<IGreeter>(new EnglishGreeter()); // Replace on an empty key registers
            Assert.IsTrue(registry.Contains<IGreeter>());
            registry.Replace<IGreeter>(new FrenchGreeter());
            Assert.AreEqual("salut", registry.Get<IGreeter>().Greet());
            Assert.IsTrue(registry.TryGet<IGreeter>(out IGreeter found));
            Assert.AreEqual("salut", found.Greet());
            registry.Dispose();
        }

        [Test]
        public void ServiceRegistry_Dispose_DisposesInReverseOrderOnce()
        {
            var log = new List<string>();
            var registry = new ServiceRegistry();
            var a = new DisposableA("a", log);
            var b = new DisposableB("b", log);
            registry.Register<DisposableA>(a);  // order 0
            registry.Register<DisposableB>(b);  // order 1
            registry.Register<IDisposable>(b);  // order 2: same instance under a second key, disposed once

            registry.Dispose();
            CollectionAssert.AreEqual(new[] { "b", "a" }, log);
            Assert.IsFalse(registry.Contains<DisposableA>());
            Assert.IsFalse(registry.Contains<IDisposable>());

            registry.Dispose(); // idempotent
            CollectionAssert.AreEqual(new[] { "b", "a" }, log);
        }
    }
}
