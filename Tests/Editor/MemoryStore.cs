using System.Collections.Generic;

namespace Toaba.Tests
{
    internal sealed class MemoryStore : ISecureStore
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();

        public string Read(string key) => Values.GetValueOrDefault(key);
        public void Write(string key, string value) => Values[key] = value;
        public void Delete(string key) => Values.Remove(key);
    }
}
