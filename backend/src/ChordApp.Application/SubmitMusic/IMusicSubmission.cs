namespace ChordApp.Application.SubmitMusic
{
    public interface IMusicSubmission<in TSource>
    {
        Task<SubmissionResult> SubmitAsync(
            TSource source,
            CancellationToken cancellationToken);
    }
}



