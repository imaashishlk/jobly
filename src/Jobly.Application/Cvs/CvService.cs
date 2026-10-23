using Jobly.Domain.Common;
using Jobly.Domain.Cvs;

namespace Jobly.Application.Cvs;

public sealed class CvService(ICvRepository cvRepository, ICvTextExtractor textExtractor, TimeProvider timeProvider) : ICvService
{
    /// <summary>The largest CV file we accept (5 MB). The API uses the same number for its request size limit.</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly string[] SupportedExtensions = [".pdf", ".docx"];

    public async Task<Result<CvResponse>> GetCv(CancellationToken cancellationToken)
    {
        var cv = await cvRepository.Get(cancellationToken);
        if (cv is null) return CvErrors.NotFound;

        return CvResponse.From(cv);
    }

    public async Task<Result<CvResponse>> UploadCv(Stream content, string fileName, long length, CancellationToken cancellationToken)
    {
        // The cheap checks come first, so an unsuitable file is refused before anyone reads its bytes.
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension)) return CvErrors.UnsupportedFileType;
        if (length > MaxFileBytes) return CvErrors.FileTooLarge;

        var extracted = await textExtractor.Extract(content, fileName, cancellationToken);
        if (extracted.IsFailure) return extracted.Error;

        // A file can be readable and still hold no words (an empty document). Same message as a scanned PDF.
        if (string.IsNullOrWhiteSpace(extracted.Value)) return CvErrors.NoTextFound;

        // Path.GetFileName keeps only the name, in case a browser sends a full path.
        var created = Cv.Create(extracted.Value, CvSource.Uploaded, Path.GetFileName(fileName), timeProvider.GetUtcNow());
        if (created.IsFailure) return created.Error;

        await cvRepository.Save(created.Value, cancellationToken);

        return CvResponse.From(created.Value);
    }

    public async Task<Result<CvResponse>> SaveCvText(SaveCvTextRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var existing = await cvRepository.Get(cancellationToken);

        // No CV yet: the pasted text becomes the CV.
        if (existing is null)
        {
            var created = Cv.Create(request.Text, CvSource.Pasted, fileName: null, now);
            if (created.IsFailure) return created.Error;

            await cvRepository.Save(created.Value, cancellationToken);
            return CvResponse.From(created.Value);
        }

        // A CV exists: only its text changes. The file name and source stay, so an uploaded CV the user corrected still shows its file.
        var replaced = existing.ReplaceText(request.Text, now);
        if (replaced.IsFailure) return replaced.Error;

        await cvRepository.Save(existing, cancellationToken);
        return CvResponse.From(existing);
    }

    public async Task<Result> DeleteCv(CancellationToken cancellationToken)
    {
        var cv = await cvRepository.Get(cancellationToken);
        if (cv is null) return CvErrors.NotFound;

        await cvRepository.Delete(cv, cancellationToken);

        return Result.Success();
    }
}
