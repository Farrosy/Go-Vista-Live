// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using UnityEditor;

    /// <summary>
    /// Provides information about the current state of the Unity Editor.
    /// </summary>
    /// <remarks>
    /// Like <see cref="EditorApplication"/> but unit-testing friendly.
    /// </remarks>
    internal class EditorStatus
    {
        public virtual bool IsCompiling => EditorApplication.isCompiling;
    }
}
