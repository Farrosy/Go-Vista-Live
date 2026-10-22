namespace Coherence.CodeSamples.Runtime.CloudStorage
{
    #region Sample

    using System.Collections;
    using Coherence.Cloud;
    using UnityEngine;

    class StorageOperationResultExample : MonoBehaviour
    {
        public StorageObjectId storageObjectId = new("Object", 1);
        public ContinuationMethod continuationMethod = ContinuationMethod.Await;

        async void Start()
        {
            PlayerAccount playerAccount = await PlayerAccount.GetMainAsync();
            var cloudStorage = playerAccount.Services.CloudStorage;
            var loadOperation = cloudStorage.LoadObjectAsync<string>(storageObjectId);

            switch (continuationMethod)
            {
                case ContinuationMethod.Await:
                    AsyncMethod(loadOperation);
                    return;
                case ContinuationMethod.Yield:
                    StartCoroutine(Coroutine(loadOperation));
                    return;
                case ContinuationMethod.ContinueWith:
                    ContinueWith(loadOperation);
                    return;
                case ContinuationMethod.OnSuccessAndFail:
                    OnSuccessAndFail(loadOperation);
                    return;
            }
        }

        IEnumerator Coroutine(StorageOperation<string> loadOperation)
        {
            // Make the coroutine wait until the operation has completed.
            yield return loadOperation;

            LogResult(loadOperation);
        }

        async void AsyncMethod(StorageOperation<string> loadOperation)
        {
            // Make the async method wait until the operation has completed.
            await loadOperation;

            LogResult(loadOperation);
        }

        void ContinueWith(StorageOperation<string> loadOperation)
        {
            // Execute LogResult after the operation has completed.
            loadOperation.ContinueWith(LogResult);
        }

        void OnSuccessAndFail(StorageOperation<string> loadOperation)
        {
            loadOperation
                // Execute an anonymous function with the result if the operation completes successfully.
                .OnSuccess(text => Debug.Log($"Loaded text: '{text}'."))
                // Execute another anonymous function with the error if the operation fails.
                .OnFail(error => Debug.LogError(error));
        }

        void LogResult(StorageOperation<string> completedLoadOperation)
        {
            if (completedLoadOperation.HasFailed)
            {
                Debug.LogWarning(completedLoadOperation.Error);
                return;
            }

            Debug.Log($"Loaded text: '{completedLoadOperation.Result}'.");
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
