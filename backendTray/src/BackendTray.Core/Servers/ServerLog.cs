namespace BackendTray.Core.Servers;

/// <summary>把子程序的輸出寫到該伺服器自己的 log 檔。實作由服務提供（Serilog）。</summary>
public interface IServerLogWriterFactory
{
    IServerLogWriter Create(string serverId);
}

public interface IServerLogWriter : IDisposable
{
    void Write(string line);
}
