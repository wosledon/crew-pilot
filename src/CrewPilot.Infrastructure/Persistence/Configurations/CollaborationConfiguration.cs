using CrewPilot.Core.Collaboration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Content).HasMaxLength(16000).IsRequired();
        builder.Property(c => c.AuthorKind).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(c => c.WorkItemId);
        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FileName).HasMaxLength(500).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(200);
        builder.Property(a => a.StoragePath).HasMaxLength(1000).IsRequired();
    }
}

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.EventType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Payload).HasMaxLength(16000);
        builder.Property(a => a.ActorKind).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(a => new { a.ProjectId, a.CreatedAt });
        builder.HasOne(a => a.WorkItem)
            .WithMany(w => w.Activities)
            .HasForeignKey(a => a.WorkItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
