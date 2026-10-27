using Jobly.Api.Common;
using Jobly.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobly.Api.Features.Cvs;

/// <summary>The use cases for the user's CV.</summary>
public interface ICvService
{
    Task<Result<CvResponse>> GetCv(CancellationToken cancellationToken);

    /// <summary>Reads a PDF or DOCX file and makes its text the CV, replacing any earlier CV.</summary>
    Task<Result<CvResponse>> UploadCv(Stream content, string fileName, long length, CancellationToken cancellationToken);

    /// <summary>Saves pasted text as the CV, or replaces the text of the existing CV (for example after fixing a file's text).</summary>
    Task<Result<CvResponse>> SaveCvText(SaveCvTextRequest request, CancellationToken cancellationToken);

    Task<Result> DeleteCv(CancellationToken cancellationToken);
}

public sealed class CvService(JoblyDbContext dbContext, ICvTextExtractor textExtractor, TimeProvider timeProvider) : ICvService
{
    /// <summary>The largest CV file we accept (5 MB). The API uses the same number for its request size limit.</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly string[] SupportedExtensions = [".pdf", ".docx"];

    public async Task<Result<CvResponse>> GetCv(CancellationToken cancellationToken)
    {
        // There is only ever one CV. Tracked on purpose (no AsNoTracking) so SaveCvText can change it and save.
        var cv = await dbContext.Cvs.FirstOrDefaultAsync(cancellationToken);
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

        await SaveAsOnlyCv(created.Value, cancellationToken);

        return CvResponse.From(created.Value);
    }

    public async Task<Result<CvResponse>> SaveCvText(SaveCvTextRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.Cvs.FirstOrDefaultAsync(cancellationToken);

        // No CV yet: the pasted text becomes the CV.
        if (existing is null)
        {
            var created = Cv.Create(request.Text, CvSource.Pasted, fileName: null, now);
            if (created.IsFailure) return created.Error;

            await SaveAsOnlyCv(created.Value, cancellationToken);
            return CvResponse.From(created.Value);
        }

        // A CV exists: only its text changes. The file name and source stay, so an uploaded CV the user corrected still shows its file.
        var replaced = existing.ReplaceText(request.Text, now);
        if (replaced.IsFailure) return replaced.Error;

        await SaveAsOnlyCv(existing, cancellationToken);
        return CvResponse.From(existing);
    }

    public async Task<Result> DeleteCv(CancellationToken cancellationToken)
    {
        var cv = await dbContext.Cvs.FirstOrDefaultAsync(cancellationToken);
        if (cv is null) return CvErrors.NotFound;

        dbContext.Cvs.Remove(cv);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task SaveAsOnlyCv(Cv cv, CancellationToken cancellationToken)
    {
        // Make this CV the only one: every other row goes, and the new one is added. Both happen in one
        // transaction, so a failure cannot leave the user with no CV at all.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Cvs.Where(other => other.Id != cv.Id).ExecuteDeleteAsync(cancellationToken);

        // A CV loaded from the database is already tracked and only needs saving. A new one must be added.
        if (dbContext.Entry(cv).State == EntityState.Detached) dbContext.Cvs.Add(cv);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
