using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Exceptions;
using FluentValidation;
using MediatR;

namespace Bookify.Application.Abstractions.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IBaseCommand
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }
        public async Task<TResponse> Handle(
            TRequest request, 
            RequestHandlerDelegate<TResponse> next, 
            CancellationToken cancellationToken)
        {
            if(!_validators.Any()) 
            {
                return await next();
            }

            var context = new ValidationContext<TRequest>(request);

            // Iterate over each validator and validate the request
            // Collect all validation errors
            // Project them into a list of ValidationError objects
            var validationErrors = _validators
                .Select(validator => validator.Validate(context))
                .Where(validationResult => validationResult.Errors.Any())
                .SelectMany(validationResult => validationResult.Errors)
                .Select(validationFailure => new ValidationError(
                    validationFailure.PropertyName,
                    validationFailure.ErrorMessage))
                .ToList();

            if(validationErrors.Any()) 
            {
                throw new Exceptions.ValidationException(validationErrors);
            }

            return await next();
        }
    }
}
