using BackendTray.Contracts;
using BackendTray.Core;
using BackendTray.Core.Database;
using BackendTray.Core.Servers;
using BackendTray.Core.Settings;
using BackendTray.Service.Logging;
using BackendTray.Service.Management;
using BackendTray.Service.Setup;
using BackendTray.Service.Workers;
using Serilog;
using Serilog.Events;

// install / uninstall / stop-for-update 由安裝程式呼叫，執行完就結束。
if (SetupCommands.TryRun(args, out var exitCode))
    return exitCode;

var paths = AppPaths.FromEnvironment();

// 服務的工作目錄是 System32，所以內容根目錄要明確指定。
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddWindowsService(options => options.ServiceName = ManagementConstants.ServiceName);
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(25));
builder.Services.AddSerilog(logger => logger
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(paths.LogsDirectory, "service-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30));

// 管理 API 只透過 named pipe 提供，不佔 TCP port，也不對網路開放。
builder.WebHost.UseNamedPipes(options =>
{
    options.CurrentUserOnly = false;
    options.PipeSecurity = ManagementPipe.CreateSecurity();
});
builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenNamedPipe(ManagementConstants.PipeName));
builder.Services.ConfigureHttpJsonOptions(options => ManagementJson.Configure(options.SerializerOptions));

builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<SecretProtector>();
builder.Services.AddSingleton<SettingsMapper>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<ConnectionStringFactory>();
builder.Services.AddSingleton<ConnectionTester>();
builder.Services.AddSingleton<ServerCatalog>();
builder.Services.AddSingleton<LaunchPlanner>();
builder.Services.AddSingleton<IServerLogWriterFactory, SerilogServerLogWriterFactory>();
builder.Services.AddSingleton<ServerSupervisor>();
builder.Services.AddSingleton<ServiceInfo>();
builder.Services.AddHostedService<SupervisorWorker>();

var app = builder.Build();
app.MapManagementApi();
app.Run();
return 0;
