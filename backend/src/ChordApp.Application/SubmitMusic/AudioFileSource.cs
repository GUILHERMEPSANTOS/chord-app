namespace ChordApp.Application.SubmitMusic
{
    /// <summary>Dados do arquivo enviado pela rota HTTP para o caso de uso de upload.</summary>
    public sealed record AudioFileSource(
        Stream Content,
        string FileName,
        long Length,
        string Model = "lv-chordia");
}
