using Jobly.Api.Common;

namespace Jobly.Api.Features.Jobs;

/// <summary>All the expected failures for jobs, in one place so codes and messages stay consistent.</summary>
public static class JobErrors
{
    public static readonly Error NotFound = Error.NotFound("Job.NotFound", "The job was not found.");

    public static readonly Error TitleRequired = Error.Validation("Job.TitleRequired", "The job title is required.");

    public static readonly Error CompanyRequired = Error.Validation("Job.CompanyRequired", "The company is required.");

    public static readonly Error DescriptionRequired = Error.Validation("Job.DescriptionRequired", "The job description is required.");

    // A method (not a field) because the message names the two statuses. Conflict becomes HTTP 409.
    public static Error InvalidStatusChange(ApplicationStatus from, ApplicationStatus to) =>
        Error.Conflict("Job.InvalidStatusChange", $"A job cannot move from {from} to {to}.");
}
