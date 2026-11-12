using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Toaba.Tests
{
    public class FileStoreTests
    {
        [Test, UnityPlatform(RuntimePlatform.LinuxEditor)]
        public void Values_are_private_and_replaced_whole()
        {
            var folder = Path.Combine(Path.GetTempPath(), "toaba-" + Guid.NewGuid().ToString("N"));
            var store = new FileStore(folder);
            try
            {
                store.Write("default", "first");
                store.Write("default", "second");

                Assert.That(store.Read("default"), Is.EqualTo("second"));
                Assert.That(Directory.GetFiles(folder), Has.Length.EqualTo(1));
                Assert.That(Mode(Path.Combine(folder, "default")), Is.EqualTo("600"));
                store.Delete("default");
                Assert.That(store.Read("default"), Is.Null);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        static string Mode(string path)
        {
            var start = new ProcessStartInfo("stat", $"-c %a \"{path}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using var stat = Process.Start(start);
            var mode = stat.StandardOutput.ReadToEnd().Trim();
            stat.WaitForExit();
            return mode;
        }
    }
}
