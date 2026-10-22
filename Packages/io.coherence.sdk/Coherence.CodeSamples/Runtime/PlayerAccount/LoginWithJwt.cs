namespace Coherence.CodeSamples.Runtime.PlayerAccount
{
    #region Sample

    using System.Threading.Tasks;
    using Coherence.Cloud;
    using UnityEngine;

    class LoginWithJwt : MonoBehaviour
    {
        public async Task<PlayerAccount> Login(string token)
        {
            PlayerAccount playerAccount = await CoherenceCloud.LoginWithJwt(token);
            Debug.Log($"Logged in as: {playerAccount}.");
            return playerAccount;
        }
    }

    #endregion
}
