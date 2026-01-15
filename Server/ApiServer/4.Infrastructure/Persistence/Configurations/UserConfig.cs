using ApiServer.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApiServer.Infrastructure.Persistence.Configurations;

public sealed class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");

        b.HasKey(x => x.Uid);
        b.Property(x => x.Uid)
            .HasColumnName("uid")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        b.Property(x => x.LastLoginAt)
            .HasColumnName("last_login_at");

        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<short>() // enum -> smallint
            .IsRequired();

        b.HasMany(x => x.Identities)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.Uid)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.Uid)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
