using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishGame.Runtime
{
    // Process-local, main thread only. Typed: Subscribe<T> receives Publish<T> with exactly the same T.
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<object>> _subscribers = new Dictionary<Type, List<object>>();

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Type key = typeof(T);
            if (!_subscribers.TryGetValue(key, out List<object> list))
            {
                list = new List<object>();
                _subscribers.Add(key, list);
            }
            var subscription = new Subscription<T>(this, handler);
            list.Add(subscription);
            return subscription;
        }

        // Iterates a snapshot, so handlers may subscribe/unsubscribe during delivery.
        // A throwing handler is logged with Debug.LogException and the remaining handlers still run.
        public void Publish<T>(T message)
        {
            if (!_subscribers.TryGetValue(typeof(T), out List<object> list)) return;
            object[] snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var subscription = (Subscription<T>)snapshot[i];
                if (!subscription.Active) continue;
                try { subscription.Handler(message); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        public int SubscriberCount<T>()
        {
            return _subscribers.TryGetValue(typeof(T), out List<object> list) ? list.Count : 0;
        }

        public void Clear()
        {
            foreach (List<object> list in _subscribers.Values)
            {
                for (int i = 0; i < list.Count; i++) ((ISubscription)list[i]).Deactivate();
            }
            _subscribers.Clear();
        }

        private void Remove(Type key, object subscription)
        {
            if (!_subscribers.TryGetValue(key, out List<object> list)) return;
            list.Remove(subscription);
            if (list.Count == 0) _subscribers.Remove(key);
        }

        private interface ISubscription
        {
            void Deactivate();
        }

        private sealed class Subscription<T> : IDisposable, ISubscription
        {
            private readonly EventBus _bus;

            public Subscription(EventBus bus, Action<T> handler)
            {
                _bus = bus;
                Handler = handler;
            }

            public Action<T> Handler { get; }
            public bool Active { get; private set; } = true;

            public void Deactivate() { Active = false; }

            public void Dispose()
            {
                if (!Active) return;
                Active = false;
                _bus.Remove(typeof(T), this);
            }
        }
    }
}
