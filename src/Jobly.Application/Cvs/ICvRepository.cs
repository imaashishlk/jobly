using Jobly.Domain.Cvs;

namespace Jobly.Application.Cvs;

/// <summary>
/// What the Application layer needs from storage for the CV. There is only ever one CV, so there is no id lookup.
/// </summary>
public interface ICvRepository
{
    /// <summary>The CV, or null if none has been added yet.</summary>
    Task<Cv?> Get(CancellationToken cancellationToken);

    /// <summary>Stores this CV and makes it the only one: any CV saved before is replaced.</summary>
    Task Save(Cv cv, CancellationToken cancellationToken);

    Task Delete(Cv cv, CancellationToken cancellationToken);
}
