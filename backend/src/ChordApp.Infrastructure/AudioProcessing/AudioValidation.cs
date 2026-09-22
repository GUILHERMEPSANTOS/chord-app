using System.Diagnostics;


namespace ChordApp.Api;

public static class AudioValidation
{
    public static async Task<bool> HasSignatureAsync(string path, string ext, CancellationToken cancellation)
    {
        var bytes = new byte[12];

        await using var stream = File.OpenRead(path);

        if (await stream.ReadAsync(bytes, cancellation) < 12)
            return false;

        return ext == ".wav"
            ? bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8)
            : bytes.AsSpan(0, 3).SequenceEqual("ID3"u8) || (bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0);
    }

    public static async Task<double?> DurationAsync(string path, CancellationToken cancellation)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("ffprobe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        foreach (var arg in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", path })
            process.StartInfo.ArgumentList.Add(arg);

        try
        {
            process.Start();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);

            return process.ExitCode == 0 && double.TryParse(output.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var duration) && double.IsFinite(duration) 
                ? duration 
                : null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(); return null;
        }
    }
}
