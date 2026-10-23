namespace Jobly.Domain.Jobs;

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
