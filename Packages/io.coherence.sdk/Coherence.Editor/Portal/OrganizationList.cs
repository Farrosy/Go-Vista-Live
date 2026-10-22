// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using System;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.Networking;
    using System.Net;
    using System.Threading.Tasks;

    [Serializable]
    internal class OrganizationList : IEquatable<OrganizationList>
    {
        public Organization[] orgs;
        public HttpStatusCode ResponseCode;

        private OrganizationList()
        {
        }

        public static async Task<OrganizationList> Fetch(ShouldWait shouldWait = ShouldWait.InBatchMode)
        {
            using var req = new PortalRequest(Endpoints.organizationsPath, "GET", autoDispose: false, logResults: false);
            req.downloadHandler = new DownloadHandlerBuffer();
            _ = req.SendWebRequest();
            if (!BatchModeUtils.WaitIf(shouldWait, req))
            {
                while (!req.isDone)
                {
                    await Task.Yield();
                }
            }

            OrganizationList orgList;
            switch (req.result)
            {
                case UnityWebRequest.Result.ProtocolError:
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log($"Error fetching organization list: {req.error}");
                    orgList = new OrganizationList();
                    orgList.ResponseCode = (HttpStatusCode)req.responseCode;
                    return orgList;
            }
            orgList = JsonUtility.FromJson<OrganizationList>(req.downloadHandler.text);
            orgList.ResponseCode = (HttpStatusCode)req.responseCode;
            return orgList;
        }

        public bool Equals(OrganizationList other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (orgs is null)
            {
                return other.orgs is null && ResponseCode == other.ResponseCode;
            }

            return other.orgs is not null && orgs.SequenceEqual(other.orgs) && ResponseCode == other.ResponseCode;
        }

        public override bool Equals(object obj) => Equals(obj as OrganizationList);
        public override int GetHashCode() => HashCode.Combine(orgs, (int)ResponseCode);
        public override string ToString() => $"OrganizationList({(orgs is null ? "null" : string.Join(", ", orgs.Select(x => x.ToString())))}, ResponseCode:{ResponseCode})";
    }
}

