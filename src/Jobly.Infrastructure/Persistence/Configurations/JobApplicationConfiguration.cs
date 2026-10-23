using Jobly.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobly.Infrastructure.Persistence.Configurations;

/// <summary>How a <see cref="JobApplication"/> maps to the JobApplications table. The link to Job is set up in <see cref="JobConfiguration"/>.</summary>
internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("JobApplications");
        builder.HasKey(application => application.Id);
        builder.Property(application => application.Id).ValueGeneratedNever();

        // Stored as text ("Applied", "Interview", ...) so the table is readable in any SQL tool.
        builder.Property(application => application.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        // Matches the limit on UpdateNotesRequest.
        builder.Property(application => application.Notes).HasMaxLength(5000);

        // The Kanban board loads one column at a time, so filtering by status gets an index.
        builder.HasIndex(application => application.Status);
    }
}
