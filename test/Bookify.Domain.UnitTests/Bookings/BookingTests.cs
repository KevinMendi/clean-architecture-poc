﻿using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Shared;
using Bookify.Domain.UnitTests.Apartments;
using Bookify.Domain.UnitTests.Users;
using Bookify.Domain.Users;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.Bookings
{
    public class BookingTests : BaseTest
    {
        [Fact]
        public void Reserve_Should_RaiseBookingReservedDomainEvent()
        {
            // Arrange
            var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email);
            var price = new Money(10.0m, Currency.Usd);
            var period = DateRange.Create(new DateOnly(2025, 06, 01), new DateOnly(2025, 06, 10));
            var apartment = ApartmentData.Create(price);
            var pricingService = new PricingService();


            // Act
            var booking = Booking.Reserve(apartment, user.Id, period, DateTime.UtcNow, pricingService);

            // Assert
            var domainEvent = AssertDomainEventWasPublished<BookingReservedDomainEvent>(booking);
            domainEvent.BookingId.Should().Be(booking.Id);
        }
    }
}
