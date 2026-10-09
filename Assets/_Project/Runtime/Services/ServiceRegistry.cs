using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishGame.Runtime
{
    public sealed class MissingServiceException : InvalidOperationException
    {
        public Type ServiceType { get; }

        public MissingServiceException(Type serviceType)
            : base("Service not registered: " + (serviceType == null ? "null" : serviceType.FullName))
        {
            ServiceType = serviceType;
        }
    }

    // Instance registry (not static). The key is the type argument T, so register under the interface consumers ask for.
    public sealed class ServiceRegistry : IDisposable
    {
        private readonly Dictionary<Type, object> _byType = new Dictionary<Type, object>();
        private readonly List<Type> _order = new List<Type>();

        public void Register<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            Type key = typeof(T);
            if (_byType.ContainsKey(key))
                throw new InvalidOperationException("Service already registered: " + key.FullName);
            _byType.Add(key, instance);
            _order.Add(key);
        }

        // Registers or overwrites. A replaced instance is not disposed by the registry; its creator still owns it.
        public void Replace<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            Type key = typeof(T);
            if (!_byType.ContainsKey(key)) _order.Add(key);
            _byType[key] = instance;
        }

        public T Get<T>() where T : class
        {
            if (_byType.TryGetValue(typeof(T), out object found)) return (T)found;
            throw new MissingServiceException(typeof(T));
        }

        public bool TryGet<T>(out T instance) where T : class
        {
            if (_byType.TryGetValue(typeof(T), out object found))
            {
                instance = (T)found;
                return true;
            }
            instance = null;
            return false;
        }

        public bool Contains<T>() where T : class
        {
            return _byType.ContainsKey(typeof(T));
        }

        // Disposes each distinct registered IDisposable once, in reverse registration order, then clears. Idempotent.
        public void Dispose()
        {
            var disposed = new List<object>();
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                var disposable = _byType[_order[i]] as IDisposable;
                if (disposable == null || ContainsReference(disposed, disposable)) continue;
                disposed.Add(disposable);
                try { disposable.Dispose(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            _byType.Clear();
            _order.Clear();
        }

        private static bool ContainsReference(List<object> items, object target)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (ReferenceEquals(items[i], target)) return true;
            }
            return false;
        }
    }
}
