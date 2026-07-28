using Arise.Application.Common.Abstractions;
using Arise.Domain.Hunters;
using Arise.Domain.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// Éprouve le repository des tâches sur un vrai Postgres : le round-trip d'une tâche déclarée,
/// la liste bornée au bon Chasseur, et la persistance de la complétion — qui passe par un chemin
/// <b>suivi</b>, puisque cocher une tâche mute la ligne.
/// </summary>
[Collection(PostgresCollection.Nom)]
public class EfTaskItemRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Creation =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Tap =
        new(2026, 7, 27, 18, 15, 0, TimeSpan.Zero);

    private async Task<Guid> ChasseurPose()
    {
        var profil = HunterProfile.Create();

        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<IHunterProfileRepository>()
            .SaveAsync(profil, CancellationToken.None);

        return profil.Id;
    }

    private async Task<TaskItem> Declarer(
        Guid chasseur, string titre, DateOnly? echeance = null)
    {
        var tache = TaskItem.Create(chasseur, titre, echeance, Creation);

        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<ITaskItemRepository>()
            .AddAsync(tache, CancellationToken.None);

        return tache;
    }

    private async Task<IReadOnlyList<TaskItem>> Relire(Guid chasseur)
    {
        await using var fournisseur = postgres.Fournisseur();
        return await fournisseur.GetRequiredService<ITaskItemRepository>()
            .GetForHunterAsync(chasseur, CancellationToken.None);
    }

    private async Task<TaskItem?> RelireParIdentifiant(Guid tache)
    {
        await using var fournisseur = postgres.Fournisseur();
        return await fournisseur.GetRequiredService<ITaskItemRepository>()
            .GetByIdAsync(tache, CancellationToken.None);
    }

    private async Task Cocher(TaskItem tache, DateTimeOffset instant)
    {
        await using var fournisseur = postgres.Fournisseur();
        var taches = fournisseur.GetRequiredService<ITaskItemRepository>();
        var chargee = await taches.GetByIdAsync(tache.Id, CancellationToken.None);
        chargee!.Complete(instant);
        await taches.SaveAsync(chargee, CancellationToken.None);
    }

    private async Task<int> CompteesEntre(
        Guid chasseur, DateTimeOffset debut, DateTimeOffset fin)
    {
        await using var fournisseur = postgres.Fournisseur();
        return await fournisseur.GetRequiredService<ITaskItemRepository>()
            .CountCompletedBetweenAsync(chasseur, debut, fin, CancellationToken.None);
    }

    [Fact]
    public async Task Relit_une_tache_declaree_depuis_un_contexte_neuf()
    {
        var chasseur = await ChasseurPose();
        var tache = await Declarer(chasseur, "Appeler le dentiste");

        (await Relire(chasseur)).Should().ContainSingle().Which.Id.Should().Be(tache.Id);
    }

    [Fact]
    public async Task Relit_le_titre_tel_qu_il_a_ete_ecrit()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Envoyer les documents");

        (await Relire(chasseur)).Should().ContainSingle()
            .Which.Title.Should().Be("Envoyer les documents");
    }

    [Fact]
    public async Task Relit_l_echeance_telle_qu_elle_a_ete_ecrite()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Payer le loyer", new DateOnly(2026, 8, 1));

        (await Relire(chasseur)).Should().ContainSingle()
            .Which.DueDate.Should().Be(new DateOnly(2026, 8, 1));
    }

    // L'absence d'échéance doit survivre à l'aller-retour : une colonne non nullable la
    // remplacerait par une date par défaut, et l'écran afficherait un retard inventé.
    [Fact]
    public async Task Relit_l_absence_d_echeance()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Ranger le garage");

        (await Relire(chasseur)).Should().ContainSingle().Which.DueDate.Should().BeNull();
    }

    [Fact]
    public async Task Relit_l_instant_de_creation()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Trier les papiers");

        (await Relire(chasseur)).Should().ContainSingle()
            .Which.CreatedAt.Should().Be(Creation);
    }

    [Fact]
    public async Task Relit_une_tache_neuve_comme_non_faite()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Réserver le train");

        (await Relire(chasseur)).Should().ContainSingle()
            .Which.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Ne_rend_pas_les_taches_d_un_autre_Chasseur()
    {
        var chasseur = await ChasseurPose();
        var autre = await ChasseurPose();
        await Declarer(autre, "Arroser les plantes");

        (await Relire(chasseur)).Should().BeEmpty();
    }

    [Fact]
    public async Task Relit_une_tache_par_son_identifiant()
    {
        var chasseur = await ChasseurPose();
        var tache = await Declarer(chasseur, "Changer l'ampoule");

        (await RelireParIdentifiant(tache.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Ne_relit_rien_pour_un_identifiant_inconnu()
    {
        (await RelireParIdentifiant(Guid.NewGuid())).Should().BeNull();
    }

    // Le chemin de la complétion : charger, muter, re-sauvegarder dans le même scope. C'est le
    // suivi de modifications d'EF Core qui doit détecter la mutation — sans lui, cocher une tâche
    // n'écrirait rien et le Chasseur la retrouverait à faire au rafraîchissement suivant.
    [Fact]
    public async Task Persiste_la_completion_d_une_tache_chargee_par_son_identifiant()
    {
        var chasseur = await ChasseurPose();
        var tache = await Declarer(chasseur, "Rendre les livres");

        await using (var fournisseur = postgres.Fournisseur())
        {
            var taches = fournisseur.GetRequiredService<ITaskItemRepository>();
            var chargee = await taches.GetByIdAsync(tache.Id, CancellationToken.None);

            chargee!.Complete(Tap);

            await taches.SaveAsync(chargee, CancellationToken.None);
        }

        var relue = await RelireParIdentifiant(tache.Id);

        relue!.IsCompleted.Should().BeTrue();
        relue.CompletedAt.Should().Be(Tap);
    }

    // --- Comptage de la fenêtre : ce dont le plafond d'XP d'engagement se nourrit ------------

    [Fact]
    public async Task Compte_une_tache_cochee_dans_la_fenetre()
    {
        var chasseur = await ChasseurPose();
        var tache = await Declarer(chasseur, "Régler la facture");
        await Cocher(tache, Tap);

        (await CompteesEntre(chasseur, Tap.AddHours(-1), Tap.AddHours(1))).Should().Be(1);
    }

    [Fact]
    public async Task Ne_compte_pas_une_tache_jamais_cochee()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Prendre rendez-vous");

        (await CompteesEntre(chasseur, Tap.AddHours(-1), Tap.AddHours(1))).Should().Be(0);
    }

    [Fact]
    public async Task Ne_compte_pas_une_tache_cochee_avant_la_fenetre()
    {
        var chasseur = await ChasseurPose();
        var tache = await Declarer(chasseur, "Sortir les poubelles");
        await Cocher(tache, Tap);

        (await CompteesEntre(chasseur, Tap.AddHours(1), Tap.AddHours(2))).Should().Be(0);
    }

    // Borne haute exclue : minuit appartient au jour qui commence. Inclusive des deux côtés, une
    // tâche cochée à minuit pile compterait dans deux journées — et vaudrait deux fois son XP.
    [Fact]
    public async Task Exclut_l_instant_de_fin_de_fenetre()
    {
        var chasseur = await ChasseurPose();
        var tache = await Declarer(chasseur, "Relever le courrier");
        await Cocher(tache, Tap);

        (await CompteesEntre(chasseur, Tap.AddHours(-1), Tap)).Should().Be(0);
    }

    [Fact]
    public async Task Inclut_l_instant_de_debut_de_fenetre()
    {
        var chasseur = await ChasseurPose();
        var tache = await Declarer(chasseur, "Arroser les plantes");
        await Cocher(tache, Tap);

        (await CompteesEntre(chasseur, Tap, Tap.AddHours(1))).Should().Be(1);
    }

    // Le plafond est celui du Chasseur : sans ce filtre, les gestes de tous épuiseraient le sien.
    [Fact]
    public async Task Ne_compte_pas_les_taches_d_un_autre_Chasseur()
    {
        var chasseur = await ChasseurPose();
        var autre = await ChasseurPose();
        var tache = await Declarer(autre, "Répondre au courriel");
        await Cocher(tache, Tap);

        (await CompteesEntre(chasseur, Tap.AddHours(-1), Tap.AddHours(1))).Should().Be(0);
    }

    // Le repository rend tout ce que le Chasseur a déclaré : ce qui s'affiche est la décision de
    // la requête de lecture, pas du stockage.
    [Fact]
    public async Task Rend_aussi_les_taches_deja_faites()
    {
        var chasseur = await ChasseurPose();
        var tache = await Declarer(chasseur, "Déclarer les impôts");

        await using (var fournisseur = postgres.Fournisseur())
        {
            var taches = fournisseur.GetRequiredService<ITaskItemRepository>();
            var chargee = await taches.GetByIdAsync(tache.Id, CancellationToken.None);
            chargee!.Complete(Tap);
            await taches.SaveAsync(chargee, CancellationToken.None);
        }

        (await Relire(chasseur)).Should().ContainSingle();
    }
}
