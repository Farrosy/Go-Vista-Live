namespace Coherence.CodeSamples.Cloud.LobbiesService
{
    #region Sample

    using Coherence.Cloud;
    using UnityEngine;

    class GetLobbySessionsAsync : MonoBehaviour
    {
        async void Start()
        {
            PlayerAccount playerAccount = await PlayerAccount.GetMainAsync();
            var lobbyService = playerAccount.Services.Rooms.LobbyService;
            var lobbySessions = await lobbyService.GetLobbySessionsAsync();
            foreach (var lobbySession in lobbySessions)
            {
                Debug.Log($"Currently in lobby: {lobbySession.LobbyData.Name}");
            }
        }
    }

    #endregion
}
