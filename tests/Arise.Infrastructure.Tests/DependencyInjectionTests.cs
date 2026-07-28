using Arise.Application.Common.Abstractions;
using Arise.Application.Features.Hunters;
using Arise.Application.Features.Sport;
using Arise.Infrastructure.Agents;
using Arise.Infrastructure.Auth;
using Arise.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests;

/// <summary>
/// Éprouve le câblage tel que l'API l'appellera : <c>AddInfrastructure</c> doit brancher les
/// contrats d'Application sur leurs implémentations EF/Identity, avec les bonnes durées de
/// vie. Aucune connexion n'est ouverte — ces tests tournent sans Docker.
/// </summary>
public class DependencyInjectionTests
{
    private static IServiceCollection Cablage() =>
        new ServiceCollection().AddInfrastructure("Host=hôte-inutilisé;Database=arise");

    // Le repository suit le DbContext (scoped) dont il dépend : un singleton capturerait un
    // contexte, un transient en gaspillerait.
    [Fact]
    public void Branche_le_repository_des_Chasseurs_sur_EF()
    {
        var descripteur = Cablage().Single(service => service.ServiceType == typeof(IUserRepository));

        descripteur.ImplementationType.Should().Be(typeof(EfUserRepository));
        descripteur.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    // Même durée de vie que le DbContext dont il dépend, pour la même raison.
    [Fact]
    public void Branche_le_repository_des_quetes_sur_EF()
    {
        var descripteur = Cablage().Single(service => service.ServiceType == typeof(IQuestRepository));

        descripteur.ImplementationType.Should().Be(typeof(EfQuestRepository));
        descripteur.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    // Même durée de vie que le DbContext dont il dépend, pour la même raison.
    [Fact]
    public void Branche_le_repository_du_journal_des_habitudes_sur_EF()
    {
        var descripteur = Cablage().Single(service => service.ServiceType == typeof(IHabitLogRepository));

        descripteur.ImplementationType.Should().Be(typeof(EfHabitLogRepository));
        descripteur.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    // Même durée de vie que le DbContext dont il dépend, pour la même raison.
    [Fact]
    public void Branche_le_repository_des_taches_sur_EF()
    {
        var descripteur = Cablage().Single(service => service.ServiceType == typeof(ITaskItemRepository));

        descripteur.ImplementationType.Should().Be(typeof(EfTaskItemRepository));
        descripteur.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    // Le hacheur est sans état : une seule instance suffit.
    [Fact]
    public void Branche_le_hacheur_sur_l_implementation_Identity()
    {
        var descripteur = Cablage().Single(service => service.ServiceType == typeof(IPasswordHasher));

        descripteur.ImplementationType.Should().Be(typeof(PasswordHasher));
        descripteur.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    // L'émetteur de jetons est sans état lui aussi : il ne dépend que d'options et d'une
    // horloge, toutes deux singletons.
    [Fact]
    public void Branche_l_emetteur_de_jetons_sur_l_implementation_JWT()
    {
        var descripteur = Cablage().Single(service => service.ServiceType == typeof(IJwtTokenGenerator));

        descripteur.ImplementationType.Should().Be(typeof(JwtTokenGenerator));
        descripteur.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    // Premier agent Gemini concret du dépôt : câblé en client HTTP typé (AddHttpClient), dont
    // le descripteur ne porte pas d'ImplementationType direct — il s'enregistre via une
    // fabrique. On résout donc réellement l'instance, ce qui éprouve aussi que la fabrique ne
    // lève pas (IOptions<GeminiOptions> résolvable même sans Configure explicite).
    [Fact]
    public void Branche_l_agent_d_onboarding_sur_l_implementation_Gemini()
    {
        using var fournisseur = Cablage().BuildServiceProvider();

        fournisseur.GetRequiredService<IOnboardingAgent>().Should().BeOfType<GeminiOnboardingAgent>();
    }

    // Même câblage en client HTTP typé que l'agent d'onboarding : on résout réellement
    // l'instance, ce qui éprouve du même coup que la fabrique ne lève pas.
    [Fact]
    public void Branche_l_agent_de_generation_de_quetes_sur_l_implementation_Gemini()
    {
        using var fournisseur = Cablage().BuildServiceProvider();

        fournisseur.GetRequiredService<IQuestGenerationAgent>()
            .Should().BeOfType<GeminiQuestGenerationAgent>();
    }

    // Le délai par défaut d'un HttpClient est de 100 secondes — soit 200 au pire avec la
    // nouvelle tentative de l'agent de quêtes, sur un chemin de lecture où le Chasseur attend
    // devant son écran. Mieux vaut le repli au bout de dix secondes.
    [Theory]
    [InlineData(nameof(IOnboardingAgent))]
    [InlineData(nameof(IQuestGenerationAgent))]
    public void Borne_le_delai_d_attente_des_clients_Gemini(string nomDuClient)
    {
        using var fournisseur = Cablage().BuildServiceProvider();

        var client = fournisseur.GetRequiredService<IHttpClientFactory>().CreateClient(nomDuClient);

        client.Timeout.Should().Be(TimeSpan.FromSeconds(10));
    }
}
