namespace Coherence.CodeSamples.Runtime.PlayerAccount
{
    #region Sample

    using Coherence.Cloud;
    using UnityEngine;

    class LoginWithPassword : MonoBehaviour
    {
        [SerializeField] string username;
        [SerializeField] string password;
        [SerializeField] bool autoSignup;

        public PlayerAccount PlayerAccount { get; private set; }

        async void Start()
        {
            PlayerAccount = await CoherenceCloud.LoginWithPassword(username, password, autoSignup);
            Debug.Log($"Logged in as: {PlayerAccount}.");
        }
    }

    #endregion
}
