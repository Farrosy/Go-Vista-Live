namespace Coherence.CodeSamples.Runtime.PlayerAccount
{
    #region Sample

    using System.Threading.Tasks;
    using Coherence.Cloud;
    using UnityEngine;

    class AcquireOneTimeCode : MonoBehaviour
    {
        async void Start()
        {
            PlayerAccount playerAccount = await CoherenceCloud.LoginAsGuest();
            string code = await playerAccount.GetOneTimeCode();
            Debug.Log($"Login with this code on your other device: {code}.");
        }
    }

    class LoginWithOneTimeCode : MonoBehaviour
    {
        public async Task<PlayerAccount> Login(string code)
        {
            var loginOperation = await CoherenceCloud.LoginWithOneTimeCode(code);
            if (loginOperation.HasFailed)
            {
                Debug.LogWarning($"Logging in failed. The code may have already expired.\n{loginOperation.Error}.");
                return null;
            }

            var playerAccount = loginOperation.Result;
            Debug.Log($"Logged in as: {playerAccount}.");
            return playerAccount;
        }
    }

    #endregion
}
