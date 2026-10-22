namespace Coherence.CodeSamples.Runtime.Friends
{
    #region Sample

    using Coherence.Cloud;
    using UnityEngine;

    class FriendsPresence : MonoBehaviour
    {
        async void Start()
        {
            // Wait until a player account has logged in to coherence Cloud.
            // The CoherenceCloudLogin component can be used for this.

            PlayerAccount playerAccount = await PlayerAccount.GetMainAsync();
            var friends = playerAccount.Services.Friends;

            friends.OnFriendPresenceChanged += friend =>
            {
                if (!friend.IsOnline)
                {
                    Debug.Log($"{friend.DisplayName} left. Last seen {friend.LastOnlineAt}.");
                    return;
                }

                Debug.Log($"{friend.DisplayName} is playing: {friend.Presence}");
            };

            await friends.RefreshFriendsAsync();
            await friends.SetPresenceAsync("in the lobby");

            foreach (var friend in friends.Friends)
            {
                if (friend.LobbyIds.Count == 0)
                {
                    continue;
                }

                await playerAccount.Services.Lobbies.JoinLobbyAsync(friend.LobbyIds[0]);
                break;
            }
        }
    }

    #endregion
}
