using ChordApp.Infrastructure;
using ChordApp.Worker;
using ChordApp.Worker.Jobs;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddDbContext<MusicDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")
        ?? "Host=localhost;Database=chordapp;Username=chordapp;Password=chordapp"));
builder.Services.AddHttpClient("processor", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ProcessorUrl"] ?? "http://localhost:8000");
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddScoped<AnalysisJobRunner>();
builder.Services.AddScoped<InterruptedJobRecovery>();
builder.Services.AddScoped<IProcessorClient, ProcessorClient>();
builder.Services.AddScoped<AnalysisResultMapper>();
builder.Services.AddHostedService<AnalysisWorker>();

Directory.CreateDirectory(TempFiles.Root);
await builder.Build().RunAsync();
