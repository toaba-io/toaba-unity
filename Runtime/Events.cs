using System;
using UnityEngine;

namespace Toaba
{
    // Calls each handler on its own, so one that throws doesn't stop the others.
    internal static class Events
    {
        public static void Raise(Action handlers) => Each(handlers, handler => ((Action)handler)());

        public static void Raise<T>(Action<T> handlers, T value) => Each(handlers, handler => ((Action<T>)handler)(value));

        static void Each(Delegate handlers, Action<Delegate> call)
        {
            if (handlers == null)
                return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    call(handler);
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                }
            }
        }
    }
}
