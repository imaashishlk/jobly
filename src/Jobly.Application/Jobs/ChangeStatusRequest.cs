using Jobly.Domain.Jobs;

namespace Jobly.Application.Jobs;

/// <summary>What the client sends to move a job to another Kanban column.</summary>
public sealed class ChangeStatusRequest
{
    public required ApplicationStatus Status { get; init; }
}
