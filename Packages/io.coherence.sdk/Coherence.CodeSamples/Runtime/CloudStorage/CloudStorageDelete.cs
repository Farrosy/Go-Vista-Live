namespace Coherence.CodeSamples.Runtime.CloudStorage
{
    #region Sample

    using Coherence.Cloud;
    using UnityEngine;

    class CloudStorageDelete : MonoBehaviour
    {
        async void Start()
        {
            PlayerAccount playerAccount = await PlayerAccount.GetMainAsync();
            var cloudStorage = playerAccount.Services.CloudStorage;
            await cloudStorage.DeleteObjectAsync(("type", "id"))
                .OnFail(Debug.LogWarning)
                .OnSuccess(() => Debug.Log("Deleted"));
        }
    }

    #endregion
}
