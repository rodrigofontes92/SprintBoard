using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SprintBoard.Api.Data;

namespace SprintBoard.Api.Tests;

public sealed class SprintBoardApiFactory : WebApplicationFactory<global::Program>
{
    private const string TestConnectionString = "Host=localhost;Database=sprintboard_tests;Username=test;Password=test";
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__DefaultConnection";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly object _databaseLock = new();
    private readonly string? _originalConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
    private bool _databaseInitialized;

    public SprintBoardApiFactory()
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, TestConnectionString);
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public HttpClient CreateApiClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

        EnsureDatabase();
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
            Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, _originalConnectionString);
        }

        base.Dispose(disposing);
    }

    private void EnsureDatabase()
    {
        lock (_databaseLock)
        {
            if (_databaseInitialized)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();
            _databaseInitialized = true;
        }
    }
}
