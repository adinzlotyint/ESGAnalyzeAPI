using ESGAnalyzeAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IParseService, ParseService>();
builder.Services.AddScoped<IAnalyzerService, AnalyzeService>();
builder.Services.AddSingleton<ICriterions, C1PolicyStrategyAnalyzer>();
builder.Services.AddSingleton<ICriterions, C2RisksAndChancesAnalyzer>();
builder.Services.AddSingleton<ICriterions, C3ClimateRiskAnalyzer>();
builder.Services.AddSingleton<ICriterions, C4EmissionsScopeAnalyzer>();
builder.Services.AddSingleton<ICriterions, C5EmissionsBoundaryAnalyzer>();
builder.Services.AddSingleton<ICriterions, C6CalculationStandardAnalyzer>();
builder.Services.AddSingleton<ICriterions, C7GwpSourcesAnalyzer>();
builder.Services.AddSingleton<ICriterions, C8EmissionsTrendAnalyzer>();
builder.Services.AddSingleton<ICriterions, C9IntensityIndicatorAnalyzer>();
builder.Services.AddSingleton<ICriterions, C10NumericConsistencyAnalyzer>();
builder.Services.AddSingleton<ICriterions, C11UnitCorrectnessAnalyzer>();
builder.Services.AddSingleton<ICriterions, C12KeywordPresenceAnalyzer>();

var app = builder.Build();

if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapEndpoints();

app.Run();
