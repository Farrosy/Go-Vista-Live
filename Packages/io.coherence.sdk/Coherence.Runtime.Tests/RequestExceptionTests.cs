// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System.Net;
    using Coherence.Cloud;
    using Coherence.Tests;
    using Log;
    using NUnit.Framework;
    using Result = UnityEngine.Networking.UnityWebRequest.Result;

    /// <summary>
    /// Edit mode unit tests for <see cref="RequestException"/>.
    /// </summary>
    public class RequestExceptionTests : CoherenceTest
    {
        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Succeeds_For_ConnectionError_Result(string response, HttpStatusCode responseCode, string error)
        {
            var success = RequestException.TryParse(Result.ConnectionError, response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.True);
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.HttpStatusCode, Is.EqualTo(responseCode));
            Assert.That(logger.GetCountForWarningID(Warning.RuntimeRequestResponse), Is.EqualTo(string.IsNullOrEmpty(response) ? 0 : 1));
        }

        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Succeeds_For_DataProcessingError_Result(string response, HttpStatusCode responseCode, string error)
        {
            var success = RequestException.TryParse(Result.DataProcessingError, response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.True);
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.HttpStatusCode, Is.EqualTo(responseCode));
            Assert.That(logger.GetCountForWarningID(Warning.RuntimeRequestResponse), Is.EqualTo(string.IsNullOrEmpty(response) ? 0 : 1));
        }

        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Succeeds_For_ProtocolError_Result(string response, HttpStatusCode responseCode, string error)
        {
            var success = RequestException.TryParse(Result.ProtocolError, response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.True);
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.HttpStatusCode, Is.EqualTo(responseCode));
            Assert.That(logger.GetCountForWarningID(Warning.RuntimeRequestResponse), Is.EqualTo(string.IsNullOrEmpty(response) ? 0 : 1));
        }

        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Fails_For_Sucess_Result(string response, HttpStatusCode responseCode, string error)
        {
            var success = RequestException.TryParse(Result.Success, response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.False);
            Assert.That(exception, Is.Null);
        }

        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Fails_For_InProgress_Result(string response, HttpStatusCode responseCode, string error)
        {
            var success = RequestException.TryParse(Result.InProgress, response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.False);
            Assert.That(exception, Is.Null);
        }

        [TestCase(null, default(HttpStatusCode), null)]
        [TestCase(null, default(HttpStatusCode), "")]
        [TestCase("", default(HttpStatusCode), null)]
        [TestCase("", default(HttpStatusCode), "")]
        [TestCase(null, HttpStatusCode.ServiceUnavailable, null)]
        [TestCase("Response", HttpStatusCode.ServiceUnavailable, "Error")]
        public void TryParse_Fails_For_Unnamed_Result(string response, HttpStatusCode responseCode, string error)
        {
            var success = RequestException.TryParse((Result)(-100), response, (int)responseCode, error, out var exception, logger);

            Assert.That(success, Is.False);
            Assert.That(exception, Is.Null);
        }

        [Test]
        public void TryParse_RSBadRequest_Returns_Exception_With_ErrorCode_And_Id()
        {
            var response = "{\"id\":\"68159a30:5;0\",\"error_code\":\"ERR_RS_BAD_REQUEST\",\"timeout\":false,\"fault\":false,\"temporary\":false}";

            var success = RequestException.TryParse(Result.ProtocolError, response, (int)HttpStatusCode.BadRequest, null, out var exception, logger);

            Assert.That(success, Is.True);
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.RequestId, Is.EqualTo("68159a30:5;0"));
            Assert.That(exception.Message, Does.Contain("68159a30:5;0"));
            Assert.That(exception.ErrorCode, Is.EqualTo(ErrorCode.RSBadRequest));
        }

        [Test]
        public void TryParse_RequireCreditCard_Returns_Exception_With_ErrorCode_And_Actionable_Message()
        {
            // Wire payload returned by the portal when a payment method is required (e.g. uploading a simulator).
            const string response = "{\"id\":\"b15be68c:6\",\"error_code\":\"REQUIRE_CREDIT_CARD\",\"timeout\":false,\"fault\":false,\"temporary\":false}";

            var success = RequestException.TryParse(Result.ProtocolError, response, (int)HttpStatusCode.BadRequest, null, out var exception, logger);

            Assert.That(success, Is.True);
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ErrorCode, Is.EqualTo(ErrorCode.RequireCreditCard));
            Assert.That(exception.RequestId, Is.EqualTo("b15be68c:6"));
            Assert.That(exception.Message, Does.Contain("payment method"));
        }

        [Test]
        public void TryParse_LobbyReservationsInvalid_Returns_Exception_With_ErrorCode_And_Actionable_Message()
        {
            // Wire payload returned when the requested reservations are refused: either they leave no slot for the
            // player making the request (more than MaxPlayers - 1), or they name a player that doesn't exist.
            const string response = "{\"id\":\"c1a2b3d4:1\",\"error_code\":\"ERR_LOBBY_RESERVATIONS_INVALID\",\"timeout\":false,\"fault\":false,\"temporary\":false}";

            var success = RequestException.TryParse(Result.ProtocolError, response, (int)HttpStatusCode.BadRequest, null, out var exception, logger);

            Assert.That(success, Is.True);
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ErrorCode, Is.EqualTo(ErrorCode.LobbyReservationsInvalid));
            Assert.That(exception.RequestId, Is.EqualTo("c1a2b3d4:1"));
            Assert.That(exception.Message, Does.Contain("reservations"));
            // The same error code covers both causes, so the message must name both, not only capacity.
            Assert.That(exception.Message, Does.Contain("MaxPlayers - 1"));
            Assert.That(exception.Message, Does.Contain("existing player accounts"));
        }

        [TestCase("ERR_LOBBY_INVITE_INVALID", HttpStatusCode.Forbidden, ErrorCode.LobbyInviteInvalid,
            "invite token", Description = "A token that no longer verifies, on join or on redeem.")]
        [TestCase("ERR_LOBBY_INVITE_POLICY", HttpStatusCode.Forbidden, ErrorCode.LobbyInvitePolicy,
            nameof(CreateLobbyOptions.InvitePermissions),
            Description = "Minting for an owner-only lobby, as anyone but its owner.")]
        [TestCase("ERR_LOBBY_INVITE_NOT_ALLOWED", HttpStatusCode.Forbidden, ErrorCode.LobbyInviteNotAllowed,
            "player id", Description = "Inviting a player id that cannot be invited.")]
        [TestCase("ERR_LOBBY_JOIN_CODE_TAKEN", HttpStatusCode.Conflict, ErrorCode.LobbyJoinCodeTaken,
            "another one", Description = "Creating a lobby with a code another open lobby already holds.")]
        [TestCase("ERR_LOBBY_JOIN_CODE_INVALID", HttpStatusCode.NotFound, ErrorCode.LobbyJoinCodeInvalid,
            "mistyped", Description = "Every code that fails to resolve, however it failed.")]
        [TestCase("ERR_LOBBY_RATE_LIMITED", HttpStatusCode.TooManyRequests, ErrorCode.LobbyRateLimited,
            "Wait a moment", Description = "Minting or guessing codes too quickly.")]
        public void TryParse_LobbyInviteError_Returns_ErrorCode_And_Actionable_Message(
            string wireCode, HttpStatusCode status, ErrorCode expected, string messageFragment)
        {
            var response = "{\"id\":\"c1a2b3d4:2\",\"error_code\":\"" + wireCode +
                "\",\"timeout\":false,\"fault\":false,\"temporary\":false}";

            var success = RequestException.TryParse(Result.ProtocolError, response, (int)status, null,
                out var exception, logger);

            Assert.That(success, Is.True);
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ErrorCode, Is.EqualTo(expected));
            Assert.That(exception.RequestId, Is.EqualTo("c1a2b3d4:2"));
            Assert.That(exception.Message, Does.Contain(messageFragment));
        }
    }
}
