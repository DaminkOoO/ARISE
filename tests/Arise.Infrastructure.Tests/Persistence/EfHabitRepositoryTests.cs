using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Habits;
using Arise.Domain.Hunters;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// Éprouve le repository des habitudes sur un vrai Postgres : le round-trip d'une habitude
/// déclarée, la liste bornée au bon Chasseur, et le contrôle d'unicité tel que le contrat de
/// <see cref="IHabitRepository"/> le promet — insensible à la casse, aveugle aux archivées.
/// </summary>
[Collection(PostgresCollection.Nom)]
public class EfHabitRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Creation =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Une habitude appartient à un Chasseur existant : la clé étrangère l'exige, comme en
    /// production.
    /// </summary>
    private async Task<Guid> ChasseurPose()
    {
        var profil = HunterProfile.Create();

        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<IHunterProfileRepository>()
            .SaveAsync(profil, CancellationToken.None);

        return profil.Id;
    }

    private async Task<Habit> Declarer(
        Guid chasseur,
        string nom,
        HabitFrequency frequence = HabitFrequency.Quotidienne,
        bool archivee = false)
    {
        var habitude = Habit.Create(chasseur, nom, frequence, Creation);

        if (archivee)
        {
            habitude.Archive();
        }

        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<IHabitRepository>()
            .AddAsync(habitude, CancellationToken.None);

        return habitude;
    }

    private async Task<IReadOnlyList<Habit>> Relire(Guid chasseur)
    {
        await using var fournisseur = postgres.Fournisseur();
        return await fournisseur.GetRequiredService<IHabitRepository>()
            .GetForHunterAsync(chasseur, CancellationToken.None);
    }

    private async Task<Habit?> RelireParIdentifiant(Guid habitude)
    {
        await using var fournisseur = postgres.Fournisseur();
        return await fournisseur.GetRequiredService<IHabitRepository>()
            .GetByIdAsync(habitude, CancellationToken.None);
    }

    private async Task<bool> NomDejaPorte(Guid chasseur, string nom)
    {
        await using var fournisseur = postgres.Fournisseur();
        return await fournisseur.GetRequiredService<IHabitRepository>()
            .ExistsWithNameAsync(chasseur, nom, CancellationToken.None);
    }

    [Fact]
    public async Task Relit_une_habitude_declaree_depuis_un_contexte_neuf()
    {
        var chasseur = await ChasseurPose();
        var habitude = await Declarer(chasseur, "Boire deux litres d'eau");

        var relues = await Relire(chasseur);

        relues.Should().ContainSingle().Which.Id.Should().Be(habitude.Id);
    }

    [Fact]
    public async Task Relit_le_nom_tel_qu_il_a_ete_ecrit()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Lire vingt minutes");

        var relues = await Relire(chasseur);

        relues.Should().ContainSingle().Which.Name.Should().Be("Lire vingt minutes");
    }

    // Stockée en texte et non en entier : l'ordre des membres de l'énumération peut changer
    // avec les mécaniques de jeu, et un entier figerait cet ordre en base à l'insu du code.
    [Fact]
    public async Task Relit_le_rythme_tel_qu_il_a_ete_ecrit()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Faire le point du mois", HabitFrequency.Hebdomadaire);

        var relues = await Relire(chasseur);

        relues.Should().ContainSingle()
            .Which.Frequency.Should().Be(HabitFrequency.Hebdomadaire);
    }

    [Fact]
    public async Task Relit_l_instant_de_creation()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Méditer cinq minutes");

        var relues = await Relire(chasseur);

        relues.Should().ContainSingle().Which.CreatedAt.Should().Be(Creation);
    }

    [Fact]
    public async Task Relit_l_etat_range_d_une_habitude_archivee()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Courir le matin", archivee: true);

        var relues = await Relire(chasseur);

        relues.Should().ContainSingle().Which.IsArchived.Should().BeTrue();
    }

    // Le repository rend tout ce que le Chasseur a déclaré : ce qui s'affiche est la décision
    // de la requête de lecture, pas du stockage.
    [Fact]
    public async Task Rend_aussi_les_habitudes_archivees()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Étirements du soir");
        await Declarer(chasseur, "Marcher trente minutes", archivee: true);

        var relues = await Relire(chasseur);

        relues.Should().HaveCount(2);
    }

    [Fact]
    public async Task Ne_rend_pas_les_habitudes_d_un_autre_Chasseur()
    {
        var chasseur = await ChasseurPose();
        var autre = await ChasseurPose();
        await Declarer(autre, "Boire un thé");

        var relues = await Relire(chasseur);

        relues.Should().BeEmpty();
    }

    // Le chemin qu'emprunte la journalisation : elle a besoin du rythme et du rattachement de
    // l'habitude, pas de toute la liste du Chasseur.
    [Fact]
    public async Task Relit_une_habitude_par_son_identifiant()
    {
        var chasseur = await ChasseurPose();
        var habitude = await Declarer(chasseur, "Sortir prendre l'air", HabitFrequency.Hebdomadaire);

        var relue = await RelireParIdentifiant(habitude.Id);

        relue.Should().NotBeNull();
        relue!.Frequency.Should().Be(HabitFrequency.Hebdomadaire);
    }

    [Fact]
    public async Task Ne_relit_rien_pour_un_identifiant_inconnu()
    {
        (await RelireParIdentifiant(Guid.NewGuid())).Should().BeNull();
    }

    // Une habitude rangée existe toujours : c'est à l'appelant de décider ce qu'elle autorise, et
    // la journalisation la refuse avec un message qui lui est propre — la faire disparaître ici
    // la rendrait « introuvable », ce qui est un autre message et une autre vérité.
    [Fact]
    public async Task Relit_aussi_une_habitude_archivee_par_son_identifiant()
    {
        var chasseur = await ChasseurPose();
        var habitude = await Declarer(chasseur, "Relire mes notes", archivee: true);

        (await RelireParIdentifiant(habitude.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Reconnait_un_nom_deja_porte_par_le_Chasseur()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Ranger le bureau");

        (await NomDejaPorte(chasseur, "Ranger le bureau")).Should().BeTrue();
    }

    // « Courir » et « courir » désignent la même intention : les laisser coexister scinderait
    // en deux la série de ce qui n'en est qu'une.
    [Fact]
    public async Task Reconnait_un_nom_deja_porte_sans_distinction_de_casse()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Nager le mardi");

        (await NomDejaPorte(chasseur, "nager le mardi")).Should().BeTrue();
    }

    [Fact]
    public async Task Ne_reconnait_pas_le_nom_porte_par_un_autre_Chasseur()
    {
        var chasseur = await ChasseurPose();
        var autre = await ChasseurPose();
        await Declarer(autre, "Écrire trois pages");

        (await NomDejaPorte(chasseur, "Écrire trois pages")).Should().BeFalse();
    }

    // Un Chasseur qui range « Courir » en janvier doit pouvoir s'y remettre en mars sans que le
    // Système lui oppose une habitude qu'il ne voit plus nulle part.
    [Fact]
    public async Task Ne_compte_pas_une_habitude_archivee_dans_l_unicite()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Se coucher avant minuit", archivee: true);

        (await NomDejaPorte(chasseur, "Se coucher avant minuit")).Should().BeFalse();
    }

    // La lecture préalable du handler ne tranche pas une course entre deux déclarations
    // simultanées. C'est cet index qui la tranche, traduit en vocabulaire métier pour que la
    // couche Application n'ait pas à connaître Npgsql.
    [Fact]
    public async Task Refuse_deux_habitudes_actives_homonymes()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Faire dix pompes");

        var acte = async () => await Declarer(chasseur, "faire dix pompes");

        await acte.Should().ThrowAsync<HabitNameAlreadyTakenException>();
    }

    // Le pendant du filtre de l'index : ranger une habitude libère son nom, en base comme dans
    // le contrôle applicatif.
    [Fact]
    public async Task Accepte_de_redeclarer_une_habitude_precedemment_archivee()
    {
        var chasseur = await ChasseurPose();
        await Declarer(chasseur, "Tenir un journal", archivee: true);

        await Declarer(chasseur, "Tenir un journal");

        (await Relire(chasseur)).Should().HaveCount(2);
    }
}
