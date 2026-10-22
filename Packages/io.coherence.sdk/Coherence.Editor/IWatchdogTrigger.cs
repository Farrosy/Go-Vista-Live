namespace Coherence.Editor
{
    using System.Collections.Generic;
    using UnityEditor.Compilation;

    internal interface IWatchdogTrigger
    {
        /// <seealso cref="OnRecompile"/>
        bool ListenForRecompile { get; }
        string Issue { get; }
        string Fix { get; }

        /// <returns><see langword="true"/> to associate the compiler message with this trigger - <see langword="false"/> otherwise</returns>
        bool Validate(CompilerMessage compilerMessage);

        /// <returns><see langword="true"/> to ask for a recompilation - <see langword="false"/> otherwise</returns>
        bool PerformFix(IReadOnlyList<CompilerMessage> compilerMessages);

        /// <seealso cref="ListenForRecompile"/>
        void OnRecompile(IReadOnlyList<CompilerMessage> compilerMessages);
    }
}
