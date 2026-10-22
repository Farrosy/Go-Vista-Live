namespace Coherence.CodeSamples.Runtime.PlayerAccount
{
    #region Sample

    using System.Threading.Tasks;
    using Coherence.Cloud;
    using UnityEngine;

    class LoginWithSteam : MonoBehaviour
    {
        public async Task<PlayerAccount> Login(string ticket, string identity = null)
        {
            PlayerAccount playerAccount = await CoherenceCloud.LoginWithSteam(ticket, identity);
            Debug.Log($"Logged in as: {playerAccount}.");
            return playerAccount;
        }
    }

    #endregion
}
