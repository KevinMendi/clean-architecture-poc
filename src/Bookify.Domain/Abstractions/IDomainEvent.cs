using MediatR;

namespace Bookify.Domain.Abstractions
{
    //INotification is used to implement pub and sub pattern
    //We will be publishing a Domain Event and can have one or more subscriber to this event that want to handle it
    public interface IDomainEvent : INotification
    {
    }
}
