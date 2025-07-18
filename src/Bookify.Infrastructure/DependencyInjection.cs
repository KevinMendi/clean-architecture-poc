﻿using Asp.Versioning;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Email;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Reviews;
using Bookify.Domain.Users;
using Bookify.Infrastructure.Authentication;
using Bookify.Infrastructure.Authorization;
using Bookify.Infrastructure.Caching;
using Bookify.Infrastructure.Clock;
using Bookify.Infrastructure.Data;
using Bookify.Infrastructure.Email;
using Bookify.Infrastructure.Outbox;
using Bookify.Infrastructure.Repositories;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace Bookify.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<IDateTimeProvider, DateTimeProvider>();
            services.AddTransient<IEmailService, EmailService>();

            AddPersistence(services, configuration);

            AddAuthentication(services, configuration);

            AddAuthorization(services);

            AddCaching(services, configuration);

            AddHealthChecks(services, configuration);

            AddApiVersioning(services);

            AddBackgroundJobs(services, configuration);

            return services;
        }

        private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

            services.Configure<AuthenticationOptions>(configuration.GetSection("Authentication"));
            services.ConfigureOptions<JwtBearerOptionsSetup>();

            services.Configure<KeycloakOptions>(configuration.GetSection("Keycloak"));

            services.AddTransient<AdminAuthorizationDelegatingHandler>();

            services.AddHttpClient<IAuthenticationService, AuthenticationService>((serviceProvider, httpclient) =>
            {
                var keycloakOptions = serviceProvider.GetRequiredService<IOptions<KeycloakOptions>>().Value;

                httpclient.BaseAddress = new Uri(keycloakOptions.AdminUrl);
            }).AddHttpMessageHandler<AdminAuthorizationDelegatingHandler>();

            services.AddHttpClient<IJwtService, JwtService>((serviceProvider, httpclient) =>
            {
                var keycloakOptions = serviceProvider.GetRequiredService<IOptions<KeycloakOptions>>().Value;

                httpclient.BaseAddress = new Uri(keycloakOptions.TokenUrl);
            });

            services.AddHttpContextAccessor();

            services.AddScoped<IUserContext, UserContext>();
        }

        private static void AddAuthorization(IServiceCollection services)
        {
            services.AddScoped<AuthorizationService>();

            services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, CustomClaimsTransformation>();

            services.AddTransient<IAuthorizationHandler, CustomPermissionAuthorizationHandler>();

            services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        }

        private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
        {
            var connectionString =
                configuration.GetConnectionString("Database") ?? throw new ArgumentNullException(nameof(configuration));

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
            });

            services.AddScoped<IUserRepository, UserRepository>();

            services.AddScoped<IApartmentRepository, ApartmentRepository>();

            services.AddScoped<IBookingRepository, BookingRepository>();

            services.AddScoped<IReviewRepository, ReviewRepository>();

            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

            services.AddSingleton<ISqlConnectionFactory>(_ =>
                new SqlConnectionFactory(connectionString));

            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        }

        private static void AddCaching(IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("Cache") ??
                                    throw new ArgumentNullException(nameof(configuration));

            services.AddStackExchangeRedisCache(options => options.Configuration = connectionString);

            services.AddSingleton<ICacheService, CacheService>();
        }

        private static void AddHealthChecks(IServiceCollection services, IConfiguration configuration)
        {
            services.AddHealthChecks()
                .AddNpgSql(configuration.GetConnectionString("Database"))
                .AddRedis(configuration.GetConnectionString("Cache"))
                .AddUrlGroup(new Uri(configuration["KeyCloak:BaseUrl"]), HttpMethod.Get, "keycloak");
        }

        private static void AddApiVersioning(IServiceCollection services)
        {
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1); //API version
                options.ReportApiVersions = true; //api-supported-versions and api-deprecated-versions will be added to all valid service routes 
                options.ApiVersionReader = new UrlSegmentApiVersionReader(); // API versioning via path
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V"; // e.g. v = v, V = wildcard
                options.SubstituteApiVersionInUrl = true;
            });
        }

        private static void AddBackgroundJobs(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));

            services.AddQuartz();

            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

            services.ConfigureOptions<ProcessOutboxMessagesJobSetup>();
        }
    }
}
