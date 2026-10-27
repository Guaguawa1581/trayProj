using System.Text.Json.Serialization;

namespace BackendTray.Core.Servers;

/// <summary>
/// servers\{資料夾}\server.json：描述一個 Web API，隨程式一起安裝與更新。
/// 新增 API 時只要多一個資料夾與這個檔案，服務就會自動找到它。
/// </summary>
public sealed class ServerManifest
{
    /// <summary>唯一識別碼，只能用小寫英數與連字號，例如 main-api。</summary>
    public string Id { get; set; } = "";

    public string DisplayName { get; set; } = "";

    /// <summary>相對於 server.json 所在資料夾的執行檔名稱。</summary>
    public string Executable { get; set; } = "";

    public int DefaultPort { get; set; }

    /// <summary>Gateway 轉發到這個 API 的路徑前綴，例如 /api。</summary>
    public string RoutePrefix { get; set; } = "/api";

    public string HealthPath { get; set; } = "/health";

    /// <summary>此 API 需要的連線名稱，會以 ConnectionStrings__{名稱} 環境變數傳入。</summary>
    public List<string> Connections { get; set; } = ["Default"];

    [JsonIgnore]
    public string Directory { get; set; } = "";

    [JsonIgnore]
    public string ExecutablePath => Path.Combine(Directory, Executable);
}
