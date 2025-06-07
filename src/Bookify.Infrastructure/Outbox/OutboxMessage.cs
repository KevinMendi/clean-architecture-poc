namespace Bookify.Infrastructure.Outbox
{
    public sealed class OutboxMessage
    {
        public OutboxMessage(Guid id, DateTime occurredOnUtc, string type, string content)
        {
            Id = id;
            OccurredOnUtc = occurredOnUtc;
            Type = type;
            Content = content;
        }

        public Guid Id { get; init; }

        public DateTime OccurredOnUtc { get; init; }

        // This will represent the fully qualified name of the domain event. It is going to be serialized into an outbox message
        public string Type { get; init; }

        // This will be a JSON string representing the Domain event instance
        public string Content { get; init; }

        // This will be used to determine if the outbox message has been processed or not
        public DateTime? ProcessedOnUtc { get; init; }

        public string? Error { get; init; }
    }
}
