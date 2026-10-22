namespace Coherence.CodeSamples.Runtime.PlayerAccount
{
    #region Sample

    using System.Threading.Tasks;
    using Coherence.Cloud;
    using UnityEngine;

    class LoginWithGameCenter : MonoBehaviour
    {
        // The credentials come from GameKit's fetchItems(forIdentityVerificationSignature:), which the
        // title fetches itself. Pass GKLocalPlayer.teamPlayerID (not gamePlayerID), and only call this
        // when GKPlayer.scopedIDsArePersistent() is true.
        public async Task<PlayerAccount> Login(string teamPlayerId, string bundleId, string publicKeyUrl,
            byte[] signature, byte[] salt, ulong timestamp)
        {
            var credentials = new GameCenterCredentials(teamPlayerId, bundleId, publicKeyUrl, signature, salt, timestamp);
            PlayerAccount playerAccount = await CoherenceCloud.LoginWithGameCenter(credentials);
            Debug.Log($"Logged in as: {playerAccount}.");
            return playerAccount;
        }
    }

    #endregion
}
