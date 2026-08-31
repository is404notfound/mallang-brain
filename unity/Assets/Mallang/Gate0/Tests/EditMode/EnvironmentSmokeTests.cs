using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Mallang.Gate0.Tests.EditMode
{
    public sealed class EnvironmentSmokeTests
    {
        [Test]
        public void EditorUsesPinnedUnityVersion()
        {
            StringAssert.StartsWith("6000.3", Application.unityVersion);
        }

        [Test]
        public void ProjectLivesInExpectedUnityDirectory()
        {
            var projectDirectory = Directory.GetParent(Application.dataPath);

            Assert.That(projectDirectory, Is.Not.Null);
            Assert.That(projectDirectory!.Name, Is.EqualTo("unity"));
        }
    }
}
