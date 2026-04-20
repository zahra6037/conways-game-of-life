using GameOfLife.Application.Ports;
using GameOfLife.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GameOfLife") 
                               ?? "Data Source=gameoflife.db";

        services.AddSingleton<IBoardRepository>(provider => 
            new SqliteBoardRepository(connectionString));

        return services;
    }
}