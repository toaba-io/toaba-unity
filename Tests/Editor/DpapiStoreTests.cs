#if UNITY_EDITOR_WIN
using NUnit.Framework;

namespace Toaba.Tests
{
    public class DpapiStoreTests
    {
        [Test]
        public void Values_are_saved_encrypted_and_read_back()
        {
            var inner = new MemoryStore();
            var store = new DpapiStore(inner);

            store.Write("default", "{\"refresh_token\":\"rt_1\"}");

            Assert.That(inner.Values["default"], Does.Not.Contain("rt_1"));
            Assert.That(store.Read("default"), Is.EqualTo("{\"refresh_token\":\"rt_1\"}"));
        }
    }
}
#endif
