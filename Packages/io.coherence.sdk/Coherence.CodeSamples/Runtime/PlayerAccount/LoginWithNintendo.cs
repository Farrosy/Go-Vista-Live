namespace Coherence.CodeSamples.Runtime.PlayerAccount
{
    #region Sample

    using System.Threading.Tasks;
    using Coherence.Cloud;
    using UnityEngine;

    class LoginWithNintendo : MonoBehaviour
    {
        public async Task<PlayerAccount> Login(string token)
        {
            PlayerAccount playerAccount = await CoherenceCloud.LoginWithNintendo(token);
            Debug.Log($"Logged in as: {playerAccount}.");
            return playerAccount;
        }
    }

    #endregion
}
