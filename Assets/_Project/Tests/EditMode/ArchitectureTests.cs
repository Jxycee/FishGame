// Architecture scan (spec 4.9). A regression guard: it is expected to pass on its first run. Owned by T15.
// An asmdef check alone is not enough, because UnityTransport lives inside Unity.Netcode.Runtime.
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace FishGame.Tests.EditMode
{
    public class ArchitectureTests
    {
        static readonly string[] ScannedFolders = { "Gameplay", "Fishing", "Presentation", "UI" };
        static readonly string[] ForbiddenInSource = { "Unity.Services", "Unity.Netcode.Transports" };
        static readonly string[] ForbiddenAsmdefReferences = { "\"Unity.Services.", "\"Unity.Networking.Transport" };

        [Test]
        public void Architecture_GameplayFishingPresentationUi_DoNotUseServicesOrTransports()
        {
            string projectRoot = Path.Combine(Application.dataPath, "_Project");
            var violations = new List<string>();

            foreach (string folder in ScannedFolders)
            {
                string dir = Path.Combine(projectRoot, folder);
                Assert.That(Directory.Exists(dir), Is.True, "missing folder " + dir);

                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    foreach (string forbidden in ForbiddenInSource)
                        if (text.Contains(forbidden)) violations.Add(file + " mentions " + forbidden);
                }

                string[] asmdefs = Directory.GetFiles(dir, "*.asmdef", SearchOption.AllDirectories);
                Assert.That(asmdefs, Is.Not.Empty, "no .asmdef under " + dir);
                foreach (string asmdef in asmdefs)
                {
                    string text = File.ReadAllText(asmdef);
                    foreach (string forbidden in ForbiddenAsmdefReferences)
                        if (text.Contains(forbidden)) violations.Add(asmdef + " references " + forbidden.TrimStart('"') + "*");
                }
            }

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }
    }
}
