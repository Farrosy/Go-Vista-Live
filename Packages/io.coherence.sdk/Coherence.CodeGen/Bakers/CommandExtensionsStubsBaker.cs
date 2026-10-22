namespace Coherence.CodeGen
{
    using System.IO;

    internal class CommandExtensionsStubsBaker : CommandExtensionsBaker
    {
        protected override string Template => "baked_command_extensions_stub";
        protected override string ClassNamePostfix => "_Stub";

        protected override string ProcessOutputDirectory(string outputDirectory)
        {
            // New path alongside the original output directory,
            // with "_stubs" appended to the name
            // Assets/coherence/bake -> Assets/coherence/bake_stubs
            var dir = new DirectoryInfo(outputDirectory);
            return dir.Parent?.CreateSubdirectory(dir.Name + "_stubs").FullName ?? outputDirectory;
        }
    }
}
