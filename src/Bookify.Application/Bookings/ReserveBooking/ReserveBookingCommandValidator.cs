using FluentValidation;

namespace Bookify.Application.Bookings.ReserveBooking
{
    internal sealed class ReserveBookingCommandValidator : AbstractValidator<ReserveBookingCommand>
    {
        public ReserveBookingCommandValidator()
        {

            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("User ID is required.");

            RuleFor(c => c.ApartmentId).NotEmpty();

            RuleFor(c => c.StartDate).LessThan(c => c.EndDate);
        }
    }
}
