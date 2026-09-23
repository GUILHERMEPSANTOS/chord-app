namespace ChordApp.Application.SubmitMusic
{
    /// <summary>Contrato comum para registrar uma música a partir de uma origem.</summary>
    public interface IMusicSubmission<in TSource>
    {
        Task<SubmissionResult?> SubmitAsync(
            TSource source,
            CancellationToken cancellationToken);
    }
}



