using CrewPilot.Core.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class ModelProviderConfiguration : IEntityTypeConfiguration<ModelProvider>
{
    public void Configure(EntityTypeBuilder<ModelProvider> builder)
    {
        builder.ToTable("ModelProviders");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.BaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(p => p.ApiKeyProtected).HasMaxLength(4000).IsRequired();
        builder.Property(p => p.Source).HasConversion<string>().HasMaxLength(16);
        builder.HasMany(p => p.Models)
            .WithOne(m => m.Provider)
            .HasForeignKey(m => m.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModelCatalogEntryConfiguration : IEntityTypeConfiguration<ModelCatalogEntry>
{
    public void Configure(EntityTypeBuilder<ModelCatalogEntry> builder)
    {
        builder.ToTable("ModelCatalogEntries");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.ModelId).HasMaxLength(200).IsRequired();
        builder.Property(m => m.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Capabilities).HasMaxLength(1000);
        builder.Property(m => m.InputPricePerMillion).HasPrecision(9, 4);
        builder.Property(m => m.OutputPricePerMillion).HasPrecision(9, 4);
        builder.HasIndex(m => new { m.ProviderId, m.ModelId }).IsUnique();
    }
}
