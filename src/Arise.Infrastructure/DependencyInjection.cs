using Arise.Application.Common.Abstractions;
using Arise.Infrastructure.Auth;
using Arise.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure;

/// <summary>
/// Point d'entrée unique du câblage de la couche Infrastructure. L'API ne connaît donc ni
/// EF Core ni Npgsql : elle appelle <see cref="AddInfrastructure"/> et rien d'autre.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AriseDbContext>(options => options
            .UseNpgsql(connectionString)
            // La convention est posée ici, sur le même chemin que celui de l'API : la
            // déclarer à côté du modèle dans un test aurait éprouvé le test, pas le câblage.
            .UseSnakeCaseNamingConvention());

        // Le repository suit la durée de vie du DbContext (scoped) dont il dépend ; le hacheur
        // est sans état, une seule instance suffit.
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // L'émetteur de jetons est sans état : il lit ses paramètres via IOptions<JwtOptions>
        // et date depuis la TimeProvider partagée. La section Jwt de la configuration est liée
        // côté API (Program), là où le middleware JwtBearer lit la même clé pour valider.
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
