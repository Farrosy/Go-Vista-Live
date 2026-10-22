// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Tests
{
    using Coherence.Editor.Portal;
    using Coherence.Tests;
    using NUnit.Framework;

    /// <summary>
    /// Edit mode unit tests for <see cref="ProjectSelectDialog.ResolveSelectedProjectIds"/>.
    /// </summary>
    public class ProjectSelectDialogTests : CoherenceTest
    {
        [Test]
        public void ResolveSelectedProjectIds_Returns_All_Ids_When_None_Selected()
        {
            var result = ProjectSelectDialog.ResolveSelectedProjectIds(
                previouslyTickedProjectIds: System.Array.Empty<string>(),
                activeProjectIds: new[] { "current-project" },
                usingMultipleProjects: false,
                hasRuntimeSettings: true);

            Assert.That(result, Is.EqualTo(new[] { "current-project" }));
        }

        [Test]
        public void ResolveSelectedProjectIds_Returns_Previously_Selected_Ids_That_Are_Still_Valid()
        {
            var result = ProjectSelectDialog.ResolveSelectedProjectIds(
                previouslyTickedProjectIds: new[] { "project-a", "project-b" },
                activeProjectIds: new[] { "project-a" },
                usingMultipleProjects: true,
                hasRuntimeSettings: true);

            Assert.That(result, Is.EqualTo(new[] { "project-a" }));
        }

        [Test]
        public void ResolveSelectedProjectIds_Returns_All_Ids_When_No_Selected_Found()
        {
            var result = ProjectSelectDialog.ResolveSelectedProjectIds(
                previouslyTickedProjectIds: new[] { "stale-project" },
                activeProjectIds: new[] { "current-project" },
                usingMultipleProjects: false,
                hasRuntimeSettings: true);

            Assert.That(result, Is.EqualTo(new[] { "current-project" }),
                "A stale stored selection should fall back to the configured project rather than returning nothing.");
        }

        [Test]
        public void ResolveSelectedProjectIds_Keeps_Stored_Ids_When_Single_Project_Without_Runtime_Settings()
        {
            // Mirrors invocation from an AssetPostprocessor before RuntimeSettings exists: there are no
            // configured ids to filter against, so the stored selection must be preserved as-is.
            var result = ProjectSelectDialog.ResolveSelectedProjectIds(
                previouslyTickedProjectIds: new[] { "stored-project" },
                activeProjectIds: System.Array.Empty<string>(),
                usingMultipleProjects: false,
                hasRuntimeSettings: false);

            Assert.That(result, Is.EqualTo(new[] { "stored-project" }));
        }
    }
}
