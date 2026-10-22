namespace Coherence.CodeSamples.Runtime.CloudStorage
{
    #region Sample

    using Coherence.Cloud;
    using UnityEngine;

    class CloudStorageLoad : MonoBehaviour
    {
        async void Start()
        {
            PlayerAccount playerAccount = await PlayerAccount.GetMainAsync();
            var cloudStorage = playerAccount.Services.CloudStorage;
            await cloudStorage.LoadObjectAsync<string>(("type", "id"))
                .OnFail(Debug.LogWarning)
                .OnSuccess(text => Debug.Log($"Loaded: '{text}'"));
        }
    }

    #endregion
}
