// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using System.Threading.Tasks;
    using Coherence.Tests;
    using NUnit.Framework;
    using Editor.Portal;
    using UnityEngine;

    /// <summary>
    /// Edit mode tests for <see cref="PortalLogin"/>.
    /// </summary>
    public class PortalLoginTests : CoherenceTest
    {
        private const string ProjectId = "cke7octklouml7ml5lrg";

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public async Task FetchOrgs_Multiple_Successive_Executions_Return_Same_Result(bool firstCallbackIsNull, bool secondCallbackIsNull)
        {
            var firstCallbackInvokedTimes = 0;
            OrganizationList firstCallbackResult = null;
            PortalLogin.FetchOrgs(firstCallbackIsNull ? null : list =>
            {
                firstCallbackInvokedTimes++;
                firstCallbackResult = list;
            });

            var secondCallbackInvokedTimes = 0;
            OrganizationList secondCallbackResult = null;
            PortalLogin.FetchOrgs(secondCallbackIsNull ? null : list =>
            {
                secondCallbackInvokedTimes++;
                secondCallbackResult = list;
            });

            while (firstCallbackInvokedTimes is 0 && secondCallbackInvokedTimes is 0)
            {
                await Task.Yield();
            }

            Assert.That(firstCallbackInvokedTimes, Is.EqualTo(firstCallbackIsNull ? 0 : 1));
            Assert.That(firstCallbackResult, firstCallbackIsNull ? Is.Null : Is.Not.Null);
            Assert.That(secondCallbackInvokedTimes, Is.EqualTo(secondCallbackIsNull ? 0 : 1));
            Assert.That(secondCallbackResult, secondCallbackIsNull ? Is.Null : Is.Not.Null);
            if (firstCallbackIsNull == secondCallbackIsNull)
            {
                Assert.That(firstCallbackResult, Is.EqualTo(secondCallbackResult));
            }
        }

        /// <summary>
        /// Backup of <see cref="RuntimeSettings"/> state, captured before each test and restored afterwards
        /// so that mutating the singleton's project name/id during a test has no lasting side effects.
        /// </summary>
        private string runtimeSettingsBackup;

        public override void SetUp()
        {
            base.SetUp();
            runtimeSettingsBackup = JsonUtility.ToJson(RuntimeSettings.Instance);
        }

        public override void TearDown()
        {
            if (runtimeSettingsBackup != null)
            {
                JsonUtility.FromJsonOverwrite(runtimeSettingsBackup, RuntimeSettings.Instance);
                runtimeSettingsBackup = null;
            }

            base.TearDown();
        }

        /// <summary>
        /// Sets the serialized project id and name on <paramref name="settings"/> without relying on
        /// the (assembly-)internal setters, which are not visible to this test assembly.
        /// </summary>
        private static void SetProjectInfo(RuntimeSettings settings, string id, string name)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new SerializedProjectInfo(id, name)), settings);
        }

        [System.Serializable]
        private struct SerializedProjectInfo
        {
            public string projectID;
            public string projectName;

            public SerializedProjectInfo(string id, string name)
            {
                projectID = id;
                projectName = name;
            }
        }

        [Test]
        public void UpdateProject_Updates_RuntimeSettings_ProjectName_When_Id_Matches()
        {
            var runtimeSettings = RuntimeSettings.Instance;
            SetProjectInfo(runtimeSettings, ProjectId, "Old Name");
            const string newName = "New Name";
            var project = new ProjectInfo { id = ProjectId, name = newName };

            var changed = PortalLogin.UpdateProject(project, updateProjectSettings: false, updateSlugStore: false);

            Assert.That(changed, Is.True);
            Assert.That(runtimeSettings.ProjectName, Is.EqualTo(newName));
        }

        [Test]
        public void UpdateProject_Does_Not_Update_RuntimeSettings_ProjectName_When_Name_Is_Unchanged()
        {
            var runtimeSettings = RuntimeSettings.Instance;
            const string sameName = "Same Name";
            SetProjectInfo(runtimeSettings, ProjectId, sameName);
            var project = new ProjectInfo { id = ProjectId, name = sameName };

            var changed = PortalLogin.UpdateProject(project, updateProjectSettings: false, updateSlugStore: false);

            Assert.That(changed, Is.False);
            Assert.That(runtimeSettings.ProjectName, Is.EqualTo(sameName));
        }

        [Test]
        public void UpdateProject_Does_Not_Update_RuntimeSettings_ProjectName_When_Id_Differs()
        {
            var runtimeSettings = RuntimeSettings.Instance;
            const string oldName = "Old Name";
            SetProjectInfo(runtimeSettings, ProjectId, oldName);
            var otherProject = new ProjectInfo { id = "different-id", name = "Different Name" };

            var changed = PortalLogin.UpdateProject(otherProject, updateProjectSettings: false, updateSlugStore: false);

            Assert.That(changed, Is.False);
            Assert.That(runtimeSettings.ProjectName, Is.EqualTo(oldName));
        }

        [Test]
        public async Task FetchOrganizationsAsync_Multiple_Successive_Executions_Return_Same_Result()
        {
            var firstCallbackResult = await PortalLogin.FetchOrganizationsAsync();
            var secondCallbackResult = await PortalLogin.FetchOrganizationsAsync();
            Assert.That(firstCallbackResult, Is.Not.Null);
            Assert.That(secondCallbackResult, Is.Not.Null);
            Assert.That(firstCallbackResult, Is.EqualTo(secondCallbackResult));
        }
    }
}
