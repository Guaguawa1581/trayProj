using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BackendTray.Agent;

public static class TrayAgentExtensions
{
    public const string ManagedVariable = "BACKENDTRAY_MANAGED";

    /// <summary>
    /// 由 BackendTray 服務啟動時，讓這個程式可以被優雅地停止。單獨執行（dotnet run）時不做任何事。
    /// </summary>
    public static TBuilder AddTrayAgent<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        if (Environment.GetEnvironmentVariable(ManagedVariable) != "1")
            return builder;

        // 服務以 UTF-8 讀取子程序輸出；明確指定編碼，中文 log 才不會因系統代碼頁變成亂碼。
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        if (Console.IsOutputRedirected)
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
        if (Console.IsErrorRedirected)
            Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });

        builder.Services.AddHostedService<ShutdownListener>();
        return builder;
    }
}
