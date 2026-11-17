using Amanet.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanet.Infrastructure.Data.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.Property(client => client.Name).IsRequired().HasMaxLength(200);
        builder.Property(client => client.Email).IsRequired().HasMaxLength(256);
        builder.Property(client => client.Phone).HasMaxLength(50);
        builder.Property(client => client.PostalAddress).HasMaxLength(500);
    }
}
