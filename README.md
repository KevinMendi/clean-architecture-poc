# Bookify - Clean Architecture Implementation

A comprehensive demonstration of Clean Architecture principles implemented in .NET 8, featuring a booking system for apartments with full authentication, authorization, and domain-driven design patterns.

## 🏗️ Architecture Overview

This project implements Clean Architecture with four distinct layers:

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                       │
│                    (Bookify.Api)                            │
├─────────────────────────────────────────────────────────────┤
│                   Application Layer                         │
│                (Bookify.Application)                        │
├─────────────────────────────────────────────────────────────┤
│                   Infrastructure Layer                      │
│                (Bookify.Infrastructure)                     │
├─────────────────────────────────────────────────────────────┤
│                     Domain Layer                            │
│                  (Bookify.Domain)                           │
└─────────────────────────────────────────────────────────────┘
```

## 📋 Table of Contents

- [Architecture Overview](#architecture-overview)
- [Domain Layer](#domain-layer)
- [Application Layer](#application-layer)
- [Infrastructure Layer](#infrastructure-layer)
- [Presentation Layer](#presentation-layer)
- [Authentication & Authorization](#authentication--authorization)
- [Advanced Features](#advanced-features)
- [Testing Strategy](#testing-strategy)
- [Getting Started](#getting-started)
- [API Documentation](#api-documentation)

## 🎯 Domain Layer

### Core Principles

The Domain layer is the heart of the application, implementing Domain-Driven Design (DDD) principles:

- **Rich Domain Model**: Entities with behavior, not just data containers
- **Value Objects**: Immutable objects representing concepts without identity
- **Domain Events**: Decoupled communication between domain objects
- **Result Pattern**: Explicit success/failure handling
- **Repository Pattern**: Abstract data access

### Key Components

#### Entities

```csharp
public sealed class User : Entity
{
    private readonly List<Role> _roles = new();

    public FirstName FirstName { get; private set; }
    public LastName LastName { get; private set; }
    public Email Email { get; private set; }

    public static User Create(FirstName firstName, LastName lastName, Email email)
    {
        var user = new User(Guid.NewGuid(), firstName, lastName, email);
        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id));
        user._roles.Add(Role.Registered);
        return user;
    }
}
```

#### Value Objects

```csharp
public sealed record FirstName
{
    public string Value { get; }

    public FirstName(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("First name cannot be empty");

        Value = value;
    }
}
```

#### Domain Events

```csharp
public sealed record BookingReservedDomainEvent(Guid BookingId) : IDomainEvent;
public sealed record UserCreatedDomainEvent(Guid UserId) : IDomainEvent;
```

#### Result Pattern

```csharp
public class Result
{
    public bool IsSuccess { get; }
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
}
```

### Domain Services

#### Pricing Service

```csharp
public sealed class PricingService
{
    public PricingDetails CalculatePrice(Apartment apartment, DateRange duration)
    {
        var priceForPeriod = apartment.Price * duration.LengthInDays;
        var cleaningFee = apartment.CleaningFee;
        var amenitiesUpCharge = apartment.Amenities.Sum(amenity => amenity.Price);

        return new PricingDetails(priceForPeriod, cleaningFee, amenitiesUpCharge);
    }
}
```

## 🔄 Application Layer

### CQRS Implementation

The Application layer implements Command Query Responsibility Segregation (CQRS) using MediatR:

#### Commands

```csharp
public record ReserveBookingCommand(
    Guid ApartmentId,
    Guid UserId,
    DateOnly StartDate,
    DateOnly EndDate) : ICommand<Guid>;
```

#### Queries

```csharp
public record SearchApartmentQuery(
    DateOnly StartDate,
    DateOnly EndDate) : IQuery<IReadOnlyList<ApartmentResponse>>;
```

#### Command Handlers

```csharp
internal sealed class ReserveBookingCommandHandler : ICommandHandler<ReserveBookingCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ReserveBookingCommand request, CancellationToken cancellationToken)
    {
        // Domain logic orchestration
        var booking = Booking.Reserve(apartment, user.Id, duration, utcNow, pricingService);
        _bookingRepository.Add(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return booking.Id;
    }
}
```

#### Query Handlers (with Dapper)

```csharp
internal sealed class SearchApartmentQueryHandler : IQueryHandler<SearchApartmentQuery, IReadOnlyList<ApartmentResponse>>
{
    public async Task<Result<IReadOnlyList<ApartmentResponse>>> Handle(SearchApartmentQuery request, CancellationToken cancellationToken)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        const string sql = """
            SELECT a.id, a.name, a.description, a.price_amount, a.price_currency
            FROM apartments AS a
            WHERE NOT EXISTS (
                SELECT 1 FROM bookings AS b
                WHERE b.apartment_id = a.id
                AND b.duration_start <= @EndDate
                AND b.duration_end >= @StartDate
            )
        """;

