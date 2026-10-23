namespace Jobly.Domain.Jobs;

/// <summary>Where a job came from.</summary>
public enum JobSource
{
    /// <summary>The user pasted or typed it in.</summary>
    Manual,

    /// <summary>Imported from Arbeidsplassen (import comes in a later plan).</summary>
    Arbeidsplassen,
}
