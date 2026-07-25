using Arise.Application.Common.Abstractions;
using Arise.Domain.Hunters;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// L'unité de travail, éprouvée sur un vrai Postgres. Le provider InMemory ne connaît pas les
/// transactions : il rendrait ces tests verts sans rien annuler du tout, ce qui est exactement
/// le mensonge qu'on ne peut pas se permettre ici.
/// </summary>
[Collection(PostgresCollection.Nom)]
public class EfUnitOfWorkTests(PostgresFixture postgres)
{
    private async Task<HunterProfile?> ProfilRelu(Guid identifiant)
    {
        await using var fournisseur = postgres.Fournisseur();

        return await fournisseur.GetRequiredService<IHunterProfileRepository>()
            .GetByIdAsync(identifiant, CancellationToken.None);
    }

    [Fact]
    public void N_annonce_aucune_transaction_en_cours_au_depart()
    {
        using var fournisseur = postgres.Fournisseur();

        fournisseur.GetRequiredService<IUnitOfWork>().TransactionEnCours.Should().BeFalse();
    }

    // Ce que lit TransactionBehavior pour savoir s'il est la commande la plus externe : sans
    // cette bascule, chaque commande imbriquée rouvrirait et revaliderait la sienne.
    [Fact]
    public async Task Annonce_une_transaction_en_cours_apres_l_avoir_commencee()
    {
        await using var fournisseur = postgres.Fournisseur();
        var uniteDeTravail = fournisseur.GetRequiredService<IUnitOfWork>();

        await uniteDeTravail.CommencerAsync(CancellationToken.None);

        uniteDeTravail.TransactionEnCours.Should().BeTrue();
    }

    [Fact]
    public async Task N_annonce_plus_de_transaction_en_cours_apres_validation()
    {
        await using var fournisseur = postgres.Fournisseur();
        var uniteDeTravail = fournisseur.GetRequiredService<IUnitOfWork>();

        await uniteDeTravail.CommencerAsync(CancellationToken.None);
        await uniteDeTravail.ValiderAsync(CancellationToken.None);

        uniteDeTravail.TransactionEnCours.Should().BeFalse();
    }

    [Fact]
    public async Task N_annonce_plus_de_transaction_en_cours_apres_annulation()
    {
        await using var fournisseur = postgres.Fournisseur();
        var uniteDeTravail = fournisseur.GetRequiredService<IUnitOfWork>();

        await uniteDeTravail.CommencerAsync(CancellationToken.None);
        await uniteDeTravail.AnnulerAsync(CancellationToken.None);

        uniteDeTravail.TransactionEnCours.Should().BeFalse();
    }

    [Fact]
    public async Task Rend_durables_les_ecritures_validees()
    {
        var profil = HunterProfile.Create();

        await using (var fournisseur = postgres.Fournisseur())
        {
            var uniteDeTravail = fournisseur.GetRequiredService<IUnitOfWork>();
            await uniteDeTravail.CommencerAsync(CancellationToken.None);

            await fournisseur.GetRequiredService<IHunterProfileRepository>()
                .SaveAsync(profil, CancellationToken.None);

            await uniteDeTravail.ValiderAsync(CancellationToken.None);
        }

        (await ProfilRelu(profil.Id)).Should().NotBeNull();
    }

    // Le test qui porte tout le reste : un SaveChangesAsync déjà exécuté doit pouvoir être
    // défait. Relu depuis un contexte neuf, pas depuis le cache d'identité de l'écriture —
    // celui-ci garderait l'instance en mémoire quoi qu'ait fait la base.
    [Fact]
    public async Task Efface_les_ecritures_annulees()
    {
        var profil = HunterProfile.Create();

        await using (var fournisseur = postgres.Fournisseur())
        {
            var uniteDeTravail = fournisseur.GetRequiredService<IUnitOfWork>();
            await uniteDeTravail.CommencerAsync(CancellationToken.None);

            await fournisseur.GetRequiredService<IHunterProfileRepository>()
                .SaveAsync(profil, CancellationToken.None);

            await uniteDeTravail.AnnulerAsync(CancellationToken.None);
        }

        (await ProfilRelu(profil.Id)).Should().BeNull();
    }
}
