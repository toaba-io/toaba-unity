using System;
using NUnit.Framework;

namespace Toaba.Tests
{
    public class ToabaClientTests
    {
        [Test]
        public void Without_settings_it_says_how_to_set_up_Toaba()
        {
            var error = Assert.Throws<InvalidOperationException>(() => _ = ToabaClient.Shared);

            Assert.That(error.Message, Does.Contain("Project Settings → Toaba"));
        }
    }
}
