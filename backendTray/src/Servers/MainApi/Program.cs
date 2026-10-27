using BackendTray.Agent;
using MainApi.Data;

var builder = WebApplication.CreateBuilder(args);

// 由 BackendTray 服務啟動時接收關閉訊號；單獨 dotnet run 時不影響。
builder.AddTrayAgent();

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();

// SQL Server 資料來源：連線名稱 Default（server.json 的 connections）。
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddSingleton<ProductRepository>();

var app = builder.Build();

app.UseExceptionHandler();

// 正式環境也開啟 Swagger。API 預設只接受本機連線，所以只能在伺服器上用 http://localhost:{port}/swagger 開啟。
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
