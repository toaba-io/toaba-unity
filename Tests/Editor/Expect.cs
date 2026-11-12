using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Toaba.Tests
{
    // NUnit's Assert.ThrowsAsync blocks the main thread, which UnityWebRequest needs.
    internal static class Expect
    {
        public static async Task<T> Throws<T>(Func<Task> call) where T : Exception
        {
            try
            {
                await call();
            }
            catch (T expected)
            {
                return expected;
            }
            Assert.Fail($"Expected {typeof(T).Name}");
            return null;
        }
    }
}
