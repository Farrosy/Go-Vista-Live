// Copyright (c) coherence ApS.
// See the license file in the package root for more information.
namespace Coherence.Editor.Tests
{
    using System;
    using System.Threading.Tasks;
    using Coherence.Tests;
    using NUnit.Framework;
    using Editor.Portal;

    /// <summary>
    /// Tests for uploading schemas to the Online Dashboard.
    /// </summary>
    public sealed class SchemasUploadOperationTests : CoherenceTest
    {
        /// <summary>
        /// A string that is neither null nor empty.
        /// </summary>
        private const string NonEmptyString = " ";

        private static readonly FakeEditorStatus NotCompiling = new(isCompiling: false);

        /// <summary>
        /// When the tests start running, the value of <see cref="PortalLogin.OrganizationsFetched"/> is cached here,
        /// and once all the tests have finished running, the value is restored back to it original state
        /// (to avoid these test having side effects that could break other tests etc.).
        /// </summary>
        private Organization[] portalLoginOrganizationsPreviousValue;

        [OneTimeSetUp]
        public override void OneTimeSetUp()
        {
            base.OneTimeSetUp();
            portalLoginOrganizationsPreviousValue = PortalLogin.OrganizationsFetched ? PortalLogin.organizations : null;
        }

        public override void SetUp()
        {
            base.SetUp();
            PortalLogin.DiscardFetchedOrganizations();
        }

        [OneTimeTearDown]
        public override void OneTimeTearDown()
        {
            if (portalLoginOrganizationsPreviousValue is null)
            {
                PortalLogin.DiscardFetchedOrganizations();
            }
            else
            {
                PortalLogin.SetOrganizations(portalLoginOrganizationsPreviousValue);
                portalLoginOrganizationsPreviousValue = null;
            }

            base.OneTimeTearDown();
        }

        [Test]
        public void Upload_ShouldFail_WithReason_MissingSchemaID_IfSchemaIDIsNull()
        {
            var schemas = new Schemas(new Schema[] { }, null);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, NonEmptyString, NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingSchemaID;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldFail_WithReason_MissingSchemaID_IfSchemaIDIsEmpty()
        {
            var schemas = new Schemas(new Schema[] { }, "");
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, NonEmptyString, NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingSchemaID;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldNotFail_WithReason_MissingPortalAndLoginTokens_IfPortalTokenIsNull()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, null, NonEmptyString, NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingPortalAndLoginTokens;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.Not.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldNotFail_WithReason_MissingPortalAndLoginTokens_IfPortalTokenIsEmpty()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, "", NonEmptyString, NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingPortalAndLoginTokens;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.Not.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldNotFail_WithReason_MissingPortalAndLoginTokens_IfLoginTokenIsNull()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, null, NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingPortalAndLoginTokens;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.Not.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldNotFail_WithReason_MissingPortalAndLoginTokens_IfLoginTokenIsEmpty()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, null, NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingPortalAndLoginTokens;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.Not.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldFail_WithReason_MissingPortalAndLoginTokens_IfPortalTokenIsNull_And_LoginTokenIsNull()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, null, null, NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingPortalAndLoginTokens;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldFail_WithReason_MissingPortalAndLoginTokens_IfPortalTokenIsEmpty_And_LoginTokenIsEmpty()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, "", "", NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingPortalAndLoginTokens;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldFail_WithReason_MissingPortalAndLoginTokens_IfPortalTokenIsNull_And_LoginTokenIsEmpty()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, null, "", NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingPortalAndLoginTokens;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldFail_WithReason_MissingPortalAndLoginTokens_IfPortalTokenIsEmpty_And_LoginTokenIsNull()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, "", null, NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingPortalAndLoginTokens;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldFail_WithReason_MissingOrganizationID_IfOrganizationIDIsNull()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, NonEmptyString, null, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingOrganizationID;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldFail_WithReason_MissingOrganizationID_IfOrganizationIDIsEmpty()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, NonEmptyString, "", project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingOrganizationID;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldFail_WithReason_MissingProjectID_IfProjectIDIsNull()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = null, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, NonEmptyString, NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingProjectID;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldFail_WithReason_MissingProjectID_IfProjectIDIsEmpty()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = "", name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, NonEmptyString, NonEmptyString, project, NotCompiling);
            var expected = SchemasUploadOperation.FailReason.MissingProjectID;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldFail_WithReason_InvalidOrganizationID_IfOrganizationIdNotFoundAmongFetchedOrganizations()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, NonEmptyString, NonEmptyString, project, NotCompiling);
            PortalLogin.SetOrganizations(Array.Empty<Organization>());
            var expected = SchemasUploadOperation.FailReason.InvalidOrganizationID;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Upload_ShouldNotFail_WithReason_InvalidOrganizationID_IfOrganizationsHaveNotBeenFetched()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name =  NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, NonEmptyString, NonEmptyString, project, NotCompiling);
            PortalLogin.DiscardFetchedOrganizations();
            var expected = SchemasUploadOperation.FailReason.InvalidOrganizationID;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.Not.EqualTo(expected));
        }

        [Test]
        public void Upload_Should_Fail_With_Reason_CompilationInProgress_If_Editor_Is_Compiling()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name = NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var editorState = new FakeEditorStatus(isCompiling: true);
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, NonEmptyString, NonEmptyString, project, editorState);
            var expected = SchemasUploadOperation.FailReason.CompilationInProgress;

            var actual = uploadOperation.Upload().FailReason;

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public async Task UploadAsync_Should_Fail_With_Reason_CompilationInProgress_If_Editor_Is_Compiling()
        {
            var schemas = new Schemas(new Schema[] { }, NonEmptyString);
            var project = new ProjectInfo { id = NonEmptyString, name = NonEmptyString, runtime_key = NonEmptyString, portal_token = NonEmptyString };
            var editorState = new FakeEditorStatus(isCompiling: true);
            var uploadOperation = new SchemasUploadOperation(schemas, NonEmptyString, NonEmptyString, NonEmptyString, project, editorState);
            var expected = SchemasUploadOperation.FailReason.CompilationInProgress;

            var result = await uploadOperation.UploadAsync();

            Assert.That(result.FailReason, Is.EqualTo(expected));
        }

        private sealed class FakeEditorStatus : EditorStatus
        {
            public FakeEditorStatus(bool isCompiling) => IsCompiling = isCompiling;

            public override bool IsCompiling { get; }
        }
    }
}
