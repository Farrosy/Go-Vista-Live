// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using UnityEngine;

    /// <summary>
    /// Specifies different ways that a coherence bridge can automatically connect to coherence Cloud.
    /// </summary>
    public enum CoherenceBridgePlayerAccount
    {
        [InspectorName("Do Nothing")]
        None = 1,
        [InspectorName("Await Next Login Call")]
        Main = 2,
        [InspectorName("Login as Guest")]
        AutoLoginAsGuest = 3,
    }
}
