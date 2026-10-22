namespace Coherence.CodeSamples.Runtime.Friends
{
    #region Sample

    using Coherence.Cloud;
    using UnityEngine;

    class FriendsSync : MonoBehaviour
    {
        async void Start()
        {
            // Wait until a player account has logged in to coherence Cloud.
            // The CoherenceCloudLogin component can be used for this.
            // Guest and username/password accounts cannot have friends.

            PlayerAccount playerAccount = await PlayerAccount.GetMainAsync();
            var friends = playerAccount.Services.Friends;

            // Read the player's friend list from wherever you keep it yourself.
            // Here we're using placeholder ids, coming from the game's own backend.
            var myFriends = new[] { "player-1234567890", "player-9876543210" };

            await friends.SyncFriendsAsync("custom", myFriends)
                .OnFail(Debug.LogWarning)
                .OnSuccess(result => Debug.Log($"{result.Resolved} of them play this game."));

            // A friend shows up only once both players have synced each other.
            foreach (var friend in friends.Friends)
            {
                Debug.Log(friend.DisplayName);
            }
        }
    }

    #endregion
}
