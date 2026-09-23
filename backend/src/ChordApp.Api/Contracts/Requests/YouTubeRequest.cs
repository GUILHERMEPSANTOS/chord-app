namespace ChordApp.Api.Contracts.Requests;

/// <summary>Corpo HTTP com a URL de vídeo e o modelo inicial.</summary>
public sealed record YouTubeRequest(string Url, string Model = "lv-chordia");
