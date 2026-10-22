namespace Coherence.CodeSamples.Runtime.PlayerAccount
{
    #region Sample

    using Coherence.Cloud;
    using UnityEngine;

    class LoginWithSessionToken : MonoBehaviour
    {
        async void Start()
        {
            var sessionToken = SessionToken.Deserialize(PlayerPrefs.GetString("SessionToken", ""));
            var loginOperation = await CoherenceCloud.LoginWithSessionToken(sessionToken);
            if (!loginOperation.HasFailed)
            {
                var playerAccount = loginOperation.Result;
                PlayerPrefs.SetString("SessionToken", SessionToken.Serialize(playerAccount.SessionToken));
                Debug.Log($"Logged in as: {playerAccount}.");
            }
        }
    }

    #endregion
}
