using ApiServer.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiServer.Infrastructure.Persistence.Configurations;

public sealed class UserIdentityConfig : IEntityTypeConfiguration<UserIdentity>
{
    public void Configure(EntityTypeBuilder<UserIdentity> b)
    {
        b.ToTable("user_identities");

        b.HasKey(x => x.Id);

        b.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        b.Property(x => x.Uid)
            .HasColumnName("uid")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasConversion<short>()
            .IsRequired();

        b.Property(x => x.ProviderSubject)
            .HasColumnName("provider_subject")
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // (provider, provider_subject) unique
        b.HasIndex(x => new { x.Provider, x.ProviderSubject })
            .IsUnique()
            .HasDatabaseName("ux_user_identities_provider_subject");

        // (uid, provider) unique (한 uid당 동일 provider 1개)
        b.HasIndex(x => new { x.Uid, x.Provider })
            .IsUnique()
            .HasDatabaseName("ux_user_identities_uid_provider");

        // 조회 최적화
        b.HasIndex(x => x.Uid)
            .HasDatabaseName("ix_user_identities_uid");
    }
}
