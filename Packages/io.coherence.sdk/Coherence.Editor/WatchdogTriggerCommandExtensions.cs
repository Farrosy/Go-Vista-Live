namespace Coherence.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Coherence.Toolkit;
    using UnityEditor.Compilation;
    using UnityEngine;

    [WatchdogTrigger]
    internal class WatchdogTriggerCommandExtensions : IWatchdogTrigger
    {
        private const string path = Paths.defaultSchemaBakeStubsPath + "/";

        public bool ListenForRecompile => false;
        public string Issue => "Compilation errors in generated stubs";
        public string Fix => "Delete conflicting stubs";

        public bool Validate(CompilerMessage compilerMessage)
            => compilerMessage.type == CompilerMessageType.Error &&
               compilerMessage.message
                   .Replace("\\", "/")
                   .StartsWith(path);

        public bool PerformFix(IReadOnlyList<CompilerMessage> compilerMessages)
        {
            try
            {
                foreach (var compilerMessage in compilerMessages)
                {
                    EditorAssetUtils.MakeAssetEditable(compilerMessage.file);
                    File.Delete(compilerMessage.file);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return true;
        }

        public void OnRecompile(IReadOnlyList<CompilerMessage> compilerMessages)
        {
        }
    }
}
