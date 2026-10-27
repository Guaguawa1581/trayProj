using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace MainApi.Controllers;

/// <summary>確認 API 是否正常、由誰啟動、拿到了哪些連線設定（只列名稱，不回傳內容）。</summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController(IConfiguration configuration, IHostEnvironment environment) : ControllerBase
{
    [HttpGet("info")]
    public IActionResult Info() => Ok(new
    {
        version = typeof(SystemController).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        environment = environment.EnvironmentName,
        managedByBackendTray = Environment.GetEnvironmentVariable("BACKENDTRAY_MANAGED") == "1",
        configuredConnections = configuration.GetSection("ConnectionStrings").GetChildren()
            .Where(section => !string.IsNullOrEmpty(section.Value))
            .Select(section => section.Key),
    });
}
