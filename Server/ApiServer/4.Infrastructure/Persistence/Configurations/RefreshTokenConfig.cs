using ApiServer.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiServer.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");

        b.HasKey(x => x.TokenId);

        b.Property(x => x.TokenId)
            .HasColumnName("token_id")
            .HasMaxLength(80)
            .IsRequired();

        b.Property(x => x.Uid)
            .HasColumnName("uid")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        b.Property(x => x.IssuedAt)
            .HasColumnName("issued_at")
            .IsRequired();

        b.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        b.Property(x => x.RevokedAt)
            .HasColumnName("revoked_at");

        b.Property(x => x.ReplacedByTokenId)
            .HasColumnName("replaced_by_token_id")
            .HasMaxLength(80);

        b.Property(x => x.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(128);

        b.Property(x => x.Ip)
            .HasColumnName("ip")
            .HasMaxLength(64);

        b.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(256);

        // token_hash unique (원문은 다르지만 해시 충돌 가능성은 사실상 무시)
        b.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_refresh_tokens_token_hash");

        // uid 조회 빠르게
        b.HasIndex(x => x.Uid)
            .HasDatabaseName("ix_refresh_tokens_uid");

        b.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("ix_refresh_tokens_expires_at");
    }
}
