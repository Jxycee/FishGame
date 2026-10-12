using FishGame.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class AudioBusTests
    {
        [Test]
        public void AudioBus_NotReady_WarnsOncePerId()
        {
            var go = new GameObject("AudioBusTest");
            int warnings = 0;
            void OnLog(string text, string stack, LogType type)
            {
                if (type == LogType.Warning && text.Contains("AudioBus not initialized")) warnings++;
            }
            Application.logMessageReceived += OnLog;
            try
            {
                var bus = go.AddComponent<AudioBus>();
                bus.PlayUi("x");
                bus.PlayUi("x");
                bus.PlayAt("x", Vector3.zero);
                bus.PlayUi("y");
                Assert.That(warnings, Is.EqualTo(2));
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                Object.DestroyImmediate(go);
            }
        }
    }
}
