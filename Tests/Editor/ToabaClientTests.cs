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

        [Test]
        public void Headers_name_the_package_version_and_keep_one_install_id()
        {
            var first = ClientContext.Headers();
            var second = ClientContext.Headers();

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ToabaClient).Assembly);
            Assert.That(Sdk.Version, Is.EqualTo(package.version));
            Assert.That(first["x-toaba-sdk"], Is.EqualTo("toaba-unity/" + package.version));
            Assert.That(second["x-toaba-install-id"], Is.EqualTo(first["x-toaba-install-id"]));
        }
    }
}
