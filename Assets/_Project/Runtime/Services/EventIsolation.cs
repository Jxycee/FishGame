using System;
using UnityEngine;

namespace FishGame.Runtime
{
    // Authority: none (local helper). Main thread only.
    // Raises a C# event so that each subscriber runs in its own try/catch: a throwing subscriber is logged with
    // Debug.LogException and the remaining subscribers still run, in subscription order. GetInvocationList() takes a
    // snapshot exactly like a multicast invoke, so subscribe/unsubscribe during the raise behaves as before.
    // Allocates one array per raise: fine for session events, not for per-frame use.
    public static class EventIsolation
    {
        public static void Invoke(Action evt)
        {
            if (evt == null) return;
            Delegate[] handlers = evt.GetInvocationList();
            for (int i = 0; i < handlers.Length; i++)
            {
                try { ((Action)handlers[i])(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static void Invoke<T>(Action<T> evt, T arg)
        {
            if (evt == null) return;
            Delegate[] handlers = evt.GetInvocationList();
            for (int i = 0; i < handlers.Length; i++)
            {
                try { ((Action<T>)handlers[i])(arg); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static void Invoke<T1, T2>(Action<T1, T2> evt, T1 a, T2 b)
        {
            if (evt == null) return;
            Delegate[] handlers = evt.GetInvocationList();
            for (int i = 0; i < handlers.Length; i++)
            {
                try { ((Action<T1, T2>)handlers[i])(a, b); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
