using Jobly.Api.Features.Jobs;

namespace Jobly.Api.UnitTests.Features.Jobs;

public sealed class JobApplicationTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Created.AddDays(2);

    // The allowed moves, written out in full so the test is a readable copy of the rules.
    private static readonly (ApplicationStatus From, ApplicationStatus To)[] AllowedMoves =
    [
        (ApplicationStatus.Saved, ApplicationStatus.PlanningToApply),
        (ApplicationStatus.Saved, ApplicationStatus.Applied),
        (ApplicationStatus.Saved, ApplicationStatus.Withdrawn),
        (ApplicationStatus.PlanningToApply, ApplicationStatus.Applied),
        (ApplicationStatus.PlanningToApply, ApplicationStatus.Withdrawn),
        (ApplicationStatus.Applied, ApplicationStatus.Interview),
        (ApplicationStatus.Applied, ApplicationStatus.Rejected),
        (ApplicationStatus.Applied, ApplicationStatus.Withdrawn),
        (ApplicationStatus.Applied, ApplicationStatus.Ghosted),
        (ApplicationStatus.Interview, ApplicationStatus.Offer),
        (ApplicationStatus.Interview, ApplicationStatus.Rejected),
        (ApplicationStatus.Interview, ApplicationStatus.Withdrawn),
        (ApplicationStatus.Interview, ApplicationStatus.Ghosted),
    ];

    public static TheoryData<ApplicationStatus, ApplicationStatus> Allowed()
    {
        var data = new TheoryData<ApplicationStatus, ApplicationStatus>();
        foreach (var (from, to) in AllowedMoves) data.Add(from, to);
        return data;
    }

    // Every pair of statuses (7 x 7) that is not in the allowed list, including "same status" and moves out of final states.
    public static TheoryData<ApplicationStatus, ApplicationStatus> Disallowed()
    {
        var data = new TheoryData<ApplicationStatus, ApplicationStatus>();
        foreach (var from in Enum.GetValues<ApplicationStatus>())
        {
            foreach (var to in Enum.GetValues<ApplicationStatus>())
            {
                if (!AllowedMoves.Contains((from, to))) data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Allowed))]
    public void ChangeStatus_AllowedMove_ChangesStatus(ApplicationStatus from, ApplicationStatus to)
    {
        // Arrange
        var application = CreateApplicationIn(from);

        // Act
        var result = application.ChangeStatus(to, Later);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(to, application.Status);
        Assert.Equal(Later, application.UpdatedAt);
    }

    [Theory]
    [MemberData(nameof(Disallowed))]
    public void ChangeStatus_DisallowedMove_ReturnsConflictAndKeepsStatus(ApplicationStatus from, ApplicationStatus to)
    {
        // Arrange
        var application = CreateApplicationIn(from);

        // Act
        var result = application.ChangeStatus(to, Later);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Job.InvalidStatusChange", result.Error.Code);
        Assert.Equal(from, application.Status);
        Assert.Equal(Created, application.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_ToApplied_SetsAppliedAt()
    {
        // Arrange
        var application = CreateApplicationIn(ApplicationStatus.Saved);

        // Act
        application.ChangeStatus(ApplicationStatus.Applied, Later);

        // Assert
        Assert.Equal(Later, application.AppliedAt);
    }

    [Fact]
    public void ChangeStatus_ToOtherStatus_LeavesAppliedAtEmpty()
    {
        // Arrange
        var application = CreateApplicationIn(ApplicationStatus.Saved);

        // Act
        application.ChangeStatus(ApplicationStatus.PlanningToApply, Later);

        // Assert
        Assert.Null(application.AppliedAt);
    }

    [Fact]
    public void UpdateNotes_WithText_SavesTrimmedNotes()
    {
        // Arrange
        var application = CreateApplicationIn(ApplicationStatus.Saved);

        // Act
        application.UpdateNotes("  Call recruiter on Monday ", Later);

        // Assert
        Assert.Equal("Call recruiter on Monday", application.Notes);
        Assert.Equal(Later, application.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void UpdateNotes_EmptyText_ClearsNotes(string? notes)
    {
        // Arrange
        var application = CreateApplicationIn(ApplicationStatus.Saved);
        application.UpdateNotes("Something", Created);

        // Act
        application.UpdateNotes(notes, Later);

        // Assert
        Assert.Null(application.Notes);
    }

    // Moves a new application to the wanted status by walking the shortest allowed path from Saved.
    private static JobApplication CreateApplicationIn(ApplicationStatus status)
    {
        var application = Job.Create("Developer", "Acme", null, "Description", null, JobSource.Manual, null, Created).Value.Application;

        ApplicationStatus[] path = status switch
        {
            ApplicationStatus.Saved => [],
            ApplicationStatus.PlanningToApply => [ApplicationStatus.PlanningToApply],
            ApplicationStatus.Applied => [ApplicationStatus.Applied],
            ApplicationStatus.Interview => [ApplicationStatus.Applied, ApplicationStatus.Interview],
            ApplicationStatus.Offer => [ApplicationStatus.Applied, ApplicationStatus.Interview, ApplicationStatus.Offer],
            ApplicationStatus.Rejected => [ApplicationStatus.Applied, ApplicationStatus.Rejected],
            ApplicationStatus.Withdrawn => [ApplicationStatus.Withdrawn],
            ApplicationStatus.Ghosted => [ApplicationStatus.Applied, ApplicationStatus.Ghosted],
        };

        foreach (var step in path) application.ChangeStatus(step, Created);

        return application;
    }
}
