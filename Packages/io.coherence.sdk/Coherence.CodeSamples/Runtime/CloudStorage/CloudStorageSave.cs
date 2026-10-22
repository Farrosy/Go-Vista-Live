namespace Coherence.CodeSamples.Runtime.CloudStorage
{
    #region Sample

    using Coherence.Cloud;
    using UnityEngine;

    class CloudStorageSave : MonoBehaviour
    {
        async void Start()
        {
            PlayerAccount playerAccount = await PlayerAccount.GetMainAsync();
            var cloudStorage = playerAccount.Services.CloudStorage;
            var objectId = ("Greeter", 1);
            await cloudStorage.SaveObjectAsync(objectId, "Hello, World!")
                .OnFail(Debug.LogWarning)
                .OnSuccess(() => Debug.Log("Saved"));
        }
    }

    #endregion
}
