// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log.Targets
{
    public interface ILogFormatter
    {
        string Format(LogLevel level, string message, (string key, object value)[] args, System.Type source, bool includeWatermark);
    }
}
