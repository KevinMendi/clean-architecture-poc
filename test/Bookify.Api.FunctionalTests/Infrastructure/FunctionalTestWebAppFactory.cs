﻿using Bookify.Api.FunctionalTests.Users;
using Bookify.Application.Abstractions.Data;
using Bookify.Infrastructure;
using Bookify.Infrastructure.Authentication;
using Bookify.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Bookify.Application.IntegrationTests.Infrastructure
{
    public class FunctionalTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .WithDatabase("bookify")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        private readonly RedisContainer _redisContainer = new RedisBuilder()
            .WithImage("redis:latest")
            .Build();

        private readonly KeycloakContainer _keycloakContainer = new KeycloakBuilder()
            .WithResourceMapping(
                new FileInfo(".files/bookify-realm-export.json"),
                new FileInfo("/opt/keycloak/data/import/realm.json"))
            .WithCommand("--import-realm")
            .Build();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));

                string connectionString = $"{_dbContainer.GetConnectionString()};Pooling=False";

                services.AddDbContext<ApplicationDbContext>(options =>
                    options
                        .UseNpgsql(connectionString)
                        .UseSnakeCaseNamingConvention());

                services.RemoveAll(typeof(ISqlConnectionFactory));

                services.AddSingleton<ISqlConnectionFactory>(_ =>
                    new SqlConnectionFactory(connectionString));

                services.Configure<RedisCacheOptions>(redisCacheOptions =>
                    redisCacheOptions.Configuration = _redisContainer.GetConnectionString());

                string? keycloakAddress = _keycloakContainer.GetBaseAddress();

                services.Configure<KeycloakOptions>(o =>
                {
                    o.AdminUrl = $"{keycloakAddress}admin/realms/bookify/";
                    o.TokenUrl = $"{keycloakAddress}realms/bookify/protocol/openid-connect/token";
                });

                services.Configure<AuthenticationOptions>(o =>
                {
                    o.Issuer = $"{keycloakAddress}realms/bookify/";
                    o.MetadataUrl = $"{keycloakAddress}realms/bookify/.well-known/openid-configuration";
                });
            });
        }

        public async Task InitializeAsync()
        {
            await _dbContainer.StartAsync();
            await _redisContainer.StartAsync();
            await _keycloakContainer.StartAsync();

            await InitializeTestUserAsync();
        }

        public new async Task DisposeAsync()
        {
            await _dbContainer.StopAsync();
            await _redisContainer.StopAsync();
            await _keycloakContainer.StopAsync();
        }

        private async Task InitializeTestUserAsync()
        {
            try
            {
                using HttpClient httpClient = CreateClient();

                await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.RegisterTestUserRequest);
            }
            catch (Exception)
            {
                // Do nothing.
                throw;
            }
        }
    }
}
