namespace Coherence.Editor
{
    using System.Collections.Generic;
    using UnityEditor.Compilation;

    [WatchdogTrigger]
    internal class WatchdogTriggerBakedCode : IWatchdogTrigger
    {
        private const string path = Paths.defaultSchemaBakePath + "/";

        public bool ListenForRecompile => true;
        public string Issue => "Baked Scripts have compilation errors";
        public string Fix => $"Delete '{Paths.defaultSchemaBakePath}' and show Network Prefabs window";

        public bool Validate(CompilerMessage compilerMessage)
            => compilerMessage.type == CompilerMessageType.Error &&
               compilerMessage.message
                   .Replace("\\", "/")
                   .StartsWith(path);

        public bool PerformFix(IReadOnlyList<CompilerMessage> compilerMessages)
            => CodeGenSelector.Clear(warn: false);

        public void OnRecompile(IReadOnlyList<CompilerMessage> compilerMessages)
            => CoherenceSyncObjectsStandaloneWindow.Open();
    }
}
