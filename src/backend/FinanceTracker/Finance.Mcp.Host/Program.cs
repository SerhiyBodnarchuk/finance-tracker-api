using Finance.Business.Services;
using Finance.Data.Repositories;
using Finance.Mcp;
using Finance.Mcp.Host.Resources;
using Finance.Mcp.Host.Tools;
using Finance.Mcp.Logging;
using Finance.Mcp.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Route all host logging to stderr so stdout stays clean for the MCP stdio protocol
builder.Logging.AddConsole(options =>
    options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<ICategoryRepository, InMemoryCategoryRepository>();
builder.Services.AddSingleton<ITransactionRepository, InMemoryTransactionRepository>();
builder.Services.AddSingleton<ICategoryService, CategoryService>();
builder.Services.AddSingleton<ITransactionService, TransactionService>();

builder.Services.AddSingleton<IMcpContextStore, McpContextStore>();
builder.Services.AddSingleton<IContextRedactor, ContextRedactor>();
builder.Services.AddSingleton<IMcpIterationLogger, McpIterationLogger>();
builder.Services.AddSingleton<Finance.Mcp.IMcpServer>(sp => new McpServer(
    sp.GetRequiredService<IMcpContextStore>(),
    sp.GetRequiredService<IContextRedactor>(),
    sp.GetRequiredService<IMcpIterationLogger>(),
    sp.GetRequiredService<ICategoryService>(),
    sp.GetRequiredService<ITransactionService>()));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<FinanceMcpTools>()
    .WithResources<CategoryMcpResources>()
    .WithResources<TransactionMcpResources>();

await builder.Build().RunAsync();
