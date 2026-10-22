namespace Coherence.CodeSamples.Runtime.CloudStorage
{
    #region Sample

    using System.Collections;
    using Coherence.Cloud;
    using UnityEngine;

    class StorageOperationExample : MonoBehaviour
    {
        public StorageObjectId storageObjectId = new("Greeter", 1);
        public ContinuationMethod continuationMethod = ContinuationMethod.Await;

        async void Start()
        {
            PlayerAccount playerAccount = await PlayerAccount.GetMainAsync();
            var cloudStorage = playerAccount.Services.CloudStorage;
            var saveOperation = cloudStorage.SaveObjectAsync(storageObjectId, "Hello, World!");

            switch (continuationMethod)
            {
                case ContinuationMethod.Await:
                    AsyncMethod(saveOperation);
                    return;
                case ContinuationMethod.Yield:
                    StartCoroutine(Coroutine(saveOperation));
                    return;
                case ContinuationMethod.ContinueWith:
                    ContinueWith(saveOperation);
                    return;
                case ContinuationMethod.OnSuccessAndFail:
                    OnSuccessAndFail(saveOperation);
                    return;
            }
        }

        IEnumerator Coroutine(StorageOperation saveOperation)
        {
            // Make the coroutine wait until the operation has completed.
            yield return saveOperation;

            LogResult(saveOperation);
        }

        async void AsyncMethod(StorageOperation saveOperation)
        {
            // Make the async method wait until the operation has completed.
            await saveOperation;

            LogResult(saveOperation);
        }

        void ContinueWith(StorageOperation saveOperation)
        {
            // Execute LogResult after the operation has completed.
            saveOperation.ContinueWith(LogResult);
        }

        void OnSuccessAndFail(StorageOperation saveOperation)
        {
            saveOperation
                // Execute an anonymous function if the operation completes successfully.
                .OnSuccess(() => Debug.Log($"Object '{storageObjectId}' saved in cloud storage successfully."))
                // Execute another anonymous function with the error if the operation fails.
                .OnFail(error => Debug.LogError(error));
        }

        void LogResult(StorageOperation completedSaveOperation)
        {
            if (completedSaveOperation.HasFailed)
            {
                Debug.LogError(completedSaveOperation.Error);
                return;
            }

            Debug.Log($"Object '{storageObjectId}' saved in cloud storage successfully.");
        }

        public enum ContinuationMethod
        {
            Await,
            Yield,
            ContinueWith,
            OnSuccessAndFail
        }
    }

    #endregion
}
