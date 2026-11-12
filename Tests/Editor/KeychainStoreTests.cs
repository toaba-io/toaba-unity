#if UNITY_EDITOR_OSX
using System;
using NUnit.Framework;

namespace Toaba.Tests
{
    public class KeychainStoreTests
    {
        [Test]
        public void Values_are_kept_replaced_and_deleted()
        {
            var store = new KeychainStore("io.toaba.tests." + Guid.NewGuid().ToString("N"));
            try
            {
                store.Write("default", "first");
                store.Write("default", "second");

                Assert.That(store.Read("default"), Is.EqualTo("second"));
                Assert.That(store.Read("other"), Is.Null);
            }
            finally
            {
                store.Delete("default");
            }
            Assert.That(store.Read("default"), Is.Null);
        }
    }
}
#endif
