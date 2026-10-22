// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.PingClient.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Cloud;
    using NUnit.Framework;

    public class PingClientTests
    {
        [Test]
        public async Task PingAsync_RegionOverload_WithNullPingServers_FiltersThemOut()
        {
            var regions = new[]
            {
                new Region("us-east", null),
                new Region("eu-west", new PingServer { Region = "eu-west", Ip = "127.0.0.1", Port = 8080 }),
                new Region("ap-south", null)
            };

            var results = await PingClient.PingAsync(regions);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Region, Is.EqualTo("eu-west"));
        }

        [Test]
        public async Task PingAsync_RegionOverload_WithAllNullPingServers_ReturnsEmpty()
        {
            var regions = new[]
            {
                new Region("us-east", null),
                new Region("eu-west", null)
            };

            var results = await PingClient.PingAsync(regions);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task PingAsync_RegionOverload_WithAllValidPingServers_PingsAll()
        {
            var regions = new[]
            {
                new Region("us-east", new PingServer { Region = "us-east", Ip = "127.0.0.1", Port = 8080 }),
                new Region("eu-west", new PingServer { Region = "eu-west", Ip = "127.0.0.1", Port = 8081 })
            };

            var results = await PingClient.PingAsync(regions);

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].Region, Is.EqualTo("us-east"));
            Assert.That(results[1].Region, Is.EqualTo("eu-west"));
        }

        // ── PingResult struct ────────────────────────────────────────────────────

        [Test]
        [Description("Verifies that Success is true when Error is null, matching how callers check for a "
            + "successful ping without inspecting the error string directly.")]
        public void PingResult_Success_IsTrueWhenErrorIsNull()
        {
            var result = new PingResult { Region = "eu-west", RoundTripMs = 42, Error = null };
            Assert.That(result.Success, Is.True);
        }

        [Test]
        [Description("Verifies that Success is true when Error is an empty string, since the property uses "
            + "string.IsNullOrEmpty so both null and empty count as 'no error'.")]
        public void PingResult_Success_IsTrueWhenErrorIsEmpty()
        {
            var result = new PingResult { Region = "eu-west", RoundTripMs = 42, Error = string.Empty };
            Assert.That(result.Success, Is.True);
        }

        [Test]
        [Description("Verifies that Success is false when Error contains a message, so callers can branch "
            + "on Success without also checking the Error string.")]
        public void PingResult_Success_IsFalseWhenErrorIsSet()
        {
            var result = new PingResult { Region = "eu-west", Error = "Response timeout" };
            Assert.That(result.Success, Is.False);
        }

        [Test]
        [Description("Verifies the ToString format for a successful result: 'Protocol:Region:Xms'.")]
        [TestCase(PingProtocol.UDP, "us-east", 35L, "UDP:us-east:35ms")]
        [TestCase(PingProtocol.TCP, "eu-west", 120L, "TCP:eu-west:120ms")]
        public void PingResult_ToString_SuccessFormat(PingProtocol protocol, string region, long roundTripMs, string expected)
        {
            var result = new PingResult { Protocol = protocol, Region = region, RoundTripMs = roundTripMs, Error = null };
            Assert.That(result.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [Description("Verifies the ToString format for a failed result: 'Protocol:Region:ErrorMessage'.")]
        [TestCase(PingProtocol.UDP, "us-east", "Cancelled", "UDP:us-east:Cancelled")]
        [TestCase(PingProtocol.TCP, "eu-west", "Response timeout", "TCP:eu-west:Response timeout")]
        public void PingResult_ToString_FailureFormat(PingProtocol protocol, string region, string error, string expected)
        {
            var result = new PingResult { Protocol = protocol, Region = region, Error = error };
            Assert.That(result.ToString(), Is.EqualTo(expected));
        }

        // ── Argument validation ──────────────────────────────────────────────────

        [Test]
        [Description("Verifies that passing null for the regions array throws ArgumentNullException immediately, "
            + "before any async work begins.")]
        public async Task PingAsync_RegionOverload_NullArgument_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = null;
            try
            {
                await PingClient.PingAsync((Region[])null);
            }
            catch (ArgumentNullException ex)
            {
                exception = ex;
            }
            Assert.That(exception, Is.Not.Null);
        }

        [Test]
        [Description("Verifies that passing null for the PingServer array throws ArgumentNullException.")]
        public async Task PingAsync_ServerArrayOverload_NullArgument_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = null;
            try
            {
                await PingClient.PingAsync((PingServer[])null);
            }
            catch (ArgumentNullException ex)
            {
                exception = ex;
            }
            Assert.That(exception, Is.Not.Null);
        }

        [Test]
        [Description("Verifies that passing null for the IEnumerable<PingServer> overload throws ArgumentNullException.")]
        public async Task PingAsync_EnumerableOverload_NullArgument_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = null;
            try
            {
                await PingClient.PingAsync((System.Collections.Generic.IEnumerable<PingServer>)null);
            }
            catch (ArgumentNullException ex)
            {
                exception = ex;
            }
            Assert.That(exception, Is.Not.Null);
        }

        // ── Fast-path: empty server list ─────────────────────────────────────────

        [Test]
        [Description("Verifies that an empty server array returns an empty result list synchronously, "
            + "without attempting any network I/O.")]
        public async Task PingAsync_EmptyServerArray_ReturnsEmpty()
        {
            var results = await PingClient.PingAsync(Array.Empty<PingServer>());

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.EqualTo(0));
        }

        // ── Fast-path: pre-cancelled token ───────────────────────────────────────

        [Test]
        [Description("Verifies that a pre-cancelled CancellationToken causes PingAsync to return results "
            + "immediately with Error='Cancelled' for every server, without attempting network I/O.")]
        [TestCase(PingProtocol.UDP)]
        [TestCase(PingProtocol.TCP)]
        public async Task PingAsync_PreCancelledToken_ReturnsCancelledResultsForEachServer(PingProtocol protocol)
        {
            var servers = new[]
            {
                new PingServer { Region = "us-east", Ip = "127.0.0.1", Port = 8080 },
                new PingServer { Region = "eu-west", Ip = "127.0.0.1", Port = 8081 },
            };

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            var results = await PingClient.PingAsync(servers, protocol, cancellationToken: cancellationTokenSource.Token);

            Assert.That(results.Count, Is.EqualTo(2));
            foreach (var result in results)
            {
                Assert.That(result.Success, Is.False, $"Expected failure for {result.Region}");
                Assert.That(result.Error, Is.EqualTo("Cancelled"), $"Wrong error for {result.Region}");
                Assert.That(result.Protocol, Is.EqualTo(protocol));
            }
        }

        // ── Server validation ────────────────────────────────────────────────────

        [Test]
        [Description("Verifies that a server with an empty IP produces an error result rather than throwing, "
            + "so one bad server does not abort the entire ping batch.")]
        public async Task PingAsync_ServerWithEmptyIp_ReturnsErrorResult()
        {
            var servers = new[]
            {
                new PingServer { Region = "us-east", Ip = "", Port = 8080 },
            };

            var results = await PingClient.PingAsync(servers);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Success, Is.False);
            Assert.That(results[0].Error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        [Description("Verifies that a server with port <= 0 produces an error result rather than throwing.")]
        public async Task PingAsync_ServerWithInvalidPort_ReturnsErrorResult()
        {
            var servers = new[]
            {
                new PingServer { Region = "eu-west", Ip = "127.0.0.1", Port = 0 },
            };

            var results = await PingClient.PingAsync(servers);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Success, Is.False);
            Assert.That(results[0].Error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        [Description("Verifies that a server with a non-parseable IP string produces an error result.")]
        public async Task PingAsync_ServerWithMalformedIp_ReturnsErrorResult()
        {
            var servers = new[]
            {
                new PingServer { Region = "ap-south", Ip = "not-an-ip", Port = 8080 },
            };

            var results = await PingClient.PingAsync(servers);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Success, Is.False);
            Assert.That(results[0].Error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        [Description("Verifies that a mixed batch of valid and invalid servers returns one result per server, "
            + "with only the invalid ones carrying errors.")]
        public async Task PingAsync_MixedValidAndInvalidServers_ReturnsResultPerServer()
        {
            var servers = new[]
            {
                new PingServer { Region = "bad", Ip = "", Port = 0 },
                new PingServer { Region = "good", Ip = "127.0.0.1", Port = 8080 },
            };

            var results = await PingClient.PingAsync(servers);

            Assert.That(results.Count, Is.EqualTo(2));
            var bad = results[0];
            Assert.That(bad.Region, Is.EqualTo("bad"));
            Assert.That(bad.Success, Is.False);
        }
    }
}
