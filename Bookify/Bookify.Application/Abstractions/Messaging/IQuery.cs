using Bookify.Domain.Abstractions;
using MediatR;

namespace Bookify.Application.Abstractions.Messaging
{
    // This will going to be a mediator request, returning a result of TResponse object
    // We want to enforce that all of our queries in the application return an envelope of response which is the Result type
    // The queries can either succeed or fail, and the Result object should communicate this
    public interface IQuery<TResponse> : IRequest<Result<TResponse>>
    {

    }
}
