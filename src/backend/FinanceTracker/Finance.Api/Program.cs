using System.Text.Json.Nodes;
using Finance.Api.Infrastructure.Validators;
using Finance.Business;
using Finance.Business.Services;
using Finance.Business.Services.Reports;
using Finance.Data.Repositories;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        var defaults = JsonSerializationOptions.Default;
        options.JsonSerializerOptions.PropertyNamingPolicy = defaults.PropertyNamingPolicy;
        options.JsonSerializerOptions.DefaultIgnoreCondition = defaults.DefaultIgnoreCondition;
        foreach (var converter in defaults.Converters)
        {
            options.JsonSerializerOptions.Converters.Add(converter);
        }
    });
builder.Services.AddSingleton<ICategoryRepository, InMemoryCategoryRepository>();
builder.Services.AddSingleton<ITransactionRepository, InMemoryTransactionRepository>();
builder.Services.AddSingleton<ICategoryService, CategoryService>();
builder.Services.AddSingleton<ITransactionService, TransactionService>();
builder.Services.AddSingleton<IReportStrategy, PeriodReportStrategy>();
builder.Services.AddSingleton<IReportStrategyFactory, ReportStrategyFactory>();
builder.Services.AddSingleton<IReportService, ReportService>();
builder.Services.AddSingleton<ITransactionValidator, TransactionValidator>();
builder.Services.AddSingleton<ICategoryValidator, CategoryValidator>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // Describe every enum as a string-typed schema with its named values. Without this,
    // Microsoft.AspNetCore.OpenApi emits enums as integers, which contradicts the JSON
    // pipeline (configured to reject integer enum values) and makes Scalar's auto-generated
    // request bodies fail on Send.
    options.AddSchemaTransformer((schema, context, _) =>
    {
        var type = context.JsonTypeInfo.Type;
        if (type.IsEnum)
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = null;
            schema.Enum = Enum.GetNames(type)
                .Select(name => (JsonNode)JsonValue.Create(name)!)
                .ToList();
        }
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
