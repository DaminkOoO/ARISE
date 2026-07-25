using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Hunters;
using Arise.Domain.Quests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Infrastructure.Tests.Persistence;

/// <summary>
/// Éprouve le repository des quêtes sur un vrai Postgres : le round-trip d'une quête posée, la
/// recherche bornée au bon Chasseur, au bon domaine et au bon jour, et la contrainte qui
/// interdit deux quêtes du même jour — celle qui fait tenir la promesse « une seule génération
/// par jour » même si un second chemin d'écriture apparaissait.
/// </summary>
[Collection(PostgresCollection.Nom)]
public class EfQuestRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateOnly Jour = new(2026, 7, 26);

    /// <summary>
    /// Une quête vise un Chasseur existant : la clé étrangère l'exige, comme en production.
    /// </summary>
    private async Task<Guid> ChasseurPose()
    {
        var profil = HunterProfile.Create();

        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<IHunterProfileRepository>()
            .SaveAsync(profil, CancellationToken.None);

        return profil.Id;
    }

    private static Quest Quete(
        Guid chasseur,
        QuestDomain domaine = QuestDomain.Sport,
        DateOnly? jour = null,
        string titre = "L'Épreuve du Guerrier") =>
        Quest.Generate(
            chasseur,
            domaine,
            jour ?? Jour,
            titre,
            "Bouge à ton rythme, Chasseur : marche, gainage, étirements.",
            QuestType.Quotidienne,
            QuestStat.Force,
            QuestDifficulty.Moyenne,
            20,
            isFallback: false);

    private async Task Poser(Quest quete)
    {
        await using var fournisseur = postgres.Fournisseur();
        await fournisseur.GetRequiredService<IQuestRepository>()
            .SaveAsync(quete, CancellationToken.None);
    }

    private async Task<Quest?> Relire(Guid chasseur, QuestDomain domaine, DateOnly jour)
    {
        await using var fournisseur = postgres.Fournisseur();
        return await fournisseur.GetRequiredService<IQuestRepository>()
            .GetForDayAsync(chasseur, domaine, jour, CancellationToken.None);
    }

    [Fact]
    public async Task Relit_une_quete_posee_depuis_un_contexte_neuf()
    {
        var chasseur = await ChasseurPose();
        var quete = Quete(chasseur);
        await Poser(quete);

        var relue = await Relire(chasseur, QuestDomain.Sport, Jour);

        relue.Should().NotBeNull();
        relue!.Id.Should().Be(quete.Id);
    }

    [Fact]
    public async Task Relit_le_texte_de_la_quete_tel_qu_il_a_ete_pose()
    {
        var chasseur = await ChasseurPose();
        var quete = Quete(chasseur, titre: "L'Éveil du Corps");
        await Poser(quete);

        var relue = await Relire(chasseur, QuestDomain.Sport, Jour);

        relue!.Title.Should().Be("L'Éveil du Corps");
        relue.Description.Should().Be(quete.Description);
    }

    // Les énumérations sont stockées en texte : une ligne lue « Sport » ou « Force » en SQL
    // brut est plus sûre à auditer qu'un « 0 », et l'ordre des membres peut évoluer avec les
    // mécaniques de jeu sans réécrire la base.
    [Fact]
    public async Task Relit_le_type_la_statistique_et_la_difficulte_de_la_quete()
    {
        var chasseur = await ChasseurPose();
        await Poser(Quete(chasseur));

        var relue = await Relire(chasseur, QuestDomain.Sport, Jour);

        relue!.Type.Should().Be(QuestType.Quotidienne);
        relue.StatTarget.Should().Be(QuestStat.Force);
        relue.Difficulty.Should().Be(QuestDifficulty.Moyenne);
    }

    [Fact]
    public async Task Relit_la_recompense_et_la_date_de_la_quete()
    {
        var chasseur = await ChasseurPose();
        await Poser(Quete(chasseur));

        var relue = await Relire(chasseur, QuestDomain.Sport, Jour);

        relue!.XpReward.Should().Be(20);
        relue.QuestDate.Should().Be(Jour);
    }

    // Le drapeau de repli est persisté, pas recalculé : sans lui, la même quête serait annoncée
    // « de repli » le jour de sa génération puis « générée » à la relecture du lendemain.
    [Fact]
    public async Task Relit_le_drapeau_de_repli()
    {
        var chasseur = await ChasseurPose();
        var quete = Quest.Generate(
            chasseur,
            QuestDomain.Sport,
            Jour,
            "Éveil du Corps",
            "Bouge à ton rythme aujourd'hui, Chasseur.",
            QuestType.Quotidienne,
            QuestStat.Force,
            QuestDifficulty.Facile,
            10,
            isFallback: true);
        await Poser(quete);

        var relue = await Relire(chasseur, QuestDomain.Sport, Jour);

        relue!.IsFallback.Should().BeTrue();
    }

    [Fact]
    public async Task Relit_une_quete_fraichement_posee_comme_non_completee()
    {
        var chasseur = await ChasseurPose();
        await Poser(Quete(chasseur));

        var relue = await Relire(chasseur, QuestDomain.Sport, Jour);

        relue!.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Rend_null_quand_aucune_quete_n_est_posee_ce_jour_la()
    {
        var chasseur = await ChasseurPose();

        var relue = await Relire(chasseur, QuestDomain.Sport, Jour);

        relue.Should().BeNull();
    }

    [Fact]
    public async Task Ne_rend_pas_la_quete_de_la_veille()
    {
        var chasseur = await ChasseurPose();
        await Poser(Quete(chasseur, jour: Jour.AddDays(-1)));

        var relue = await Relire(chasseur, QuestDomain.Sport, Jour);

        relue.Should().BeNull();
    }

    [Fact]
    public async Task Ne_rend_pas_la_quete_d_un_autre_Chasseur()
    {
        var chasseur = await ChasseurPose();
        var autreChasseur = await ChasseurPose();
        await Poser(Quete(autreChasseur));

        var relue = await Relire(chasseur, QuestDomain.Sport, Jour);

        relue.Should().BeNull();
    }

    // Le domaine borne la recherche comme il borne l'unicité : la quête d'Habitudes du jour ne
    // doit ni être rendue à l'écran Sport, ni empêcher celle du Sport d'exister.
    [Fact]
    public async Task Ne_rend_pas_la_quete_d_un_autre_domaine_pose_le_meme_jour()
    {
        var chasseur = await ChasseurPose();
        await Poser(Quete(chasseur, domaine: QuestDomain.Habitudes));

        var relue = await Relire(chasseur, QuestDomain.Sport, Jour);

        relue.Should().BeNull();
    }

    [Fact]
    public async Task Accepte_deux_quetes_de_domaines_differents_le_meme_jour()
    {
        var chasseur = await ChasseurPose();
        await Poser(Quete(chasseur, domaine: QuestDomain.Sport, titre: "Quête de Sport"));
        await Poser(Quete(chasseur, domaine: QuestDomain.Habitudes, titre: "Quête d'Habitudes"));

        var relue = await Relire(chasseur, QuestDomain.Habitudes, Jour);

        relue!.Title.Should().Be("Quête d'Habitudes");
    }

    // La contrainte vit en base, pas seulement dans le handler : un validator se contourne par
    // un second chemin d'écriture (worker de briefing, seed, deux requêtes concurrentes au
    // premier réveil du matin), un index unique non.
    //
    // Traduite dans le vocabulaire métier comme EfUserRepository le fait de la violation
    // d'unicité des noms : une DbUpdateException nue laisserait le handler d'écriture sans
    // moyen de rattraper la course sans connaître Npgsql, et retomberait sur un 500 en anglais.
    [Fact]
    public async Task Refuse_une_deuxieme_quete_pour_le_meme_Chasseur_le_meme_jour_et_le_meme_domaine()
    {
        var chasseur = await ChasseurPose();
        await Poser(Quete(chasseur, titre: "Première quête"));

        var acte = () => Poser(Quete(chasseur, titre: "Seconde quête du même jour"));

        await acte.Should().ThrowAsync<QuestAlreadyPosedException>();
    }

    [Fact]
    public async Task Refuse_une_quete_visant_un_Chasseur_inexistant()
    {
        var acte = () => Poser(Quete(Guid.NewGuid()));

        await acte.Should().ThrowAsync<DbUpdateException>();
    }

    // ---------------------------------------------------------------------------------------
    // Recherche par identifiant : le chemin de la complétion. Le Chasseur tape sur la quête
    // qu'il a sous les yeux, dont l'écran connaît l'identifiant — pas le jour ni le domaine.
    // ---------------------------------------------------------------------------------------

    private async Task<Quest?> RelireParIdentifiant(Guid identifiant)
    {
        await using var fournisseur = postgres.Fournisseur();
        return await fournisseur.GetRequiredService<IQuestRepository>()
            .GetByIdAsync(identifiant, CancellationToken.None);
    }

    [Fact]
    public async Task Relit_une_quete_par_son_identifiant()
    {
        var chasseur = await ChasseurPose();
        var quete = Quete(chasseur, titre: "Quête à retrouver par identifiant");
        await Poser(quete);

        var relue = await RelireParIdentifiant(quete.Id);

        relue.Should().NotBeNull();
        relue!.Title.Should().Be("Quête à retrouver par identifiant");
    }

    // Le handler de complétion compare ce rattachement à celui de la commande : sans lui,
    // n'importe quel Chasseur complèterait les quêtes des autres.
    [Fact]
    public async Task Relit_le_Chasseur_auquel_la_quete_est_rattachee()
    {
        var chasseur = await ChasseurPose();
        var quete = Quete(chasseur);
        await Poser(quete);

        var relue = await RelireParIdentifiant(quete.Id);

        relue!.HunterProfileId.Should().Be(chasseur);
    }

    [Fact]
    public async Task Rend_null_en_cherchant_une_quete_absente()
    {
        var relue = await RelireParIdentifiant(Guid.NewGuid());

        relue.Should().BeNull();
    }

    // La complétion est la seconde écriture d'une quête déjà posée : le repository doit détecter
    // la mutation d'une entité qu'il a lui-même chargée, sans ré-insertion ni comparaison
    // manuelle de champs.
    [Fact]
    public async Task Relit_la_completion_d_une_quete_depuis_un_contexte_neuf()
    {
        var chasseur = await ChasseurPose();
        var quete = Quete(chasseur);
        await Poser(quete);

        await using (var completion = postgres.Fournisseur())
        {
            var repository = completion.GetRequiredService<IQuestRepository>();
            var chargee = await repository.GetByIdAsync(quete.Id, CancellationToken.None);
            chargee!.Complete(new DateTimeOffset(2026, 7, 26, 23, 30, 0, TimeSpan.FromHours(-4)));
            await repository.SaveAsync(chargee, CancellationToken.None);
        }

        var relue = await RelireParIdentifiant(quete.Id);

        relue!.IsCompleted.Should().BeTrue();
    }

    // Npgsql refuse d'écrire un DateTimeOffset décalé dans un timestamptz : l'entité normalise
    // en UTC, et c'est cet instant absolu qu'on doit retrouver.
    [Fact]
    public async Task Relit_l_instant_de_completion_en_UTC()
    {
        var chasseur = await ChasseurPose();
        var quete = Quete(chasseur);
        await Poser(quete);

        await using (var completion = postgres.Fournisseur())
        {
            var repository = completion.GetRequiredService<IQuestRepository>();
            var chargee = await repository.GetByIdAsync(quete.Id, CancellationToken.None);
            chargee!.Complete(new DateTimeOffset(2026, 7, 26, 23, 30, 0, TimeSpan.FromHours(-4)));
            await repository.SaveAsync(chargee, CancellationToken.None);
        }

        var relue = await RelireParIdentifiant(quete.Id);

        relue!.CompletedAt.Should().Be(new DateTimeOffset(2026, 7, 27, 3, 30, 0, TimeSpan.Zero));
    }

    // ---------------------------------------------------------------------------------------
    // Deux complétions simultanées. La garde d'idempotence de l'entité est en mémoire, et deux
    // requêtes concurrentes ont chacune leur scope, donc leur DbContext : les deux lisent une
    // quête non complétée, les deux voient Complete() rendre true, les deux créditent l'XP.
    // Seule la base peut trancher — d'où le jeton de concurrence.
    //
    // Les deux lectures sont faites avant la première écriture : la course est ici reproduite
    // à coup sûr, là où deux tâches lancées en parallèle ne l'attraperaient qu'une fois sur
    // trois.
    // ---------------------------------------------------------------------------------------

    private static readonly DateTimeOffset InstantDeCompletion =
        new(2026, 7, 26, 23, 30, 0, TimeSpan.FromHours(-4));

    [Fact]
    public async Task Refuse_la_seconde_ecriture_de_deux_completions_simultanees()
    {
        var chasseur = await ChasseurPose();
        var quete = Quete(chasseur);
        await Poser(quete);

        await using var premier = postgres.Fournisseur();
        await using var second = postgres.Fournisseur();
        var repositoryPremier = premier.GetRequiredService<IQuestRepository>();
        var repositorySecond = second.GetRequiredService<IQuestRepository>();
        var vuePremier = await repositoryPremier.GetByIdAsync(quete.Id, CancellationToken.None);
        var vueSecond = await repositorySecond.GetByIdAsync(quete.Id, CancellationToken.None);
        vuePremier!.Complete(InstantDeCompletion);
        vueSecond!.Complete(InstantDeCompletion.AddMinutes(1));
        await repositoryPremier.SaveAsync(vuePremier, CancellationToken.None);

        var acte = () => repositorySecond.SaveAsync(vueSecond, CancellationToken.None);

        await acte.Should().ThrowAsync<ConcurrentQuestUpdateException>();
    }

    // La complétion gagnante est la première : celle du perdant ne doit pas écraser son instant.
    [Fact]
    public async Task Conserve_l_instant_de_la_completion_gagnante()
    {
        var chasseur = await ChasseurPose();
        var quete = Quete(chasseur);
        await Poser(quete);

        await using var premier = postgres.Fournisseur();
        await using var second = postgres.Fournisseur();
        var repositoryPremier = premier.GetRequiredService<IQuestRepository>();
        var repositorySecond = second.GetRequiredService<IQuestRepository>();
        var vuePremier = await repositoryPremier.GetByIdAsync(quete.Id, CancellationToken.None);
        var vueSecond = await repositorySecond.GetByIdAsync(quete.Id, CancellationToken.None);
        vuePremier!.Complete(InstantDeCompletion);
        vueSecond!.Complete(InstantDeCompletion.AddMinutes(1));
        await repositoryPremier.SaveAsync(vuePremier, CancellationToken.None);

        var acte = () => repositorySecond.SaveAsync(vueSecond, CancellationToken.None);

        await acte.Should().ThrowAsync<ConcurrentQuestUpdateException>();
        (await RelireParIdentifiant(quete.Id))!.CompletedAt
            .Should().Be(InstantDeCompletion.ToUniversalTime());
    }

    // Le perdant repart avec l'état gagnant en main : c'est ce qui permet au handler d'annoncer
    // « déjà accomplie » avec le bon instant, plutôt qu'avec celui de sa propre tentative.
    [Fact]
    public async Task Rafraichit_la_quete_perdante_avec_l_etat_gagnant()
    {
        var chasseur = await ChasseurPose();
        var quete = Quete(chasseur);
        await Poser(quete);

        await using var premier = postgres.Fournisseur();
        await using var second = postgres.Fournisseur();
        var repositoryPremier = premier.GetRequiredService<IQuestRepository>();
        var repositorySecond = second.GetRequiredService<IQuestRepository>();
        var vuePremier = await repositoryPremier.GetByIdAsync(quete.Id, CancellationToken.None);
        var vueSecond = await repositorySecond.GetByIdAsync(quete.Id, CancellationToken.None);
        vuePremier!.Complete(InstantDeCompletion);
        vueSecond!.Complete(InstantDeCompletion.AddMinutes(1));
        await repositoryPremier.SaveAsync(vuePremier, CancellationToken.None);

        var acte = () => repositorySecond.SaveAsync(vueSecond, CancellationToken.None);

        await acte.Should().ThrowAsync<ConcurrentQuestUpdateException>();
        vueSecond.CompletedAt.Should().Be(InstantDeCompletion.ToUniversalTime());
    }
}
