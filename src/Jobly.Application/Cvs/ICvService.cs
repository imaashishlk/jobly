using Jobly.Domain.Common;

namespace Jobly.Application.Cvs;

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
