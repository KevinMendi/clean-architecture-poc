using Bookify.Domain.Apartments;
using Bookify.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Infrastructure.Configurations
{
    internal sealed class ApartmentConfiguration : IEntityTypeConfiguration<Apartment>
    {
        public void Configure(EntityTypeBuilder<Apartment> builder)
        {
            // Map Apartment entity to the "apartments" table
            builder.ToTable("apartments");

            // Define the primary key for the Apartment entity
            builder.HasKey(apartment => apartment.Id);

            // Map Address value object(complex VO) into a set of columns in the same table as the owning entity
            builder.OwnsOne(apartment => apartment.Address);

            // For simple value objects, we can use the HasConversion method
            // to convert from VO to a primitive type and from primitive type to VO
            builder.Property(apartment => apartment.Name)
                .HasMaxLength(200)
                .HasConversion(name => name.Value, value => new Name(value));

            builder.Property(apartment => apartment.Description)
                .HasMaxLength(2000)
                .HasConversion(description => description.Value, value => new Description(value));

            builder.OwnsOne(apartment => apartment.Price, priceBuilder =>
            {
                priceBuilder.Property(money => money.Currency)
                    .HasConversion(currency => currency.Code, code => Currency.FromCode(code));
            });

            builder.OwnsOne(apartment => apartment.CleaningFee, priceBuilder =>
            {
                priceBuilder.Property(money => money.Currency)
                    .HasConversion(currency => currency.Code, code => Currency.FromCode(code));
            });

            // Define shadow property that can be used for optimistic concurrency support
            builder.Property<uint>("Version").IsRowVersion();
        }
    }
}
