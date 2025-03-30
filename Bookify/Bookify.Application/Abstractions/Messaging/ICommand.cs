using Bookify.Domain.Abstractions;
using MediatR;

namespace Bookify.Application.Abstractions.Messaging
{
    public interface ICommand : IRequest<Result>, IBaseCommand
    {
    }

    public interface ICommand<TRespose> : IRequest<Result<TRespose>>, IBaseCommand
    {
    }

    public interface IBaseCommand
    {

    }
}
