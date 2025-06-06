﻿namespace Bookify.Domain.Abstractions
{
    public abstract class Entity
    {
        private readonly List<IDomainEvent> _domaiEvents = new();

        protected Entity(Guid id) 
        {
            Id = id;
        }

        // To resolve migration issue for rich model domain
        protected Entity()
        {

        }

        public Guid Id { get; init; }

        public IReadOnlyList<IDomainEvent> GetDomainEvents()
        {
            return _domaiEvents.ToList();
        }

        public void ClearDomainEvents()
        {
            _domaiEvents.Clear();
        }

        protected void RaiseDomainEvent(IDomainEvent domainEvent)
        {
            _domaiEvents.Add(domainEvent);
        }
    }
}
