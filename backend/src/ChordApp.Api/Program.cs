using ChordApp.Api.Extensions;
using ChordApp.Api.Middleware;
using ChordApp.Api.Services;
using ChordApp.Application.CorrectChord;
using ChordApp.Application.QueryMusic;
using ChordApp.Application.SubmitMusic;
using ChordApp.Infrastructure;
using ChordApp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<MusicDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database") ?? "Host=localhost;Database=chordapp;Username=chordapp;Password=chordapp"));
builder.Services.AddHttpClient("processor", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ProcessorUrl"] ?? "http://localhost:8000");
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddScoped<IMusicRepository, MusicRepository>();
builder.Services.AddScoped<IMusicSubmission<AudioFileSource>, AudioFileSourceUpload>();
builder.Services.AddScoped<IMusicSubmission<YouTubeSource>, MusicSubmissionYoutube>();
builder.Services.AddScoped<ListMusics>();
builder.Services.AddScoped<GetMusicDetails>();
builder.Services.AddScoped<SelectMusicModel>();
builder.Services.AddScoped<CorrectMusicChord>();
builder.Services.AddScoped<IProcessingProgressReader, ProcessorProgressReader>();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration["WebOrigin"] ?? "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()));

Directory.CreateDirectory(TempFiles.Root);
var app = builder.Build();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors();
await app.InitializeMusicDatabaseAsync();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapMusicEndpoints();
app.Run();

/// <summary>Ponto de entrada da API; declarado como partial para os testes HTTP.</summary>
public partial class Program { }
