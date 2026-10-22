// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_EDITOR
namespace Coherence.Toolkit
{
    using UnityEditor;

    internal static class EditorAssetUtils
    {
        public static void MakeAssetEditable(string assetPath)
        {
            assetPath = NormalizePath(assetPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            _ = AssetDatabase.MakeEditable(assetPath);
        }

        private static string NormalizePath(string path) => path?.Replace('\\', '/');
    }
}
#endif
