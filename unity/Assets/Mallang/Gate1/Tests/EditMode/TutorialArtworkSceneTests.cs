using Mallang.Gate1.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mallang.Gate1.Tests.EditMode
{
    public sealed class TutorialArtworkSceneTests
    {
        [Test]
        public void ActualTutorialSceneUsesTheOriginalRabbitTextureAndKoreanFont()
        {
            const string assetPath = "Assets/Mallang/Gate1/Resources/lop_rabbit_atlas.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            Assert.That(texture, Is.Not.Null, "Original rabbit must import as a Texture2D.");
            Assert.That(Resources.Load<Texture2D>("lop_rabbit_atlas"), Is.EqualTo(texture));
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            Assert.That(width, Is.EqualTo(1774));
            Assert.That(height, Is.EqualTo(887));
            Assert.That(texture.width / (float)texture.height, Is.EqualTo(2f), "Android import must preserve the atlas aspect ratio.");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Mallang/Gate1/Scenes/ListenRepeatTutorial.unity");
            try
            {
                ListenRepeatTutorialScreen screen = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponentInChildren<ListenRepeatTutorialScreen>() is ListenRepeatTutorialScreen found)
                        screen = found;
                Assert.That(screen, Is.Not.Null);
                var serialized = new SerializedObject(screen);
                Assert.That(serialized.FindProperty("rabbitAtlas").objectReferenceValue, Is.EqualTo(texture),
                    "The real scene must reference the supplied atlas, not just the Resources fallback.");
                Assert.That(serialized.FindProperty("koreanFont").objectReferenceValue, Is.Not.Null);
                AssertAsset<Texture2D>(serialized, "paperBackground", "Art/Tutorial/paper_background.png");
                AssertAsset<Texture2D>(serialized, "leftInstrument", "Art/Tutorial/drum_low.png");
                AssertAsset<Texture2D>(serialized, "rightInstrument", "Art/Tutorial/wood_block_high.png");
                AssertAsset<AudioClip>(serialized, "dongSample", "Audio/Tutorial/dung_01.wav");
                AssertAsset<AudioClip>(serialized, "takSample", "Audio/Tutorial/tak_01.wav");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void AssertAsset<T>(SerializedObject screen, string property, string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>("Assets/Mallang/Gate1/" + path);
            Assert.That(asset, Is.Not.Null, path + " must import successfully.");
            Assert.That(screen.FindProperty(property).objectReferenceValue, Is.EqualTo(asset), property + " scene reference");
        }
    }
}
