using BackendTray.Core;
using BackendTray.Core.Servers;
using Serilog;

namespace BackendTray.Service.Logging;

/// <summary>每個伺服器的輸出寫到 logs\{id}-yyyyMMdd.log，保留 30 天。</summary>
internal sealed class SerilogServerLogWriterFactory(AppPaths paths) : IServerLogWriterFactory
{
    public IServerLogWriter Create(string serverId)
    {
        var logger = new LoggerConfiguration()
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, $"{serverId}-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Message:l}{NewLine}")
            .CreateLogger();
        return new Writer(logger);
    }

    private sealed class Writer(Serilog.Core.Logger logger) : IServerLogWriter
    {
        public void Write(string line) => logger.Information("{Line:l}", line);

        public void Dispose() => logger.Dispose();
    }
}
