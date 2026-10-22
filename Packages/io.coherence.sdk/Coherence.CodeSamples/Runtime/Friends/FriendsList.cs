namespace Coherence.CodeSamples.Runtime.Friends
{
    #region Sample

    using Coherence.Cloud;
    using UnityEngine;

    class FriendsList : MonoBehaviour
    {
        async void Start()
        {
            // Wait until a player account has logged in to coherence Cloud.
            // The CoherenceCloudLogin component can be used for this.
            // Guest and username/password accounts cannot have friends.

            PlayerAccount playerAccount = await PlayerAccount.GetMainAsync();
            var friends = playerAccount.Services.Friends;

            friends.OnFriendAdded += friend => Debug.Log($"{friend.DisplayName} is now a friend.");
            friends.OnFriendRemoved += friend => Debug.Log($"{friend.DisplayName} is no longer a friend.");

            await friends.RefreshFriendsAsync()
                .OnFail(Debug.LogWarning)
                .OnSuccess(list => Debug.Log($"{list.Count} friend(s) also play this game."));
        }
    }

    #endregion
}
