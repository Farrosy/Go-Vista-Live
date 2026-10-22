// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Log.Targets
{
    using System;
    using System.Text;

    public class ConsoleTarget : ILogTarget
    {
        public LogLevel Level { get; set; }

        private static readonly object locker = new object();

        private ILogFormatter formatter;

        public ConsoleTarget(ILogFormatter formatter)
        {
            this.formatter = formatter;
        }

        public void Log(LogLevel level, string message, (string key, object value)[] args, Logger logger)
        {
            var result = formatter.Format(level, message, args, logger.Source, logger.UseWatermark);

            try
            {
                if (level >= LogLevel.Error)
                {
                    lock (locker)
                    {
                        Console.Error.WriteLine(result);
                    }
                }
                else
                {
                    lock (locker)
                    {
                        Console.WriteLine(result);
                    }
                }
            }
#pragma warning disable CS0168
            catch (System.IO.IOException _)
#pragma warning restore CS0168
            {
                // There could have been a file IO exception here because the console closed just before the log was written.
                // https://github.com/coherence/unity/issues/8342
            }
        }

        public void Dispose()
        {
        }
    }
}
