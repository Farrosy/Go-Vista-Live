// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Runtime.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Coherence.Cloud;
    using Coherence.Tests;

    public abstract class FriendsServiceTestBase : CoherenceTest
    {
        private protected const string UpdatePush = "/friends/update";
        private protected const string PresencePush = "/friends/presence";

        private protected MockAuthClientBuilder authClientBuilder;
        private protected MockRequestFactoryBuilder requestFactoryBuilder;

        private CloudCredentialsPair CloudCredentialsPair
            => new(authClientBuilder.AuthClient, requestFactoryBuilder.RequestFactory);

        public override void SetUp()
        {
            base.SetUp();
            authClientBuilder = new();
            requestFactoryBuilder = new();

            Calls.Clear();
            Response = null;
            ResponseTask = null;
        }

        private protected FriendsService CreateService() => new(CloudCredentialsPair, logger: logger);

        private protected async Task<FriendsService> ServiceKnowing(params string[] knownFriends)
        {
            RecordCalls(FriendsBody(knownFriends));
            var friends = CreateService();
            await friends.RefreshFriendsAsync();
            Calls.Clear();
            return friends;
        }

        private protected List<(string path, string method, string body)> Calls { get; } = new();
        private protected string Response;
        private protected Func<Task<string>> ResponseTask;

        // Hangs the next request, as one from a socket that has gone does. Complete the source to
        // answer it and hand the hook back.
        private protected TaskCompletionSource<string> HoldNextResponse()
        {
            var held = new TaskCompletionSource<string>();
            ResponseTask = () =>
            {
                ResponseTask = null;
                return held.Task;
            };

            return held;
        }

        private protected List<(string path, string method, string body)> RecordCalls(string response = null)
        {
            Response = response ?? FriendsBody();
            requestFactoryBuilder.SendRequestAsyncReturns((basePath, pathParams, method, body, _, _, _) =>
            {
                Calls.Add((basePath + pathParams, method, body));
                return ResponseTask is null ? Task.FromResult(Response) : ResponseTask();
            });
            return Calls;
        }

        private protected static string Friend(string id, string displayName)
            => "{\"player_id\":\"" + id + "\",\"display_name\":\"" + displayName +
               "\",\"source_ids\":{\"steam\":\"76561\"}}";

        private protected static string FriendsBody(params string[] friends)
            => "{\"friends\":[" + string.Join(",", friends) + "]}";
    }
}