        var apartments = await connection.QueryAsync<ApartmentResponse>(sql, request);
        return apartments.ToList();
    }
}
```

### Cross-Cutting Concerns

#### Validation Pipeline

```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        return await next();
    }
}
```

#### Logging Behavior

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
        var result = await next();
        _logger.LogInformation("Handled {RequestName}", typeof(TRequest).Name);
        return result;
    }
}
```

## 🏗️ Infrastructure Layer

### Entity Framework Core Configuration

#### DbContext

```csharp
public sealed class ApplicationDbContext : DbContext, IUnitOfWork
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AddDomainEventsAsOutboxMessages();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void AddDomainEventsAsOutboxMessages()
    {
        var outboxMessages = ChangeTracker
            .Entries<Entity>()
            .SelectMany(entity => entity.GetDomainEvents())
            .Select(domainEvent => new OutboxMessage(
                Guid.NewGuid(),
                _dateTimeProvider.UtcNow,
                domainEvent.GetType().Name,
                JsonConvert.SerializeObject(domainEvent)))
            .ToList();

        AddRange(outboxMessages);
    }
}
```

#### Repository Implementation

```csharp
internal sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<User>()
            .Include(user => user.Roles)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }
}
```

### Authentication & Authorization

#### Keycloak Integration

```csharp
internal sealed class AuthenticationService : IAuthenticationService
{
    public async Task<string> RegisterAsync(User user, string password, CancellationToken cancellationToken = default)
    {
        var userRepresentationModel = UserRepresentationModel.FromUser(user);
        var response = await _httpClient.PostAsJsonAsync("users", userRepresentationModel, cancellationToken);
        return ExtractIdentityIdFromLocationHeader(response);
    }
}
```

#### JWT Configuration

```csharp
public class JwtBearerOptionsSetup : IConfigureOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options)
    {
        options.Authority = _keycloakOptions.AuthorityUrl;
        options.Audience = _keycloakOptions.Audience;
        options.RequireHttpsMetadata = false;
    }
}
```

#### Permission-Based Authorization

```csharp
public class CustomPermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var permissions = await _authorizationService.GetPermissionsForUserAsync(identityId);

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
```

### Outbox Pattern

#### Outbox Message Processing

```csharp
[DisallowConcurrentExecution]
internal sealed class ProcessOutboxMessagesJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var connection = _sqlConnectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        var outboxMessages = await GetOutboxMessagesAsync(connection, transaction);

        foreach (var outboxMessage in outboxMessages)
        {
            try
            {
                var domainEvent = JsonConvert.DeserializeObject<IDomainEvent>(outboxMessage.Content);
                await _publisher.Publish(domainEvent, context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while processing outbox message {MessageId}", outboxMessage.Id);
            }

            await UpdateOutboxMessageAsync(connection, transaction, outboxMessage, exception);
        }

        transaction.Commit();
    }
}
```

### Caching with Redis

```csharp
internal sealed class CacheService : ICacheService
{
    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _redisDatabase.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var serializedValue = JsonSerializer.Serialize(value);
        await _redisDatabase.StringSetAsync(key, serializedValue, expiration);
    }
}
```

## 🌐 Presentation Layer

### API Controllers

#### RESTful Endpoints

```csharp
[Authorize]
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route("api/v{version:apiVersion}/bookings")]
public class BookingsController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetBookingQuery(id);
        var result = await _sender.Send(query, cancellationToken);
        return result.IsSuccess ? Ok(result) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> ReserveBooking(ReserveBookingRequest request, CancellationToken cancellationToken)
    {
        var command = new ReserveBookingCommand(request.ApartmentId, request.UserId, request.StartDate, request.EndDate);
        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetBooking), new { id = result.Value }, result.Value);
    }
}
```

### Middleware

#### Exception Handling

```csharp
public class ExceptionHandlingMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { errors = ex.Errors });
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred" });
        }
    }
}
```

#### Request Logging

```csharp
public class RequestContextLoggingMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = context.TraceIdentifier,
            ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
            ["IPAddress"] = context.Connection.RemoteIpAddress?.ToString()
        });

        await next(context);
    }
}
```

## 🔐 Authentication & Authorization

### Keycloak Setup

The application uses Keycloak as the identity provider:

1. **Realm Configuration**: Custom realm with users, roles, and permissions
2. **Client Setup**: Confidential client with JWT token configuration
3. **User Management**: Integration with domain users

### Authorization Levels

#### Role-Based Authorization

```csharp
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    // Admin-only endpoints
}
```

