using Acr.Filo.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acr.Filo.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> e)
    {
        e.ToTable("AuditLogs");
        e.HasKey(x => x.Id);
        e.Property(x => x.EntityName).HasColumnType("varchar(60)").IsRequired();
        e.Property(x => x.EntityId).HasColumnType("varchar(40)").IsRequired();
        e.Property(x => x.Action).HasColumnName("Action").HasColumnType("varchar(10)").IsRequired();
        e.Property(x => x.ColumnName).HasColumnType("varchar(60)");
        e.Property(x => x.OldValue).HasColumnType("nvarchar(max)");
        e.Property(x => x.NewValue).HasColumnType("nvarchar(max)");
        e.Property(x => x.CorrelationId).HasColumnType("varchar(32)");
        e.Property(x => x.IpAddress).HasColumnType("varchar(45)");
        e.Property(x => x.OccurredAtUtc).HasColumnType("datetime2(3)");
        e.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> e)
    {
        e.ToTable("NumberSequences");
        e.HasKey(x => new { x.Key, x.Year });
        e.Property(x => x.Key).HasColumnName("Key").HasColumnType("varchar(40)");
        e.Property(x => x.Year).HasColumnName("Year").HasColumnType("smallint");
    }
}
