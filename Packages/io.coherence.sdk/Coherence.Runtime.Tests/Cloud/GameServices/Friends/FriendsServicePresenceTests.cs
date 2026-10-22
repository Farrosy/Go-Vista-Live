// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Log;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;

    public class FriendsServicePresenceTests : FriendsServiceTestBase
    {
        // -- writing your own presence ----------------------------------------------------------

        [Test]
        public async Task SetPresenceAsync_PostsIt_WhenGivenOne()
        {
            var calls = RecordCalls();
            using var friends = CreateService();

            await friends.SetPresenceAsync("boss fight, 2/4 alive");

            Assert.That(calls[0].path, Is.EqualTo("/presence"));
            Assert.That(calls[0].method, Is.EqualTo("POST"));
            Assert.That(Body(calls[0])["presence"]?.Value<string>(), Is.EqualTo("boss fight, 2/4 alive"));
        }

        [Test]
        [Description("Setting presence does not including changes for other fields (like appear_offline).")]
        public async Task SetPresenceAsync_DoesNotIncludeAppearingOffline_WhenSettingAPresence()
        {
            var calls = RecordCalls();
            using var friends = CreateService();

            await friends.SetPresenceAsync("boss fight");

            Assert.That(Body(calls[0]).ContainsKey("appear_offline"), Is.False);
        }

        [Test]
        [Description("When SetPresenceAsync(null), it should send presence in the body (instead of nothing, which would not update presence at all).")]
        public async Task SetPresenceAsync_PostsPresence_WhenNull()
        {
            var calls = RecordCalls();
            using var friends = CreateService();

            await friends.SetPresenceAsync(null);

            var body = Body(calls[0]);
            Assert.That(body.ContainsKey("presence"), Is.True, "Clearing presence did not include 'presence' in message body.");
            Assert.That(body["presence"]?.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        [Description("SetAppearOfflineAsync() should not change presence.")]
        public async Task SetAppearOfflineAsync_SaysNothingAboutThePresence_WhenCalled()
        {
            var calls = RecordCalls();
            using var friends = CreateService();

            await friends.SetAppearOfflineAsync(true);

            var body = Body(calls[0]);
            Assert.That(body["appear_offline"]?.Value<bool>(), Is.True);
            Assert.That(body.ContainsKey("presence"), Is.False, "Appearing offline changed presence.");
        }

        [Test]
        [Description("coherence Cloud reads an empty presence as none, so the SDK sends what that means.")]
        public async Task SetPresenceAsync_ClearsIt_WhenGivenAnEmptyPresence()
        {
            var calls = RecordCalls();
            using var friends = CreateService();

            await friends.SetPresenceAsync("");

            Assert.That(Body(calls[0])["presence"]?.Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void SetPresenceAsync_FailsWithPresenceNotConnected_WhenThePlayerIsNotConnected()
        {
            requestFactoryBuilder.OnSendRequestAsyncCalled(
                new RequestException(ErrorCode.PresenceNotConnected, HttpStatusCode.Conflict));
            using var friends = CreateService();

            var operation = friends.SetPresenceAsync("boss fight");

            Assert.That(operation.HasFailed, Is.True);
            Assert.That(operation.Error.Type, Is.EqualTo(FriendsErrorType.PresenceNotConnected));
        }

        [Test]
        [Description("A presence coherence Cloud rejects, such as a too long one, is sent and reported as invalid.")]
        public void SetPresenceAsync_FailsWithPresenceInvalid_WhenCloudRejectsThePayload()
        {
            requestFactoryBuilder.OnSendRequestAsyncCalled(
                new RequestException(ErrorCode.PresencePayloadInvalid, HttpStatusCode.BadRequest));
            using var friends = CreateService();

            var operation = friends.SetPresenceAsync("boss fight");

            Assert.That(operation.HasFailed, Is.True);
            Assert.That(operation.Error.Type, Is.EqualTo(FriendsErrorType.PresenceInvalid));
        }

        [Test]
        [Description("Presence rate limited error is handled.")]
        public void SetPresenceAsync_FailsWithTooManyRequests_WhenTooManyWentOutInARow()
        {
            requestFactoryBuilder.OnSendRequestAsyncCalled(
                new RequestException(ErrorCode.PresenceRateLimited, HttpStatusCode.TooManyRequests));
            using var friends = CreateService();

            var operation = friends.SetPresenceAsync("boss fight");

            Assert.That(operation.HasFailed, Is.True);
            Assert.That(operation.Error.Type, Is.EqualTo(FriendsErrorType.TooManyRequests));
        }

        [Test]
        public void SetPresenceAsync_Throws_WhenTheServiceIsAlreadyDisposed()
        {
            var friends = CreateService();
            friends.Dispose();

            Assert.That(() => friends.SetPresenceAsync("boss fight"), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        public void SetAppearOfflineAsync_Throws_WhenTheServiceIsAlreadyDisposed()
        {
            var friends = CreateService();
            friends.Dispose();

            Assert.That(() => friends.SetAppearOfflineAsync(true), Throws.TypeOf<ObjectDisposedException>());
        }

        // -- hearing about a friend's presence ----------------------------------------------------

        [Test]
        public async Task OnFriendPresenceChanged_HandsOverTheFriendAsTheyAreNow_WhenAPresencePushArrives()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            Friend announced = default;
            friends.OnFriendPresenceChanged += friend => announced = friend;

            requestFactoryBuilder.RaisePushCallback(PresencePush, PresenceBody("1",
                online: true, lobbyIds: "[\"lby_8f3a\"]", presence: "\"outfit:pirate_03\""));

            Assert.That(announced.Id, Is.EqualTo((PlayerAccountId)"1"));
            Assert.That(announced.IsOnline, Is.True);
            Assert.That(announced.LobbyIds, Is.EqualTo(new[] { "lby_8f3a" }));
            Assert.That(announced.Presence, Is.EqualTo("outfit:pirate_03"));
            Assert.That(announced.DisplayName, Is.EqualTo("Alice"), "Presence overwrote who the friend is.");
        }

        [Test]
        [Description("Presence is updated when the presence push is sent.")]
        public async Task Friends_HoldsTheNewPresence_WhenAPresencePushArrives()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));

            requestFactoryBuilder.RaisePushCallback(PresencePush,
                PresenceBody("1", online: true, presence: "\"in menu\""));

            Assert.That(friends.Friends[0].Presence, Is.EqualTo("in menu"));
            Assert.That(friends.Friends[0].IsOnline, Is.True);
        }

        [Test]
        [Description("Going offline should clear Presence and LobbyIds, but keep LastOnlineAt")]
        public async Task OnFriendPresenceChanged_CarriesTheWholeState_WhenTheFriendGoesOffline()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            requestFactoryBuilder.RaisePushCallback(PresencePush, PresenceBody("1",
                online: true, lobbyIds: "[\"lby_8f3a\"]", presence: "\"boss fight\""));

            requestFactoryBuilder.RaisePushCallback(PresencePush,
                PresenceBody("1", online: false, lastOnlineAt: "1754330000"));

            var friend = friends.Friends[0];
            Assert.That(friend.IsOnline, Is.False);
            Assert.That(friend.Presence, Is.Empty, "An offline friend was left with the presence they had.");
            Assert.That(friend.LobbyIds, Is.Empty, "An offline friend was left in a lobby.");
            Assert.That(friend.LastOnlineAt,
                Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1754330000)));
        }

        [Test]
        [Description("No changes to the stored friends list when there's changes to players that are not friends.")]
        public async Task Friends_IsLeftAlone_WhenAPresencePushNamesSomebodyWhoIsNotAFriend()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            var announced = false;
            friends.OnFriendPresenceChanged += _ => announced = true;

            requestFactoryBuilder.RaisePushCallback(PresencePush, PresenceBody("2", online: true));

            Assert.That(friends.Friends.Select(friend => friend.Id), Is.EqualTo(new PlayerAccountId[] { "1" }));
            Assert.That(announced, Is.False);
        }

        [Test]
        [Description("An invalid presence push must not modify friend data.")]
        public async Task Friends_IsLeftAlone_WhenAPresencePushCannotBeRead()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            requestFactoryBuilder.RaisePushCallback(PresencePush,
                PresenceBody("1", online: true, presence: "\"boss fight\""));

            requestFactoryBuilder.RaisePushCallback(PresencePush, "not json");

            Assert.That(friends.Friends[0].Presence, Is.EqualTo("boss fight"));
            Assert.That(logger.GetCountForErrorID(Error.RuntimeCloudDeserializationException), Is.EqualTo(1));
        }

        // -- surviving a reconnect ----------------------------------------------------------------

        [Test]
        [Description("Writes are not queued, so the newest presence is the one a reconnect should use.")]
        public async Task FriendsService_PutsTheNewestPresenceBackUp_WhenTwoWritesAreAnsweredOutOfOrder()
        {
            // The mock captures its send hook when the request factory is built,
            // so this has to be in place before the service exists.
            var answers = new List<TaskCompletionSource<string>>();
            requestFactoryBuilder.SendRequestAsyncReturns((basePath, pathParams, method, body, _, _, _) =>
            {
                Calls.Add((basePath + pathParams, method, body));

                if (basePath + pathParams is not "/presence")
                {
                    return Task.FromResult(FriendsBody(Friend("1", "Alice")));
                }

                var answer = new TaskCompletionSource<string>();
                answers.Add(answer);
                return answer.Task;
            });

            using var friends = CreateService();
            await friends.RefreshFriendsAsync();

            var older = friends.SetPresenceAsync("boss fight");
            var newer = friends.SetPresenceAsync("in the lobby");

            // The newer write is answered first, so the older one assigns last.
            answers[1].SetResult("");
            answers[0].SetResult("");
            await older;
            await newer;
            Calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            var presence = Calls.Where(call => call.path is "/presence").ToList();
            Assert.That(presence, Has.Count.EqualTo(1), "The reconnect did not put a presence back up.");
            Assert.That(Body(presence[0])["presence"]?.Value<string>(), Is.EqualTo("in the lobby"),
                "The reconnect put back a presence the game had already replaced.");
        }

        [Test]
        [Description("Presence is sent after reconnect.")]
        public async Task FriendsService_SetsThePresenceAgain_WhenTheConnectionComesBack()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            await friends.SetPresenceAsync("boss fight");
            Calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            var presence = Calls.Where(call => call.path is "/presence").ToList();
            Assert.That(presence, Has.Count.EqualTo(1), "The presence was not set again after the reconnect.");
            Assert.That(Body(presence[0])["presence"]?.Value<string>(), Is.EqualTo("boss fight"));
        }

        [Test]
        [Description("When no presence is set, reconnecting should not send presence.")]
        public async Task FriendsService_SetsNoPresence_WhenTheConnectionComesBackAfterClearingIt()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            await friends.SetPresenceAsync("boss fight");
            await friends.SetPresenceAsync(null);
            Calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(Calls.Any(call => call.path is "/presence"), Is.False);
        }

        [Test]
        [Description("Presence that comes from catch-up is newer and must not be overwritten.")]
        public async Task FriendsService_SetsNoPresenceAgain_WhenTheGameSetOneDuringCatchUp()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            await friends.SetPresenceAsync("boss fight");
            Calls.Clear();

            var fetch = HoldNextResponse();
            requestFactoryBuilder.RaiseOnWebSocketConnect();

            await friends.SetPresenceAsync("in the lobby");
            fetch.SetResult(FriendsBody(Friend("1", "Alice")));

            var presence = Calls.Where(call => call.path is "/presence").ToList();
            Assert.That(presence, Has.Count.EqualTo(1), "Catch-up put a stale presence back up.");
            Assert.That(Body(presence[0])["presence"]?.Value<string>(), Is.EqualTo("in the lobby"));
        }

        [Test]
        [Description("The next player to log in must not inherit the last one's presence.")]
        public async Task FriendsService_ForgetsThePresence_WhenThePlayerLogsOut()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            await friends.SetPresenceAsync("boss fight");
            authClientBuilder.RaiseOnLogout();
            Calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(Calls, Is.Empty);
        }

        [Test]
        [Description("Appearing offline does not call presence on reconnect.")]
        public async Task FriendsService_SaysNothingAboutAppearingOffline_WhenTheConnectionComesBack()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            await friends.SetAppearOfflineAsync(true);
            Calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(Calls.Any(call => call.path is "/presence"), Is.False);
        }

        [Test]
        [Description("An update push holds snapshots. Presence is taken from the update push, not from a previous presence push.")]
        public async Task Friends_TakeThePresenceFromTheUpdate_WhenAKnownFriendIsSentAgain()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            requestFactoryBuilder.RaisePushCallback(PresencePush,
                PresenceBody("1", online: true, presence: "\"boss fight\""));

            requestFactoryBuilder.RaisePushCallback(UpdatePush, "{\"added\":[" + Friend("1", "Alice") + "]}");

            Assert.That(friends.Friends[0].IsOnline, Is.False);
            Assert.That(friends.Friends[0].Presence, Is.Empty);
        }

        [Test]
        [Description("When an update push changes presence, OnFriendPresenceChanged must trigger.")]
        public async Task OnFriendPresenceChanged_IsRaised_WhenAnUpdateMovesAKnownFriendsPresence()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            Friend announced = default;
            friends.OnFriendPresenceChanged += friend => announced = friend;

            requestFactoryBuilder.RaisePushCallback(UpdatePush,
                "{\"added\":[" + OnlineFriend("1", "Alice", "\"boss fight\"") + "]}");

            Assert.That(announced.Id, Is.EqualTo((PlayerAccountId)"1"));
            Assert.That(announced.Presence, Is.EqualTo("boss fight"));
        }

        [Test]
        [Description("Presence request is cancelled if the player logs out.")]
        public async Task FriendsService_SetsNoPresenceAgain_WhenTheWriteLandedAfterALogout()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            var response = HoldNextResponse();

            var set = friends.SetPresenceAsync("boss fight");
            authClientBuilder.RaiseOnLogout();
            response.SetResult("");
            await set;

            Assert.That(set.IsCanceled, Is.True, "Presence request wasn't cancelled when the player logged out.");

            // Read the list again so a reconnect has something to catch up on, then watch what it sends.
            await friends.RefreshFriendsAsync();
            Calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(Calls.Any(call => call.path is "/presence"), Is.False);
        }

        [Test]
        [Description("Multiple consecutive reconnections only trigger presence once.")]
        public async Task FriendsService_SetsThePresenceOnce_WhenTheConnectionComesBackDuringACatchUp()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            await friends.SetPresenceAsync("boss fight");
            Calls.Clear();

            // The first catch-up's fetch is left hanging, as a request from a dropped socket is.
            var stale = HoldNextResponse();
            requestFactoryBuilder.RaiseOnWebSocketConnect();

            requestFactoryBuilder.RaiseOnWebSocketConnect();
            stale.SetResult(Response);
            await Task.Yield();

            Assert.That(Calls.Count(call => call.path is "/presence"), Is.EqualTo(1),
                "The presence went up for a connection already gone, or did not go up at all.");
        }

        // -- catching up after a break --------------------------------------------------------------

        [Test]
        [Description("On reconnect, trigger OnFriendPresenceChanged if there was a friend that changed.")]
        public async Task OnFriendPresenceChanged_IsRaised_WhenTheCatchUpFindsAFriendChanged()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            Friend announced = default;
            friends.OnFriendPresenceChanged += friend => announced = friend;
            Response = FriendsBody(OnlineFriend("1", "Alice", "\"boss fight\""));

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(announced.Id, Is.EqualTo((PlayerAccountId)"1"));
            Assert.That(announced.Presence, Is.EqualTo("boss fight"));
        }

        [Test]
        [Description("On reconnect, trigger OnFriendRemoved if there was a friend that was removed.")]
        public async Task OnFriendRemoved_IsRaised_WhenTheCatchUpFindsAFriendGone()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"), Friend("2", "Bob"));
            var gone = new List<PlayerAccountId>();
            friends.OnFriendRemoved += friend => gone.Add(friend.Id);
            Response = FriendsBody(Friend("1", "Alice"));

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(gone, Is.EqualTo(new PlayerAccountId[] { "2" }));
        }

        [Test]
        [Description("On reconnect, trigger OnFriendAdded if there was a friend that was added.")]
        public async Task OnFriendAdded_IsRaised_WhenTheCatchUpFindsANewFriend()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            var added = new List<PlayerAccountId>();
            friends.OnFriendAdded += friend => added.Add(friend.Id);
            Response = FriendsBody(Friend("1", "Alice"), Friend("2", "Bob"));

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(added, Is.EqualTo(new PlayerAccountId[] { "2" }));
        }

        [Test]
        [Description("On reconnect, don't trigger events if friends do not change.")]
        public async Task OnFriendPresenceChanged_IsNotRaised_WhenTheCatchUpFindsNothingChanged()
        {
            using var friends = await ServiceKnowing(OnlineFriend("1", "Alice", "\"boss fight\""));
            var announced = false;
            friends.OnFriendPresenceChanged += _ => announced = true;
            Response = FriendsBody(OnlineFriend("1", "Alice", "\"boss fight\""));

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(announced, Is.False);
        }

        [Test]
        [Description("On reconnect, don't update friends if the update happens after log out.")]
        public async Task Friends_StaysEmpty_WhenACatchUpLandsAfterThePlayerLoggedOut()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            var response = HoldNextResponse();

            requestFactoryBuilder.RaiseOnWebSocketConnect();
            authClientBuilder.RaiseOnLogout();
            response.SetResult(FriendsBody(Friend("1", "Alice")));
            await Task.Yield();

            Assert.That(friends.Friends, Is.Empty);
        }

        [Test]
        [Description("Presence is not set when given a cancelled token.")]
        public async Task FriendsService_SetsNoPresenceAgain_WhenTheOneItWasGivenWasCancelled()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await friends.SetPresenceAsync("boss fight", cancellation.Token);
            Calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(Calls.Any(call => call.path is "/presence"), Is.False);
        }

        [Test]
        [Description("A game that only broadcasts presence, and never reads the friend list, still gets it back up after a reconnect.")]
        public async Task FriendsService_SetsThePresenceAgain_WhenTheConnectionComesBackAndTheListWasNeverRead()
        {
            var calls = RecordCalls();
            using var friends = CreateService();
            await friends.SetPresenceAsync("boss fight");
            calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            var presence = calls.Where(call => call.path is "/presence").ToList();
            Assert.That(presence, Has.Count.EqualTo(1), "The presence was not set again after the reconnect.");
            Assert.That(Body(presence[0])["presence"]?.Value<string>(), Is.EqualTo("boss fight"));
            Assert.That(calls.Any(call => call.path is "/friends"), Is.False,
                "The reconnect fetched a friend list the game never asked for.");
        }

        [Test]
        [Description("The presence is a connection's, not the friend list's: failing to read the list must not also leave friends without the presence.")]
        public async Task FriendsService_SetsThePresenceAgain_WhenTheCatchUpCannotReadTheList()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            await friends.SetPresenceAsync("boss fight");
            Calls.Clear();
            Response = "not json";

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            var presence = Calls.Where(call => call.path is "/presence").ToList();
            Assert.That(presence, Has.Count.EqualTo(1), "A catch-up that failed took the presence with it.");
            Assert.That(Body(presence[0])["presence"]?.Value<string>(), Is.EqualTo("boss fight"));
            Assert.That(logger.GetCountForWarningID(Warning.RuntimeCloudFriendsCatchUpFailed), Is.EqualTo(1));
            Assert.That(logger.GetCountForErrorID(Error.RuntimeCloudDeserializationException), Is.EqualTo(1));
        }

        [Test]
        [Description("When the newer of two overlapping writes fails, coherence Cloud holds the older one, so that is the one to put back up.")]
        public async Task FriendsService_PutsBackThePresenceThatLanded_WhenTheNewerWriteFailed()
        {
            var answers = new List<TaskCompletionSource<string>>();
            requestFactoryBuilder.SendRequestAsyncReturns((basePath, pathParams, method, body, _, _, _) =>
            {
                Calls.Add((basePath + pathParams, method, body));

                if (basePath + pathParams is not "/presence" || answers.Count >= 2)
                {
                    return Task.FromResult(FriendsBody(Friend("1", "Alice")));
                }

                var answer = new TaskCompletionSource<string>();
                answers.Add(answer);
                return answer.Task;
            });

            using var friends = CreateService();
            await friends.RefreshFriendsAsync();

            var older = friends.SetPresenceAsync("boss fight");
            var newer = friends.SetPresenceAsync("in the lobby");

            answers[0].SetResult("");
            answers[1].SetException(
                new RequestException(ErrorCode.PresenceRateLimited, HttpStatusCode.TooManyRequests));
            await older;
            await newer;
            Assert.That(newer.HasFailed, Is.True);
            Assert.That(newer.Error.Type, Is.EqualTo(FriendsErrorType.TooManyRequests));
            Calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            var presence = Calls.Where(call => call.path is "/presence").ToList();
            Assert.That(presence, Has.Count.EqualTo(1), "The reconnect did not put back the presence friends saw.");
            Assert.That(Body(presence[0])["presence"]?.Value<string>(), Is.EqualTo("boss fight"));
        }

        [Test]
        [Description("Every presence push carries full state, so one that repeats what is known changes nothing.")]
        public async Task OnFriendPresenceChanged_IsNotRaised_WhenAPresencePushRepeatsWhatIsKnown()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            var push = PresenceBody("1", online: true, lobbyIds: "[\"lby_8f3a\"]", presence: "\"boss fight\"");
            requestFactoryBuilder.RaisePushCallback(PresencePush, push);
            var announced = 0;
            friends.OnFriendPresenceChanged += _ => announced++;

            requestFactoryBuilder.RaisePushCallback(PresencePush, push);

            Assert.That(announced, Is.Zero);
        }

        // -- helpers -------------------------------------------------------------------------------

        private static JObject Body((string path, string method, string body) call) => JObject.Parse(call.body);

        private static string OnlineFriend(string id, string displayName, string presence)
            => "{\"player_id\":\"" + id + "\",\"display_name\":\"" + displayName +
               "\",\"online\":true,\"presence\":" + presence + "}";

        private static string PresenceBody(string id, bool online, string lobbyIds = "[]", string presence = "null",
            string lastOnlineAt = "null")
            => "{\"player_id\":\"" + id + "\",\"online\":" + (online ? "true" : "false") +
               ",\"lobby_ids\":" + lobbyIds + ",\"presence\":" + presence +
               ",\"last_online_at\":" + lastOnlineAt + ",\"ts\":1754330000}";
    }
}
