using Jobly.Api.Common;
using Jobly.Application.Cvs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jobly.Api.Endpoints.Cvs;

public static class CvEndpoints
{
    // Called once from Program.cs: app.MapCvEndpoints();
    public static void MapCvEndpoints(this IEndpointRouteBuilder app)
    {
        var cv = app.MapGroup("/api/cv")
            .WithTags("CV")
            // TEMPORARY: there is no sign-in yet, so the whole group is public. The auth plan replaces this with
            // .RequireAuthorization() and ties the CV to its owner (see .claude/rules/security.md).
            .AllowAnonymous();

        cv.MapGet("/", GetCv);
        cv.MapPut("/text", SaveCvText);
        cv.MapDelete("/", DeleteCv);

        cv.MapPost("/file", UploadCv)
            // Antiforgery tokens protect cookie-based sign-in against forged forms. There is no cookie sign-in yet,
            // and without this call ASP.NET Core refuses every file upload. The auth plan revisits it.
            .DisableAntiforgery();
    }

    private static async Task<Results<Ok<CvResponse>, ProblemHttpResult>> GetCv(ICvService cvService, CancellationToken cancellationToken)
    {
        var result = await cvService.GetCv(cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    // IFormFile is the uploaded file. The form field must be named "file".
    private static async Task<Results<Ok<CvResponse>, ProblemHttpResult>> UploadCv(
        IFormFile file,
        ICvService cvService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var result = await cvService.UploadCv(content, file.FileName, file.Length, cancellationToken);
        if (result.IsFailure) return result.Error.ToProblem();

        // Shows when a CV was replaced and how big it was. Never the file name or the text: they hold personal data.
        logger.LogInformation("CV uploaded ({CharacterCount} characters)", result.Value.CharacterCount);

        return TypedResults.Ok(result.Value);
    }

    private static async Task<Results<Ok<CvResponse>, ProblemHttpResult>> SaveCvText(
        SaveCvTextRequest request,
        ICvService cvService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var result = await cvService.SaveCvText(request, cancellationToken);
        if (result.IsFailure) return result.Error.ToProblem();

        // Same idea as the upload: size and source only, never the text.
        logger.LogInformation("CV text saved ({CharacterCount} characters, source {Source})", result.Value.CharacterCount, result.Value.Source);

        return TypedResults.Ok(result.Value);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteCv(
        ICvService cvService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var result = await cvService.DeleteCv(cancellationToken);
        if (result.IsFailure) return result.Error.ToProblem();

        // Deleting can't be undone, so it is always logged.
        logger.LogInformation("CV deleted");

        return TypedResults.NoContent();
    }
}
