// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Linq;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Utils;
    using Log;
    using Newtonsoft.Json;
    using NUnit.Framework;

    public class FriendsServiceTests : FriendsServiceTestBase
    {
        [Test]
        public async Task SyncFriendsAsync_PostsTheSourceAndItsFriendIds_WhenGivenAFriendList()
        {
            var calls = RecordCalls();
            using var friends = CreateService();

            await friends.SyncFriendsAsync("steam", new[] { "76561", "76562" });

            var sync = calls[0];
            var sent = CoherenceJson.DeserializeObject<DecodedSync>(sync.body);
            Assert.That(sync.path, Is.EqualTo("/friends/native-sync"));
            Assert.That(sync.method, Is.EqualTo("POST"));
            Assert.That(sent.Source, Is.EqualTo("steam"));
            Assert.That(sent.FriendIds, Is.EqualTo(new[] { "76561", "76562" }));
        }

        [Test]
        [Description("An empty list is how a game removes every friend synced from a platform, so it must still be sent.")]
        public async Task SyncFriendsAsync_StillPostsTheList_WhenItIsEmpty()
        {
            var calls = RecordCalls();
            using var friends = CreateService();

            await friends.SyncFriendsAsync("steam", new string[0]);

            var sent = CoherenceJson.DeserializeObject<DecodedSync>(calls[0].body);
            Assert.That(sent.FriendIds, Is.Empty);
        }

        [Test]
        public async Task RefreshFriendsAsync_GetsTheFriendList_WhenCalled()
        {
            var calls = RecordCalls();
            using var friends = CreateService();

            await friends.RefreshFriendsAsync();

            Assert.That(calls[0].path, Is.EqualTo("/friends"));
            Assert.That(calls[0].method, Is.EqualTo("GET"));
        }

        [Test]
        public void SyncFriendsAsync_Fails_WhenNoSourceIsGiven()
        {
            using var friends = CreateService();

            var operation = friends.SyncFriendsAsync("", new[] { "76561" });

            Assert.That(operation.HasFailed, Is.True);
            Assert.That(operation.Error.Type, Is.EqualTo(FriendsErrorType.InvalidInput));
        }

        [Test]
        [Description("Passing null would silently look like 'remove everything', so it is rejected.")]
        public void SyncFriendsAsync_Fails_WhenTheFriendIdsAreNull()
        {
            using var friends = CreateService();

            var operation = friends.SyncFriendsAsync("steam", null);

            Assert.That(operation.HasFailed, Is.True);
            Assert.That(operation.Error.Type, Is.EqualTo(FriendsErrorType.InvalidInput));
        }

        [Test]
        public async Task RefreshFriendsAsync_FillsInTheFriendList_WhenTheBackendReturnsFriends()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(FriendsBody(Friend("1", "Alice"), Friend("2", "Bob")));
            using var friends = CreateService();

            await friends.RefreshFriendsAsync();

            Assert.That(friends.Friends.Select(f => f.Id), Is.EqualTo(new PlayerAccountId[] { "1", "2" }));
            Assert.That(friends.Friends[0].DisplayName, Is.EqualTo("Alice"));
        }

        [Test]
        [Description("How many friends actually play the game is the only signal a sync did anything.")]
        public async Task SyncFriendsAsync_ReportsHowManyFriendsPlayTheGame_WhenTheSyncSucceeds()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(
                () => Task.FromResult("{\"resolved\":3,\"discarded\":37}"),
                () => Task.FromResult(FriendsBody()));
            using var friends = CreateService();

            var result = (await friends.SyncFriendsAsync("steam", new[] { "76561" })).Result;

            Assert.That(result.Resolved, Is.EqualTo(3));
            Assert.That(result.Discarded, Is.EqualTo(37));
        }

        [Test]
        public async Task SyncFriendsAsync_RefreshesTheFriendList_BeforeTheRequestFinishes()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(
                () => Task.FromResult("{\"resolved\":1,\"discarded\":0}"),
                () => Task.FromResult(FriendsBody(Friend("1", "Alice"))));
            using var friends = CreateService();

            await friends.SyncFriendsAsync("steam", new[] { "76561" });

            Assert.That(friends.Friends.Select(f => f.Id), Is.EqualTo(new PlayerAccountId[] { "1" }));
        }

        [Test]
        public void Friends_GainsTheFriendAndRaisesOnFriendAdded_WhenAnAddedPushArrives()
        {
            using var friends = CreateService();
            Friend? added = null;
            friends.OnFriendAdded += friend => added = friend;

            requestFactoryBuilder.RaisePushCallback(UpdatePush, "{\"added\":[" + Friend("1", "Alice") + "]}");

            Assert.That(added?.Id, Is.EqualTo((PlayerAccountId)"1"));
            Assert.That(friends.Friends.Select(f => f.Id), Is.EqualTo(new PlayerAccountId[] { "1" }));
        }

        [Test]
        public async Task Friends_DropsTheFriendAndRaisesOnFriendRemoved_WhenARemovedPushArrives()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(FriendsBody(Friend("1", "Alice"), Friend("2", "Bob")));
            using var friends = CreateService();
            await friends.RefreshFriendsAsync();
            Friend? removed = null;
            friends.OnFriendRemoved += friend => removed = friend;

            requestFactoryBuilder.RaisePushCallback(UpdatePush, "{\"removed\":[\"1\"]}");

            Assert.That(removed?.Id, Is.EqualTo((PlayerAccountId)"1"));
            Assert.That(removed?.DisplayName, Is.EqualTo("Alice"), "The removed friend has to arrive named, so a game can say who left.");
            Assert.That(friends.Friends.Select(f => f.Id), Is.EqualTo(new PlayerAccountId[] { "2" }));
        }

        [Test]
        public void OnFriendRemoved_IsNotRaised_WhenTheRemovedFriendWasNeverInTheList()
        {
            using var friends = CreateService();
            var announced = false;
            friends.OnFriendRemoved += _ => announced = true;

            requestFactoryBuilder.RaisePushCallback(UpdatePush, "{\"removed\":[\"3\"]}");

            Assert.That(announced, Is.False);
        }

        [Test]
        [Description("A push replayed after a reconnect must not announce the same friend twice.")]
        public void OnFriendAdded_IsRaisedOnce_WhenTheSameAddedPushIsReplayed()
        {
            using var friends = CreateService();
            var announced = 0;
            friends.OnFriendAdded += _ => announced++;

            requestFactoryBuilder.RaisePushCallback(UpdatePush, "{\"added\":[" + Friend("1", "Alice") + "]}");
            requestFactoryBuilder.RaisePushCallback(UpdatePush, "{\"added\":[" + Friend("1", "Alice") + "]}");

            Assert.That(announced, Is.EqualTo(1));
            Assert.That(friends.Friends, Has.Count.EqualTo(1));
        }

        [Test]
        [Description("The friend list handed to a game must not be a handle on the service's own cache.")]
        public async Task Friends_CannotBeModified_WhenAGameCastsItToAMutableList()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(FriendsBody(Friend("1", "Alice")));
            using var friends = CreateService();
            await friends.RefreshFriendsAsync();

            Assert.That(() => ((IList<Friend>)friends.Friends).Clear(), Throws.TypeOf<NotSupportedException>());
            Assert.That(friends.Friends, Has.Count.EqualTo(1));
        }

        [Test]
        [Description("A game that draws its list from the events must not go stale because it asked for a refresh.")]
        public async Task OnFriendRemoved_IsRaised_WhenARefreshFindsAFriendGone()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(FriendsBody(Friend("1", "Alice"), Friend("2", "Bob")));
            using var friends = CreateService();
            await friends.RefreshFriendsAsync();
            var gone = new List<PlayerAccountId>();
            friends.OnFriendRemoved += friend => gone.Add(friend.Id);

            requestFactoryBuilder.SendRequestAsyncReturns(FriendsBody(Friend("1", "Alice")));
            await friends.RefreshFriendsAsync();

            Assert.That(gone, Is.EqualTo(new PlayerAccountId[] { "2" }));
        }

        [Test]
        [Description("The game asked for the list and is about to read it, so the first read announces nothing.")]
        public async Task OnFriendAdded_IsNotRaised_WhenTheListIsReadForTheFirstTime()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(FriendsBody(Friend("1", "Alice")));
            using var friends = CreateService();
            var announced = false;
            friends.OnFriendAdded += _ => announced = true;

            await friends.RefreshFriendsAsync();

            Assert.That(announced, Is.False);
            Assert.That(friends.Friends, Has.Count.EqualTo(1));
        }

        [Test]
        [Description("Replayed pushes cover a short break, but a long one outruns them, so the list is read again.")]
        public async Task FriendsService_ReadsTheListAgain_WhenTheConnectionComesBack()
        {
            var calls = RecordCalls(FriendsBody(Friend("1", "Alice")));
            using var friends = CreateService();
            await friends.RefreshFriendsAsync();
            calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(calls.Select(call => (call.path, call.method)),
                Is.EqualTo(new[] { ("/friends", "GET") }));
        }

        [Test]
        [Description("A game that never asked for a friend list is not handed one behind its back.")]
        public void FriendsService_FetchesNothing_WhenTheConnectionComesBackAndTheListWasNeverRead()
        {
            var calls = RecordCalls();
            using var friends = CreateService();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(calls, Is.Empty);
        }

        [Test]
        [Description("When a reconnect occurs that kills the first SyncFriends, the friend list should be loaded again.")]
        public async Task FriendsService_ReadsTheListAgain_WhenAReconnectCutTheFirstSyncShort()
        {
            var reconnected = false;
            var reads = 0;

            requestFactoryBuilder.SendRequestAsyncReturns((basePath, pathParams, method, body, _, _, _) =>
            {
                if (method is not "GET")
                {
                    return Task.FromResult(FriendsBody());
                }

                reads++;

                if (!reconnected)
                {
                    reconnected = true;
                    requestFactoryBuilder.RaiseOnWebSocketConnect();
                }

                return Task.FromResult(FriendsBody(Friend("1", "Alice")));
            });

            using var friends = CreateService();

            await friends.SyncFriendsAsync("steam", new[] { "76561" });

            Assert.That(reads, Is.GreaterThan(1), "The reconnect never read the friend list again.");
            Assert.That(friends.Friends.Select(friend => friend.Id), Is.EqualTo(new PlayerAccountId[] { "1" }),
                "The synced list was uploaded but never landed in FriendsService.Friends.");
        }

        [Test]
        [Description("Logging out takes the list away, so coming back connected must not go and read it again.")]
        public async Task FriendsService_FetchesNothing_WhenTheConnectionComesBackAfterALogout()
        {
            var calls = RecordCalls(FriendsBody(Friend("1", "Alice")));
            using var friends = CreateService();
            await friends.RefreshFriendsAsync();
            authClientBuilder.RaiseOnLogout();
            calls.Clear();

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(calls, Is.Empty);
        }

        [Test]
        [Description("When the catch-up cannot be read (e.g., invalid JSON response), don't modify the friend list and warn.")]
        public async Task FriendsService_WarnsAndKeepsTheList_WhenTheCatchUpCannotBeRead()
        {
            using var friends = await ServiceKnowing(Friend("1", "Alice"));
            Response = "not json";

            requestFactoryBuilder.RaiseOnWebSocketConnect();

            Assert.That(friends.Friends.Select(friend => friend.Id),
                Is.EqualTo(new PlayerAccountId[] { "1" }), "A catch-up that failed took the list with it.");
            Assert.That(logger.GetCountForWarningID(Warning.RuntimeCloudFriendsCatchUpFailed), Is.EqualTo(1));
            Assert.That(logger.GetCountForErrorID(Error.RuntimeCloudDeserializationException), Is.EqualTo(1));
        }

        [Test]
        [Description("A sync whose answer cannot be read must not look like a sync that resolved nobody.")]
        public async Task SyncFriendsAsync_Fails_WhenTheAnswerCannotBeRead()
        {
            // Only the sync's own answer is unreadable; the refresh that follows it succeeds, so the
            // failure can come from nowhere else.
            requestFactoryBuilder.SendRequestAsyncReturns(
                () => Task.FromResult("not json"),
                () => Task.FromResult(FriendsBody(Friend("1", "Alice"))));
            using var friends = CreateService();

            var operation = await friends.SyncFriendsAsync("steam", new[] { "76561" });

            Assert.That(operation.HasFailed, Is.True, "A sync whose answer could not be read reported success.");
            Assert.That(operation.Error.Type, Is.EqualTo(FriendsErrorType.InvalidResponse));
            Assert.That(logger.GetCountForErrorID(Error.RuntimeCloudDeserializationException), Is.EqualTo(1));
        }

        [Test]
        [Description("A garbled push must not take out the friend list.")]
        public async Task Friends_IsLeftAlone_WhenAPushCannotBeRead()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(FriendsBody(Friend("1", "Alice")));
            using var friends = CreateService();
            await friends.RefreshFriendsAsync();

            requestFactoryBuilder.RaisePushCallback(UpdatePush, "not json");

            Assert.That(friends.Friends.Select(f => f.Id), Is.EqualTo(new PlayerAccountId[] { "1" }));
            Assert.That(logger.GetCountForErrorID(Error.RuntimeCloudDeserializationException), Is.EqualTo(1));
        }

        [Test]
        public async Task Friends_IsEmptied_WhenThePlayerLogsOut()
        {
            requestFactoryBuilder.SendRequestAsyncReturns(FriendsBody(Friend("1", "Alice")));
            using var friends = CreateService();
            await friends.RefreshFriendsAsync();

            authClientBuilder.RaiseOnLogout();

            Assert.That(friends.Friends, Is.Empty);
        }

        [Test]
        [Description("Reaching for a disposed service is a mistake in the game's code, so it is reported where it happened.")]
        public void RefreshFriendsAsync_Throws_WhenTheServiceIsAlreadyDisposed()
        {
            var friends = CreateService();
            friends.Dispose();

            Assert.That(() => friends.RefreshFriendsAsync(), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        public void SyncFriendsAsync_Throws_WhenTheServiceIsAlreadyDisposed()
        {
            var friends = CreateService();
            friends.Dispose();

            Assert.That(() => friends.SyncFriendsAsync("steam", new[] { "76561" }), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        [Description("A game has to be able to see the throw coming, so disposal is readable.")]
        public void IsDisposed_TellsAGameWhetherTheThrowIsComing()
        {
            var friends = CreateService();

            Assert.That(friends.IsDisposed, Is.False);

            friends.Dispose();

            Assert.That(friends.IsDisposed, Is.True);
        }

        [Test]
        [Description("A teardown that asked for ongoing work to finish has to let the request complete, not cancel it.")]
        public async Task DisposeAsync_LetsTheRequestFinish_WhenAskedToWaitForOngoingOperations()
        {
            var response = new TaskCompletionSource<string>();
            requestFactoryBuilder.SendRequestAsyncReturns(() => response.Task);
            var friends = CreateService();

            var operation = friends.RefreshFriendsAsync();
            var disposing = friends.DisposeAsync(waitForOngoingOperationsToFinish: true);
            response.SetResult(FriendsBody(Friend("1", "Alice")));
            await operation;
            await disposing;

            Assert.That(operation.IsCanceled, Is.False, "The request was cancelled even though the teardown asked for it to finish.");
            Assert.That(operation.IsCompletedSuccessfully, Is.True);
        }

        [Test]
        [Description("Disposing while the response is on its way must not refill the list Dispose just cleared.")]
        public async Task RefreshFriendsAsync_IsCanceled_WhenTheServiceIsDisposedMidRequest()
        {
            var response = new TaskCompletionSource<string>();
            requestFactoryBuilder.SendRequestAsyncReturns(() => response.Task);
            var friends = CreateService();

            var operation = friends.RefreshFriendsAsync();
            friends.Dispose();
            response.SetResult(FriendsBody(Friend("1", "Alice")));

            await operation;

            Assert.That(friends.Friends, Is.Empty, "A response that arrived after Dispose refilled the friend list.");
            Assert.That(operation.IsCanceled, Is.True);
        }

        [Test]
        public void SyncFriendsAsync_FailsWithTooManyRequests_WhenCloudRejectsTheRequest()
        {
            requestFactoryBuilder.OnSendRequestAsyncCalled(new RequestException(ErrorCode.FriendsRateLimited, HttpStatusCode.TooManyRequests));
            using var friends = CreateService();

            var operation = friends.SyncFriendsAsync("steam", new[] { "76561" });

            Assert.That(operation.HasFailed, Is.True);
            Assert.That(operation.Error.Type, Is.EqualTo(FriendsErrorType.TooManyRequests));
        }

        // Mirror of the wire body, so tests assert on field names without the internal DTO leaking out.
        private sealed class DecodedSync
        {
            [JsonProperty("source")] public string Source;
            [JsonProperty("friends")] public List<string> FriendIds;
        }
    }
}
