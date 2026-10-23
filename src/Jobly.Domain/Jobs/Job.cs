using Jobly.Domain.Common;

namespace Jobly.Domain.Jobs;

/// <summary>
/// A job posting, saved from Arbeidsplassen or entered by the user.
/// </summary>
public sealed class Job
{
    // Private: the only way to get a Job is Create, so an invalid Job can't exist.
    private Job(string title, string company, string? location, string description, string? url, JobSource source, string? externalId, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Title = title;
        Company = company;
        Location = location;
        Description = description;
        Url = url;
        Source = source;
        ExternalId = externalId;
        CreatedAt = now;
        Application = new JobApplication(Id, now);
    }

    // Used by EF Core when it loads a row from the database.
    private Job()
    {
        Title = string.Empty;
        Company = string.Empty;
        Description = string.Empty;
        Application = null!;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public string Company { get; private set; }

    public string? Location { get; private set; }

    public string Description { get; private set; }

    public string? Url { get; private set; }

    public JobSource Source { get; private set; }

    /// <summary>The job's id at the source (for example the Arbeidsplassen ad id). Null for manual jobs.</summary>
    public string? ExternalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public JobApplication Application { get; private set; }

    /// <summary>Creates a job together with its application in the Saved column.</summary>
    public static Result<Job> Create(
        string title,
        string company,
        string? location,
        string description,
        string? url,
        JobSource source,
        string? externalId,
        DateTimeOffset now)
    {
        var error = ValidateRequiredFields(title, company, description);
        if (error is not null) return error;

        return new Job(title.Trim(), company.Trim(), location?.Trim(), description.Trim(), url?.Trim(), source, externalId?.Trim(), now);
    }

    /// <summary>
    /// Edits the fields the user can change. Source and ExternalId stay as they were, because they say where the job came from.
    /// If a field is invalid nothing is changed.
    /// </summary>
    public Result Update(string title, string company, string? location, string description, string? url)
    {
        var error = ValidateRequiredFields(title, company, description);
        if (error is not null) return error;

        Title = title.Trim();
        Company = company.Trim();
        Location = location?.Trim();
        Description = description.Trim();
        Url = url?.Trim();

        return Result.Success();
    }

    // Shared by Create and Update so both enforce the same rules. Returns null when everything is valid.
    private static Error? ValidateRequiredFields(string title, string company, string description) => (title, company, description) switch
    {
        _ when string.IsNullOrWhiteSpace(title) => JobErrors.TitleRequired,
        _ when string.IsNullOrWhiteSpace(company) => JobErrors.CompanyRequired,
        _ when string.IsNullOrWhiteSpace(description) => JobErrors.DescriptionRequired,
        _ => null,
    };
}
