using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobly.Api.Features.Jobs;

/// <summary>How a <see cref="Job"/> maps to the Jobs table.</summary>
internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(job => job.Id);

        // The domain creates the Guid itself (Guid.NewGuid()), so the database must not generate one.
        builder.Property(job => job.Id).ValueGeneratedNever();

        // Max lengths match the [MaxLength] limits on the request DTOs.
        builder.Property(job => job.Title).HasMaxLength(200).IsRequired();
        builder.Property(job => job.Company).HasMaxLength(200).IsRequired();
        builder.Property(job => job.Location).HasMaxLength(200);
        builder.Property(job => job.Description).HasMaxLength(20_000).IsRequired();
        builder.Property(job => job.Url).HasMaxLength(2000);
        builder.Property(job => job.ExternalId).HasMaxLength(200);

        // Stored as text ("Manual", "Arbeidsplassen") instead of 0, 1, so the table is readable
        // and reordering the enum later can't silently change what existing rows mean.
        builder.Property(job => job.Source).HasConversion<string>().HasMaxLength(30).IsRequired();

        // Stops the same Arbeidsplassen ad being saved twice later. No filter is needed: manual jobs have no ExternalId (NULL),
        // and PostgreSQL treats every NULL in a unique index as different, so any number of manual jobs can exist.
        builder.HasIndex(job => new { job.Source, job.ExternalId })
            .IsUnique();

        // One job has exactly one application. Deleting the job deletes its application too.
        // HasForeignKey also creates a unique index on JobApplications.JobId.
        builder.HasOne(job => job.Application)
            .WithOne()
            .HasForeignKey<JobApplication>(application => application.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(job => job.Application).IsRequired();
    }
}

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
