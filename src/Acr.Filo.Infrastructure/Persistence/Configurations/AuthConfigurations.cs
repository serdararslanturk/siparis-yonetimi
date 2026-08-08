using Acr.Filo.Domain.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acr.Filo.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("Users");
        e.HasKey(x => x.Id);

        // KRİTİK: DB collation Turkish_CI_AS. Orada 'I' ile 'i' EŞLEŞMEZ.
        // Bu kolon açıkça Latin1_General_100_CI_AS olmalı, yoksa
        // 'INFO@x.com' ile 'info@x.com' farklı sayılır ve login kırılır.
        e.Property(x => x.Email).HasColumnType("nvarchar(200)")
            .UseCollation("Latin1_General_100_CI_AS").IsRequired();

        e.Property(x => x.FullName).HasColumnType("nvarchar(150)").IsRequired();
        e.Property(x => x.PasswordHash).HasColumnType("nvarchar(400)");
        e.Property(x => x.SecurityStamp).HasDefaultValueSql("NEWID()");
        e.Property(x => x.MustChangePassword).HasDefaultValue(false);
        e.Property(x => x.AccessFailedCount).HasDefaultValue(0);
        e.Property(x => x.LockoutEndUtc).HasColumnType("datetime2(3)");
        e.Property(x => x.LastLoginAtUtc).HasColumnType("datetime2(3)");
        e.Property(x => x.IsActive).HasDefaultValue(true);
        e.Property(x => x.IsDeleted).HasDefaultValue(false);
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.UpdatedAt).HasColumnType("datetime2(3)");
        e.Property(x => x.RowVersion).IsRowVersion();

        e.HasIndex(x => x.Email).IsUnique()
            .HasFilter("[IsDeleted] = 0").HasDatabaseName("UX_Users_Email");

        e.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> e)
    {
        e.ToTable("Roles");
        e.HasKey(x => x.Id);
        e.Property(x => x.Key).HasColumnName("Key").HasColumnType("varchar(40)").IsRequired();
        e.Property(x => x.Name).HasColumnType("nvarchar(80)").IsRequired();
        e.Property(x => x.Description).HasColumnType("nvarchar(250)");
        e.Property(x => x.IsSystem).HasDefaultValue(false);
        e.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        e.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_Roles_Key");
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> e)
    {
        e.ToTable("UserRoles");
        e.HasKey(x => new { x.UserId, x.RoleId });
        e.HasOne(x => x.User).WithMany(u => u.UserRoles)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Role).WithMany(r => r.UserRoles)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> e)
    {
        e.ToTable("Permissions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Key).HasColumnName("Key").HasColumnType("varchar(60)").IsRequired();
        e.Property(x => x.Name).HasColumnType("nvarchar(120)").IsRequired();
        e.Property(x => x.Group).HasColumnName("Group").HasColumnType("nvarchar(60)").IsRequired();
        e.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_Permissions_Key");
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> e)
    {
        e.ToTable("RolePermissions");
        e.HasKey(x => new { x.RoleId, x.PermissionId });
        e.HasOne(x => x.Role).WithMany(r => r.RolePermissions)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Permission).WithMany(p => p.RolePermissions)
            .HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.ToTable("RefreshTokens");
        e.HasKey(x => x.Id);
        e.Property(x => x.TokenHash).HasColumnType("varbinary(32)").IsRequired();
        e.Property(x => x.ExpiresAtUtc).HasColumnType("datetime2(3)");
        e.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(3)");
        e.Property(x => x.CreatedByIp).HasColumnType("varchar(45)");
        e.Property(x => x.RevokedAtUtc).HasColumnType("datetime2(3)");
        e.Property(x => x.RevokedReason).HasColumnType("nvarchar(100)");

        e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UX_RefreshTokens_TokenHash");

        e.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // Self-FK: CASCADE olamaz (döngü). SQL tarafında da NO ACTION.
        e.HasOne<RefreshToken>().WithMany()
            .HasForeignKey(x => x.ReplacedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LoginAuditLogConfiguration : IEntityTypeConfiguration<LoginAuditLog>
{
    public void Configure(EntityTypeBuilder<LoginAuditLog> e)
    {
        e.ToTable("LoginAuditLogs");
        e.HasKey(x => x.Id);
        e.Property(x => x.AttemptedEmail).HasColumnType("nvarchar(200)")
            .UseCollation("Latin1_General_100_CI_AS");
        e.Property(x => x.FailureReason).HasColumnType("varchar(40)");
        e.Property(x => x.IpAddress).HasColumnType("varchar(45)");
        e.Property(x => x.UserAgent).HasColumnType("nvarchar(300)");
        e.Property(x => x.CorrelationId).HasColumnType("varchar(32)");
        e.Property(x => x.OccurredAtUtc).HasColumnType("datetime2(3)");
        // Kullanıcı silinse bile giriş izi kalır → CASCADE YOK.
        e.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
