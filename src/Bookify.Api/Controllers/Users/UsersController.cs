using Asp.Versioning;
using Bookify.Application.Users.GetLoggedInUser;
using Bookify.Application.Users.LogInUser;
using Bookify.Application.Users.RegisterUser;
using Bookify.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Api.Controllers.Users
{
    [ApiController]
    // [ApiVersion(1)] // Prefer using a constant to avoid mistyping the version
    // [ApiVersion(ApiVersions.V1, Deprecated = true)] // This is how to deprecate API version

    //List all available API versions
    [ApiVersion(ApiVersions.V1)]
    [ApiVersion(ApiVersions.V2)]
    [Route("api/v{version:apiVersion}/users")]
    public class UsersController : ControllerBase
    {
        private readonly ISender _sender;

        public UsersController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet("me")]
        // [Authorize(Roles = Roles.Registered)] // Role-based Authorization
        [MapToApiVersion(ApiVersions.V1)]
        [HasPermission(Permissions.UsersRead)]
        public async Task<IActionResult> GetLoggedInUserV1(CancellationToken cancellationToken)
        {
            var query = new GetLoggedInUserQuery();

            var result = await _sender.Send(query, cancellationToken);

            return Ok(result.Value);
        }

        [HttpGet("me")]
        // [Authorize(Roles = Roles.Registered)] // Role-based Authorization
        [MapToApiVersion(ApiVersions.V2)]
        [HasPermission(Permissions.UsersRead)]
        public async Task<IActionResult> GetLoggedInUserV2(CancellationToken cancellationToken)
        {
            var query = new GetLoggedInUserQuery();

            var result = await _sender.Send(query, cancellationToken);

            return Ok(result.Value);
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            RegisterUserRequest request,
            CancellationToken cancellationToken)
        {
            var command = new RegisterUserCommand(
                request.Email,
                request.FirstName,
                request.LastName,
                request.Password);

            var result = await _sender.Send(command, cancellationToken);

            if(result.IsFailure)
            {
                return BadRequest(result.Error);
            }

            return Ok(result.Value);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LogInUserRequest request, CancellationToken cancellationToken)
        {
            var command = new LogInUserCommand(
                request.Email,
                request.Password);

            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Unauthorized(result.Error);
            }

            return Ok(result.Value);
        }
    }
}
