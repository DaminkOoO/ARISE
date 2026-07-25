using Arise.Application.Common.Abstractions;
using Arise.Application.Features.Hunters;
using Arise.Application.Features.Sport;
using Arise.Infrastructure.Agents;
using Arise.Infrastructure.Auth;
using Arise.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Arise.Infrastructure;

/// <summary>
/// Point d'entrée unique du câblage de la couche Infrastructure. L'API ne connaît donc ni
/// EF Core ni Npgsql : elle appelle <see cref="AddInfrastructure"/> et rien d'autre.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Budget d'attente accordé au Système. Le défaut d'un <see cref="HttpClient"/> est de 100
    /// secondes — 200 au pire pour l'agent de quêtes, qui réessaie une fois — alors que ces
    /// deux agents sont appelés sur un chemin où le Chasseur attend devant son écran. Au-delà
    /// de dix secondes, une quête de repli vaut mieux qu'une page qui tourne.
    /// </summary>
    private static readonly TimeSpan DelaiDAttenteGemini = TimeSpan.FromSeconds(10);

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
        services.AddScoped<IHunterProfileRepository, EfHunterProfileRepository>();
        services.AddScoped<IQuestRepository, EfQuestRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // L'émetteur de jetons est sans état : il lit ses paramètres via IOptions<JwtOptions>
        // et date depuis la TimeProvider partagée. La section Jwt de la configuration est liée
        // côté API (Program), là où le middleware JwtBearer lit la même clé pour valider.
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // Client HTTP typé : IHttpClientFactory gère le cycle de vie du handler sous-jacent,
        // l'agent lui-même reste transient. La section Gemini de la configuration est liée
        // côté API (Program), sur le même modèle que Jwt — la clé ne vit jamais ici.
        services.AddHttpClient<IOnboardingAgent, GeminiOnboardingAgent>((provider, client) =>
        {
            var gemini = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;
            client.BaseAddress = new Uri(gemini.BaseUrl);
            client.Timeout = DelaiDAttenteGemini;
        });

        services.AddHttpClient<IQuestGenerationAgent, GeminiQuestGenerationAgent>((provider, client) =>
        {
            var gemini = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;
            client.BaseAddress = new Uri(gemini.BaseUrl);
            client.Timeout = DelaiDAttenteGemini;
        });

        return services;
    }
}
