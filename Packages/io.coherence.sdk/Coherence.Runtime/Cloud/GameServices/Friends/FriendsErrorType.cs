// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Cloud
{
    /// <summary>What kind of problem a <see cref="FriendsService"/> request ran into.</summary>
    /// <remarks>
    /// Read it from <see cref="FriendsError"/>. The backend code behind it, when there was one, is on
    /// <see cref="FriendsError.RequestException"/>.
    /// </remarks>
    public enum FriendsErrorType
    {
        /// <summary>Nothing went wrong.</summary>
        None = 0,

        /// <summary>Something unforeseen went wrong on this side. Read the message.</summary>
        InternalException,

        /// <summary>coherence Cloud turned the request down, for a reason with no entry of its own here.</summary>
        RequestException,

        /// <summary>coherence Cloud answered with something that could not be read.</summary>
        InvalidResponse,

        /// <summary>The call was made with arguments it cannot be made with.</summary>
        InvalidInput,

        /// <summary>No player is logged in to coherence Cloud.</summary>
        NotLoggedIn,

        /// <summary>Too many requests went out in a row. Wait a moment and try again.</summary>
        TooManyRequests,

        /// <summary>coherence Cloud does not accept friend lists from that platform.</summary>
        UnsupportedSource,

        /// <summary>The player has no live connection to carry a presence.</summary>
        PresenceNotConnected,

        /// <summary>coherence Cloud rejected the presence, for example for being too long.</summary>
        PresenceInvalid,
    }
}
