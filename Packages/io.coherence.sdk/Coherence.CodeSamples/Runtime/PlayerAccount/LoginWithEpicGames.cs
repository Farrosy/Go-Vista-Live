// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.CodeSamples.Runtime.PlayerAccount
{
    #region Sample

    using System.Threading.Tasks;
    using Coherence.Cloud;
    using UnityEngine;

    class LoginWithEpicGames : MonoBehaviour
    {
        public async Task<PlayerAccount> Login(string token)
        {
            PlayerAccount playerAccount = await CoherenceCloud.LoginWithEpicGames(token);
            Debug.Log($"Logged in as: {playerAccount}.");
            return playerAccount;
        }
    }

    #endregion
}
