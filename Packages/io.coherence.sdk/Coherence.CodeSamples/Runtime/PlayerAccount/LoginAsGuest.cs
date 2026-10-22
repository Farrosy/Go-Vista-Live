namespace Coherence.CodeSamples.Runtime.PlayerAccount
{
    #region Sample

    using Coherence.Cloud;
    using UnityEngine;

    class LoginAsGuest : MonoBehaviour
    {
        public PlayerAccount PlayerAccount { get; private set; }

        async void Start()
        {
            PlayerAccount = await CoherenceCloud.LoginAsGuest();
            Debug.Log($"Logged in as: {PlayerAccount}.");
        }
    }

    #endregion
}
