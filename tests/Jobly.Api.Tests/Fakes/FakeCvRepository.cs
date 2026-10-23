using Jobly.Application.Cvs;
using Jobly.Domain.Cvs;

namespace Jobly.Api.Tests.Fakes;

/// <summary>An in-memory <see cref="ICvRepository"/> so API tests run without a database. There is only ever one CV.</summary>
public sealed class FakeCvRepository : ICvRepository
{
    /// <summary>The stored CV, or null. Tests read it to check what was saved, and set it to prepare a starting state.</summary>
    public Cv? Current { get; set; }

    public Task<Cv?> Get(CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task Save(Cv cv, CancellationToken cancellationToken)
    {
        Current = cv;
        return Task.CompletedTask;
    }

    public Task Delete(Cv cv, CancellationToken cancellationToken)
    {
        Current = null;
        return Task.CompletedTask;
    }
}
