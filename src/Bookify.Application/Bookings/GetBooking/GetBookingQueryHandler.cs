using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Dapper;

namespace Bookify.Application.Bookings.GetBooking
{
    internal sealed class GetBookingQueryHandler : IQueryHandler<GetBookingQuery, BookingResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetBookingQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<BookingResponse>> Handle(GetBookingQuery request, CancellationToken cancellationToken)
        {

            using var connection = _sqlConnectionFactory.CreateConnection();
            const string sql = """
                SELECT
                    id AS Id,
                    apartment_id AS ApartmentId,
                    user_id AS UserId,
                    status AS Status,
                    price_for_period_amount AS PriceAmount,
                    price_for_period_currency AS PriceCurrency,
                    cleaning_fee_amount AS Cleaning FeeAmount,
                    cleaning_fee_currency AS Cleaning FeeCurrency,
                    amenities_up_charge_amount AS AmenitiesUpCharge Amount, amenities_up_charge_currency AS Amenities UpChargeCurrency,
                    total_price_amount AS TotalPriceAmount,
                    total_price_currency AS TotalPriceCurrency,
                    duration_start AS DurationStart,
                    duration_end AS DurationEnd,
                    created_on_utc AS CreatedOnUtc
                FROM bookings
                WHERE id = @BookingId
                """;

            var booking = await connection.QueryFirstOrDefaultAsync<BookingResponse>(
                sql,
                new
                {
                    request.BookingId
                });
            
            return booking;
        }
    }
}
