// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using System;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.Networking;

    [Serializable]
    internal class UnlockToken
    {
#pragma warning disable 649
        public string token;
#pragma warning restore 649

        private UnlockToken() { }

        public static async Task<string> FetchAsync()
        {
            using var req = new PortalRequest(Endpoints.unlockTokenPath, "GET", autoDispose: false, logResults: false);
            req.downloadHandler = new DownloadHandlerBuffer();
            _ = req.SendWebRequest();
            while (!req.isDone)
            {
                await Task.Yield();
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                return null;
            }

            return JsonUtility.FromJson<UnlockToken>(req.downloadHandler.text)?.token;
        }
    }
}