#### Permission-Based Authorization

```csharp
[HasPermission(Permissions.Bookings.Cancel)]
public async Task<IActionResult> CancelBooking(Guid id)
{
    // Permission-checked endpoint
}
```

#### Resource-Based Authorization

```csharp
public async Task<IActionResult> GetBooking(Guid id)
{
    var booking = await _bookingRepository.GetByIdAsync(id);

    if (booking.UserId != _userContext.UserId && !_userContext.HasPermission(Permissions.Bookings.ViewAll))
    {
        return Forbid();
    }

    return Ok(booking);
}
```

## 🚀 Advanced Features

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString)
    .AddRedis(redisConnectionString)
    .AddUrlGroup(new Uri("https://keycloak:8080"), "Keycloak");
```

### API Versioning

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});
```

### Structured Logging with Serilog

```csharp
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));
```

### Docker Compose Setup

```yaml
services:
  bookify.api:
    build: .
    ports: ["8080:8080"]
    depends_on: [bookify-db, bookify-idp]

  bookify-db:
    image: postgres:latest
    environment:
      POSTGRES_DB: bookify
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres

  bookify-idp:
    image: quay.io/keycloak/keycloak:latest
    command: start-dev --import-realm
    ports: ["18080:8080"]

  bookify-seq:
    image: datalust/seq:latest
    ports: ["8082:80"]

  bookify-redis:
    image: redis:latest
    ports: ["6379:6379"]
```

## 🧪 Testing Strategy

### Architecture Tests

```csharp
public class LayerTests : BaseTest
{
    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_ApplicationLayer()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOn(ApplicationAssembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
```

### Unit Tests

#### Domain Tests

```csharp
public class BookingTests
{
    [Fact]
    public void Reserve_ShouldCreateBookingWithReservedStatus()
    {
        var apartment = ApartmentData.Create();
        var duration = DateRange.Create(DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)));

        var booking = Booking.Reserve(apartment, Guid.NewGuid(), duration, DateTime.UtcNow, new PricingService());

        booking.Status.Should().Be(BookingStatus.Reserved);
        booking.ApartmentId.Should().Be(apartment.Id);
    }
}
```

#### Application Tests

```csharp
public class ReserveBookingTests
{
    [Fact]
    public async Task Handle_ShouldReserveBooking_WhenValidRequest()
    {
        var command = new ReserveBookingCommand(Guid.NewGuid(), Guid.NewGuid(), DateOnly.Today, DateOnly.Today.AddDays(2));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }
}
```

### Integration Tests

```csharp
public class ConfirmBookingTests : BaseIntegrationTest
{
    [Fact]
    public async Task ConfirmBooking_ShouldChangeStatusToConfirmed()
    {
        var booking = await CreateBookingAsync();

        var command = new ConfirmBookingCommand(booking.Id);
        var result = await _sender.Send(command);

        result.IsSuccess.Should().BeTrue();

        var confirmedBooking = await _bookingRepository.GetByIdAsync(booking.Id);
        confirmedBooking.Status.Should().Be(BookingStatus.Confirmed);
    }
}
```

### Functional Tests

```csharp
public class LoginUserTests : BaseFunctionalTest
{
    [Fact]
    public async Task Login_ShouldReturnAccessToken_WhenValidCredentials()
    {
        var request = new LogInUserRequest("user@example.com", "password");

        var response = await _client.PostAsJsonAsync("/api/v1/users/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
    }
}
```

## 🚀 Getting Started

### Prerequisites

- .NET 8 SDK
- Docker and Docker Compose
- PostgreSQL (via Docker)
- Keycloak (via Docker)

> **Note**: Default development credentials are provided for local development only. Always use strong, unique passwords in production environments.

### Quick Start

1. **Clone the repository**

   ```bash
   git clone <repository-url>
   cd clean-architecture-poc
   ```

2. **Start the infrastructure**

   ```bash
   docker-compose up -d
   ```

3. **Run the application**

   ```bash
   dotnet run --project src/Bookify.Api
   ```

4. **Access the application**
   - API: http://localhost:8080
   - Swagger UI: http://localhost:8080/swagger
   - Keycloak Admin: http://localhost:18080 (admin/admin)
   - Seq Logging: http://localhost:8082

### Database Migrations

```bash
# Create migration
dotnet ef migrations add InitialCreate --project src/Bookify.Infrastructure --startup-project src/Bookify.Api

# Apply migrations
dotnet ef database update --project src/Bookify.Infrastructure --startup-project src/Bookify.Api
```

## 📚 API Documentation

### Authentication Endpoints

#### Register User

```http
POST /api/v1/users/register
Content-Type: application/json

{
  "firstName": "John",
  "lastName": "Doe",
  "email": "user@example.com",
  "password": "your-secure-password"
}
```

