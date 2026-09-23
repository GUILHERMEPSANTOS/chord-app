using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChordApp.Domain;
using ChordApp.Worker.Contracts;

namespace ChordApp.Worker.Jobs;

/// <summary>Envia arquivo ou URL ao processador Python e interpreta a resposta HTTP.</summary>
public sealed class ProcessorClient(IHttpClientFactory clients) : IProcessorClient
{
    public async Task<ProcessResult> AnalyzeAsync(
        Music music,
        string? audioPath,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromHours(6));
        using var response = music.SourceUrl is not null
            ? await SendYouTubeAsync(music, timeout.Token)
            : await SendFileAsync(music, audioPath!, timeout.Token);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            throw new ProcessorRejectedException(ReadProcessorError(body));
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProcessResult>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            timeout.Token) ?? throw new InvalidDataException("Resposta vazia do processador.");
    }

    private Task<HttpResponseMessage> SendYouTubeAsync(Music music, CancellationToken cancellationToken) =>
        clients.CreateClient("processor").PostAsJsonAsync(
            "/analyze-youtube",
            new { url = music.SourceUrl, jobId = music.Id.ToString() },
            cancellationToken);

    private async Task<HttpResponseMessage> SendFileAsync(
        Music music,
        string audioPath,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        await using var input = File.OpenRead(audioPath);
        using var content = new StreamContent(input);
        form.Add(content, "file", Path.GetFileName(audioPath));
        form.Add(new StringContent(music.Id.ToString()), "job_id");
        return await clients.CreateClient("processor").PostAsync("/analyze", form, cancellationToken);
    }

    private static string ReadProcessorError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var detail = document.RootElement.GetProperty("detail");
            if (detail.ValueKind == JsonValueKind.String && detail.GetString() is { Length: > 0 } message)
                return message.Length <= 300 ? message : message[..300];
        }
        catch (JsonException) { }
        catch (KeyNotFoundException) { }

        return "Não foi possível processar este áudio. Consulte os logs do processador.";
    }
}

/// <summary>Falha de entrada que o processador retornou como HTTP 422.</summary>
public sealed class ProcessorRejectedException(string message) : Exception(message);
