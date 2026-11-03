using CrewPilot.Core.WorkLogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class WorkLogConfiguration : IEntityTypeConfiguration<WorkLog>
{
    public void Configure(EntityTypeBuilder<WorkLog> builder)
    {
        builder.ToTable("WorkLogs");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Hours).HasPrecision(9, 2);
        builder.Property(w => w.Note).HasMaxLength(2000);
        builder.HasIndex(w => w.WorkItemId);
        builder.HasOne(w => w.Member)
            .WithMany()
            .HasForeignKey(w => w.MemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