#### Login User

```http
POST /api/v1/users/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "your-secure-password"
}
```

### Booking Endpoints

#### Reserve Booking

```http
POST /api/v1/bookings
Authorization: Bearer <jwt-token>
Content-Type: application/json

{
  "apartmentId": "123e4567-e89b-12d3-a456-426614174000",
  "userId": "123e4567-e89b-12d3-a456-426614174001",
  "startDate": "2024-01-15",
  "endDate": "2024-01-20"
}
```

#### Get Booking

```http
GET /api/v1/bookings/{id}
Authorization: Bearer <jwt-token>
```

#### Confirm Booking

```http
PUT /api/v1/bookings/{id}/confirm
Authorization: Bearer <jwt-token>
```

### Apartment Endpoints

#### Search Apartments

```http
GET /api/v1/apartments/search?startDate=2024-01-15&endDate=2024-01-20
```

## 🏗️ Design Patterns Implemented

1. **Clean Architecture**: Separation of concerns with dependency inversion
2. **Domain-Driven Design**: Rich domain models with business logic
3. **CQRS**: Command Query Responsibility Segregation
4. **Repository Pattern**: Abstract data access
5. **Unit of Work**: Transaction management
6. **Mediator Pattern**: Decoupled communication
7. **Result Pattern**: Explicit error handling
8. **Outbox Pattern**: Reliable message processing
9. **Specification Pattern**: Complex query encapsulation
10. **Factory Pattern**: Object creation
11. **Strategy Pattern**: Algorithm selection
12. **Observer Pattern**: Domain events

## 🔧 Configuration

### Application Settings

For production deployments, use environment variables or user secrets to store sensitive configuration:

```json
{
  "ConnectionStrings": {
    "Database": "Host=your-db-host;Database=bookify;Username=your-username;Password=your-secure-password"
  },
  "Keycloak": {
    "AuthorityUrl": "https://your-keycloak-instance/realms/bookify",
    "Audience": "bookify-api"
  },
  "Redis": {
    "ConnectionString": "your-redis-connection-string"
  },
  "Outbox": {
    "BatchSize": 100,
    "IntervalInSeconds": 30
  }
}
```

### Environment Variables

Set the following environment variables in production:

```bash
# Database
DATABASE_CONNECTION_STRING="Host=your-db-host;Database=bookify;Username=your-username;Password=your-secure-password"

# Keycloak
KEYCLOAK_AUTHORITY_URL="https://your-keycloak-instance/realms/bookify"
KEYCLOAK_AUDIENCE="bookify-api"

# Redis
REDIS_CONNECTION_STRING="your-redis-connection-string"
```

### User Secrets (Development)

For local development, use .NET User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Database=bookify;Username=postgres;Password=postgres"
dotnet user-secrets set "Keycloak:AuthorityUrl" "http://localhost:18080/realms/bookify"
dotnet user-secrets set "Keycloak:Audience" "bookify-api"
dotnet user-secrets set "Redis:ConnectionString" "localhost:6379"
```

## 📈 Performance Considerations

1. **Dapper for Queries**: High-performance data access for read operations
2. **Redis Caching**: Distributed caching for frequently accessed data
3. **Optimistic Concurrency**: Conflict resolution for concurrent updates
4. **Connection Pooling**: Efficient database connection management
5. **Async/Await**: Non-blocking I/O operations throughout the stack

## 🔒 Security Features

1. **JWT Authentication**: Secure token-based authentication
2. **Role-Based Access Control**: User role management
3. **Permission-Based Authorization**: Fine-grained access control
4. **Input Validation**: Comprehensive request validation
5. **HTTPS Enforcement**: Secure communication
6. **CORS Configuration**: Cross-origin resource sharing control
7. **Environment-Based Configuration**: Sensitive data stored in environment variables or user secrets
8. **No Hardcoded Secrets**: All credentials and connection strings externalized

## 📊 Monitoring & Observability

1. **Structured Logging**: Serilog with Seq integration
2. **Health Checks**: Application and infrastructure health monitoring
3. **Request Logging**: Comprehensive request/response logging
4. **Performance Metrics**: Built-in performance monitoring
5. **Error Tracking**: Centralized error handling and reporting

## 🤝 Contributing

1. Follow Clean Architecture principles
2. Write comprehensive tests
3. Use meaningful commit messages
4. Follow C# coding conventions
5. Update documentation as needed

---

This implementation demonstrates a production-ready Clean Architecture solution with comprehensive testing, security, and monitoring capabilities. The codebase serves as an excellent reference for building scalable, maintainable .NET applications following DDD and Clean Architecture principles.
