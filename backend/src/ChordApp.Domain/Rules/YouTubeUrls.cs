using System.Text.RegularExpressions;

namespace ChordApp.Domain.Rules;

public static class YouTubeUrls
{
    public static string? Canonical(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443)
            return null;

        var host = uri.Host.ToLowerInvariant();
        string? videoId = null;

        if (host is "youtu.be" or "www.youtu.be")
            videoId = uri.AbsolutePath.Trim('/');

        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com")
        {
            if (uri.AbsolutePath == "/watch")
                videoId = uri.Query
                   .TrimStart('?')
                   .Split('&')
                   .Select(p => p.Split('=', 2))
                   .FirstOrDefault(p => p.Length == 2 && p[0] == "v")
                   ?.ElementAtOrDefault(1);
            else if (uri.AbsolutePath.StartsWith("/shorts/"))
                videoId = uri.AbsolutePath[8..].Trim('/');
        }
        return videoId is not null && Regex.IsMatch(videoId, "^[A-Za-z0-9_-]{11}$")
               ? $"https://www.youtube.com/watch?v={videoId}" : null;
    }
}
