// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.ProtocolDef
{
    public class ManagedTypesTables
    {
        public HandleTable<string> StringsTable = new();
        public HandleTable<byte[]> ByteArraysTable = new();
    }
}
