using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobly.Api.Features.Cvs;

/// <summary>How a <see cref="Cv"/> maps to the Cvs table.</summary>
internal sealed class CvConfiguration : IEntityTypeConfiguration<Cv>
{
    public void Configure(EntityTypeBuilder<Cv> builder)
    {
        builder.ToTable("Cvs");
        builder.HasKey(cv => cv.Id);

        // The domain creates the Guid itself, so the database must not generate one.
        builder.Property(cv => cv.Id).ValueGeneratedNever();

        // The same limit the Domain enforces, so the database can never hold more than a CV may have.
        builder.Property(cv => cv.Text).HasMaxLength(Cv.MaxTextLength).IsRequired();

        // 260 is the longest file name Windows allows.
        builder.Property(cv => cv.FileName).HasMaxLength(260);

        // Stored as text ("Uploaded", "Pasted") so the table is readable.
        builder.Property(cv => cv.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
    }
}
