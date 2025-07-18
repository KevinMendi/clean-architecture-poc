﻿using Bookify.Application.Apartments.SearchApartments;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Abstractions;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Apartments
{
    public class SearchApartmentsTests : BaseIntegrationTest
    {
        public SearchApartmentsTests(IntegrationTestWebAppFactory factory)
            : base(factory)
        {
        }

        [Fact]
        public async Task SearchApartments_ShouldReturnEmptyList_WhenDateRangeInvalid()
        {
            // Arrange
            var query = new SearchApartmentQuery(new DateOnly(2025, 6, 10), new DateOnly(2025, 6, 1));

            // Act
            Result<IReadOnlyList<ApartmentResponse>> result = await Sender.Send(query);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task SearchApartments_ShouldReturnApartments_WhenDateRangeIsValid()
        {
            // Arrange
            var query = new SearchApartmentQuery(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 10));

            // Act
            Result<IReadOnlyList<ApartmentResponse>> result = await Sender.Send(query);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeEmpty();
        }
    }
}
