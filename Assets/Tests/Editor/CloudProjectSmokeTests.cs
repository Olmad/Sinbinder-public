// Import-level smoke checks. These run inside a real Unity Editor, not the .NET shims.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Sinbinder.CloudSmoke
{
    public sealed class CloudProjectSmokeTests
    {
        private static readonly string[] CriticalScenes =
        {
            "Assets/Scenes/Prologue_Camp.unity",
            "Assets/Scenes/Prologue_Dream.unity",
            "Assets/Scenes/Tug/Field.unity",
            "Assets/Scenes/Tug/Multiplayer_Test.unity"
        };

        [Test]
        public void ExpectedEditorVersionIsUsed()
        {
            Assert.AreEqual("6000.3.22f1", Application.unityVersion,
                "Editor import must run using the version recorded by the project.");
        }

        [Test]
        public void CriticalScenesAreImportableAssets()
        {
            foreach (string path in CriticalScenes)
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),
                    "Missing or unimportable Unity scene: " + path);
        }

        [Test]
        public void EnabledBuildScenesAreUniqueAndImportable()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int enabled = 0;
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled) continue;
                enabled++;
                Assert.IsTrue(seen.Add(entry.path), "Duplicate enabled scene: " + entry.path);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path),
                    "Missing enabled scene: " + entry.path);
            }
            Assert.Greater(enabled, 0, "No scenes are enabled for the player.");
        }
    }
}
