// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    public struct AuthorityChangedEventArgs
    {
        public ICoherenceSync Sync;
        public AuthorityType OldAuthority;
        public AuthorityType NewAuthority;
    }
}
