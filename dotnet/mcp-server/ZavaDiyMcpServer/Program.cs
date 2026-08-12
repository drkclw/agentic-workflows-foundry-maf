using ZavaDiyMcpServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<InventoryTools>();

builder.Services.AddSingleton<IDatabaseService, DatabaseService>();

var app = builder.Build();

app.MapMcp();

app.Run();