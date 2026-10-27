using System.Text.Json;
using Jobly.Api.Features.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobly.Api.Features.Matches;

/// <summary>How a <see cref="JobMatch"/> maps to the JobMatches table.</summary>
internal sealed class JobMatchConfiguration : IEntityTypeConfiguration<JobMatch>
{
    // Tells EF Core how to tell whether a list changed (by its entries) and how to copy it, so saving a match works.
    private static readonly ValueComparer<IReadOnlyList<string>> ListComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        list => list.ToList());

    public void Configure(EntityTypeBuilder<JobMatch> builder)
    {
        builder.ToTable("JobMatches");
        builder.HasKey(match => match.Id);
        builder.Property(match => match.Id).ValueGeneratedNever();

        builder.Property(match => match.Summary).HasMaxLength(JobMatch.MaxSummaryLength).IsRequired();
        builder.Property(match => match.Model).HasMaxLength(100).IsRequired();

        // The skill lists are saved as one text column holding JSON, for example ["C#","SQL"]. They are only ever
        // read and shown together with the match, never searched on their own, so a separate table would add nothing.
        builder.Property(match => match.MatchingSkills).HasConversion(list => ToJson(list), json => FromJson(json), ListComparer).IsRequired();
        builder.Property(match => match.Gaps).HasConversion(list => ToJson(list), json => FromJson(json), ListComparer).IsRequired();

        // Only the latest result per job is kept, and deleting a job deletes its result.
        builder.HasIndex(match => match.JobId).IsUnique();
        builder.HasOne<Job>().WithOne().HasForeignKey<JobMatch>(match => match.JobId).OnDelete(DeleteBehavior.Cascade);
    }

    // The options argument is passed as null on purpose: it means "default JSON settings".
    private static string ToJson(IReadOnlyList<string> list) => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null);

    private static IReadOnlyList<string> FromJson(string json) => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? [];
}
