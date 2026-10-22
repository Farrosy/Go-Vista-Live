// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Coherence.Utils;
    using Common;
    using Newtonsoft.Json;
    using NUnit.Framework;

    /// <summary>
    /// Edit Mode unit tests for <see cref="ReportsService"/>.
    /// </summary>
    public class ReportsServiceTests : CoherenceTest
    {
        private MockAuthClientBuilder authClientBuilder;
        private MockRequestFactoryBuilder requestFactoryBuilder;
        private MockRuntimeSettingsBuilder runtimeSettingsBuilder;

        private CloudCredentialsPair CloudCredentialsPair => new(AuthClient, RequestFactory);
        private IAuthClientInternal AuthClient => authClientBuilder.AuthClient;
        private IRequestFactoryInternal RequestFactory => requestFactoryBuilder.RequestFactory;
        private IRuntimeSettings RuntimeSettings => runtimeSettingsBuilder.RuntimeSettings;

        public override void SetUp()
        {
            base.SetUp();
            authClientBuilder = new();
            requestFactoryBuilder = new();
            runtimeSettingsBuilder = new();
        }

        [Test]
        public async Task SubmitReportAsync_Serializes_The_Request_Body_With_Expected_Fields()
        {
            const string reportId = "rep-abc-123";
            requestFactoryBuilder.SendRequestAsyncReturns(CoherenceJson.SerializeObject(new { report_id = reportId }));
            using var reportsService = CreateReportsService();

            var result = await reportsService.SubmitReportAsync(
                target: "target-id",
                category: ReportCategory.Cheating,
                notes: "saw them flying");

            Assert.That((string)result, Is.EqualTo(reportId));

            Assert.That(requestFactoryBuilder.SendRequestAsyncWasCalled, Is.True);
            var call = requestFactoryBuilder.SendRequestAsyncWasCalledWith;
            Assert.That(call.basePath, Is.EqualTo(ReportsService.reportsSubmitEndpoint));
            Assert.That(call.method, Is.EqualTo("POST"));

            // Round-trip through the wire DTO so the test isn't tied to JSON formatting choices.
            var sent = CoherenceJson.DeserializeObject<SubmitReportRequest>(call.body);
            Assert.That(sent.Target, Is.EqualTo("target-id"));
            Assert.That(sent.Category, Is.EqualTo(ReportCategory.Cheating));
            Assert.That(sent.Notes, Is.EqualTo("saw them flying"));
        }

        [TestCase(ReportCategory.Harassment, "harassment")]
        [TestCase(ReportCategory.HateSpeech, "hate_speech")]
        [TestCase(ReportCategory.Cheating, "cheating")]
        [TestCase(ReportCategory.Spam, "spam")]
        [TestCase(ReportCategory.InappropriateContent, "inappropriate_content")]
        [TestCase(ReportCategory.Impersonation, "impersonation")]
        [TestCase(ReportCategory.UnderageAccount, "underage_account")]
        [TestCase(ReportCategory.Other, "other")]
        public async Task SubmitReportAsync_Serializes_Each_Category_As_Snake_Case(ReportCategory category, string expectedWireValue)
        {
            requestFactoryBuilder.SendRequestAsyncReturns(CoherenceJson.SerializeObject(new { report_id = "x" }));
            using var reportsService = CreateReportsService();

            await reportsService.SubmitReportAsync(target: "target", category: category);

            var sent = CoherenceJson.DeserializeObject<DecodedRequest>(requestFactoryBuilder.SendRequestAsyncWasCalledWith.body);
            Assert.That(sent.Category, Is.EqualTo(expectedWireValue));
        }

        [Test]
        public async Task SubmitReportAsync_Omits_Notes_When_Null_Or_Empty()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(CoherenceJson.SerializeObject(new { report_id = "x" }));
            using var reportsService = CreateReportsService();

            await reportsService.SubmitReportAsync(
                target: "target",
                category: ReportCategory.Spam,
                notes: null);

            var bodyWithoutNotes = requestFactoryBuilder.SendRequestAsyncWasCalledWith.body;
            Assert.That(bodyWithoutNotes, Does.Not.Contain("\"notes\""), "null notes should not appear on the wire");

            // Reset and try with empty string — also should be omitted.
            requestFactoryBuilder = new MockRequestFactoryBuilder();
            requestFactoryBuilder.SendRequestAsyncReturns(CoherenceJson.SerializeObject(new { report_id = "x" }));
            using var reportsService2 = CreateReportsService();
            await reportsService2.SubmitReportAsync(
                target: "target",
                category: ReportCategory.Spam,
                notes: "");
            Assert.That(requestFactoryBuilder.SendRequestAsyncWasCalledWith.body, Does.Not.Contain("\"notes\""),
                "empty notes should not appear on the wire");
        }

        [Test]
        public async Task SubmitReportAsync_CanBeCanceled_While_Waiting_For_Throttle()
        {
            var cooldownCompletionSource = new TaskCompletionSource<bool>();
            using var reportsService = CreateReportsService(cooldownCompletionSource);
            using var cts = new CancellationTokenSource();

            var task = reportsService.SubmitReportAsync(
                target: new PlayerAccountId("target"),
                category: ReportCategory.Harassment,
                notes: null,
                cancellationToken: cts.Token);

            await Task.Yield();
            Assert.That(task.IsCompleted, Is.False);
            cts.Cancel();

            try { await task; }
            catch (OperationCanceledException) { return; }

            Assert.Fail("Expected OperationCanceledException was not thrown.");
        }

        [Test]
        public async Task SubmitReportAsync_Propagates_RequestException_When_Backend_Rejects()
        {
            requestFactoryBuilder.OnSendRequestAsyncCalled(new RequestException(ErrorCode.ReportsTargetCooldown, 429, "cooldown"));
            using var reportsService = CreateReportsService();

            try
            {
                await reportsService.SubmitReportAsync(target: "target", category: ReportCategory.Harassment);
            }
            catch (RequestException exception)
            {
                Assert.That(exception.ErrorCode, Is.EqualTo(ErrorCode.ReportsTargetCooldown));
                return;
            }

            Assert.Fail("Expected RequestException was not thrown.");
        }

        [Test]
        public async Task SubmitReportAsync_Throws_ResponseDeserializationException_On_Malformed_Response()
        {
            requestFactoryBuilder.SendRequestAsyncReturns("{ this is not json");
            using var reportsService = CreateReportsService();

            try
            {
                await reportsService.SubmitReportAsync(target: "target", category: ReportCategory.Other);
            }
            catch (ResponseDeserializationException exception)
            {
                Assert.That(exception.ErrorCode, Is.EqualTo(Result.InvalidResponse));
                return;
            }

            Assert.Fail("Expected ResponseDeserializationException was not thrown.");
        }

        [Test]
        public async Task SubmitReport_Callback_Variant_Returns_Deserialized_ReportId_On_Success()
        {
            const string reportId = "rep-callback-1";
            requestFactoryBuilder.SendRequestAsyncReturns(CoherenceJson.SerializeObject(new { report_id = reportId }));
            using var reportsService = CreateReportsService();
            RequestResponse<ReportId>? response = null;

            reportsService.SubmitReport
            (
                target: "target",
                category: ReportCategory.Other,
                notes: "test",
                onRequestFinished: x => response = x
            );

            await Task.Yield();

            Assert.That(response, Is.Not.Null, "SubmitReport onRequestFinished callback was not invoked.");
            Assert.That(response.Value.Status, Is.EqualTo(RequestStatus.Success));
            Assert.That((string)response.Value.Result, Is.EqualTo(reportId));
        }

        [Test]
        public async Task SubmitReport_Callback_Variant_Reports_Failure_When_Backend_Rejects()
        {
            requestFactoryBuilder.OnSendRequestAsyncCalled(new RequestException(ErrorCode.TooManyRequests, 429, "daily cap"));
            using var reportsService = CreateReportsService();
            RequestResponse<ReportId>? response = null;

            reportsService.SubmitReport
            (
                target: "target",
                category: ReportCategory.Spam,
                onRequestFinished: x => response = x
            );

            await Task.Yield();

            Assert.That(response, Is.Not.Null, "SubmitReport onRequestFinished callback was not invoked.");
            Assert.That(response.Value.Status, Is.EqualTo(RequestStatus.Fail));
            Assert.That(response.Value.Exception, Is.Not.Null, "A failed submit should surface the backend exception.");
        }

        private ReportsService CreateReportsService(TaskCompletionSource<bool> cooldownCompletionSource = null)
        {
            if (cooldownCompletionSource is null)
            {
                cooldownCompletionSource = new();
                cooldownCompletionSource.SetResult(true);
            }

            return new
            (
                CloudCredentialsPair,
                RuntimeSettings,
                playerAccountProvider: null,
                throttle: new FakeRequestThrottle(cooldownCompletionSource), logger: logger
            );
        }

        // Mirror of the wire-level body so tests can assert on field names without leaking the
        // internal DTO out of Coherence.Runtime.
        private sealed class DecodedRequest
        {
            [JsonProperty("target")]    public string Target;
            [JsonProperty("category")]  public string Category;
            [JsonProperty("notes")]     public string Notes;
        }
    }
}
