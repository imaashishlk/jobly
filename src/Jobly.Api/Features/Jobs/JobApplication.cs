using Jobly.Api.Common;

namespace Jobly.Api.Features.Jobs;

/// <summary>
/// The user's tracking of one job: which Kanban column it is in, notes, and when they applied.
/// </summary>
public sealed class JobApplication
{
    // Only Job creates it, so an application can't exist without a job.
    internal JobApplication(Guid jobId, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        JobId = jobId;
        Status = ApplicationStatus.Saved;
        CreatedAt = now;
        UpdatedAt = now;
    }

    // Used by EF Core when it loads a row from the database.
    private JobApplication()
    {
    }

    public Guid Id { get; private set; }

    public Guid JobId { get; private set; }

    public ApplicationStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset? AppliedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Moves the application to another Kanban column, if the rules allow that move.</summary>
    public Result ChangeStatus(ApplicationStatus newStatus, DateTimeOffset now)
    {
        if (!CanMoveTo(newStatus)) return JobErrors.InvalidStatusChange(Status, newStatus);

        Status = newStatus;
        UpdatedAt = now;

        // There is no way back to Applied, so this is set only once.
        if (newStatus == ApplicationStatus.Applied) AppliedAt = now;

        return Result.Success();
    }

    /// <summary>Saves the user's notes. Empty text clears them.</summary>
    public void UpdateNotes(string? notes, DateTimeOffset now)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = now;
    }

    private bool CanMoveTo(ApplicationStatus newStatus) => Status switch
    {
        ApplicationStatus.Saved => newStatus is ApplicationStatus.PlanningToApply or ApplicationStatus.Applied or ApplicationStatus.Withdrawn,
        ApplicationStatus.PlanningToApply => newStatus is ApplicationStatus.Applied or ApplicationStatus.Withdrawn,
        ApplicationStatus.Applied => newStatus is ApplicationStatus.Interview or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Ghosted,
        ApplicationStatus.Interview => newStatus is ApplicationStatus.Offer or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Ghosted,

        // Final columns: nothing can follow. No `_` fallback, so a new status must be handled here or the build warns.
        ApplicationStatus.Offer or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Ghosted => false,
    };
}

/// <summary>The Kanban column a job is in.</summary>
public enum ApplicationStatus
{
    Saved,
    PlanningToApply,
    Applied,
    Interview,
    Offer,
    Rejected,
    Withdrawn,

    /// <summary>The user applied (or interviewed) but never got a reply.</summary>
    Ghosted,
}
