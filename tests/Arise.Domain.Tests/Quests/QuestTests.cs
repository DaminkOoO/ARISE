using Arise.Domain.Quests;
using FluentAssertions;

namespace Arise.Domain.Tests.Quests;

/// <summary>
/// La quête telle qu'elle est posée en base après génération. Le contrat de sortie du modèle
/// (doc mécaniques, section 3) y arrive déjà validé par l'agent ; l'entité rejoue néanmoins la
/// cohérence récompense/difficulté, parce que l'agent n'est pas le seul chemin d'écriture
/// possible — un seed, un import ou une future quête de pénalité générée par le worker
/// passeront par ici sans repasser par lui.
/// </summary>
public class QuestTests
{
    private static readonly Guid Chasseur = Guid.NewGuid();

    private static readonly DateOnly Jour = new(2026, 7, 25);

    private static Quest Generer(
        string titre = "L'Épreuve du Guerrier",
        string description = "Bouge à ton rythme aujourd'hui : marche, gainage, étirements.",
        QuestType type = QuestType.Quotidienne,
        QuestDifficulty difficulte = QuestDifficulty.Moyenne,
        int xp = 20,
        bool estRepli = false) =>
        Quest.Generate(
            Chasseur,
            QuestDomain.Sport,
            Jour,
            titre,
            description,
            type,
            QuestStat.Force,
            difficulte,
            xp,
            estRepli);

    [Fact]
    public void Genere_une_quete_rattachee_au_Chasseur_vise()
    {
        Generer().HunterProfileId.Should().Be(Chasseur);
    }

    [Fact]
    public void Genere_une_quete_datee_du_jour_demande()
    {
        Generer().QuestDate.Should().Be(Jour);
    }

    [Fact]
    public void Genere_une_quete_dans_le_domaine_demande()
    {
        Generer().Domain.Should().Be(QuestDomain.Sport);
    }

    [Fact]
    public void Genere_une_quete_dotee_d_un_identifiant()
    {
        Generer().Id.Should().NotBeEmpty();
    }

    // L'état de complétion existe dès la génération pour que CompleteGymQuestCommand n'ait
    // pas à faire évoluer le schéma : une quête naît simplement non complétée.
    [Fact]
    public void Genere_une_quete_non_completee()
    {
        Generer().IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void Genere_une_quete_sans_instant_de_completion()
    {
        Generer().CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Conserve_la_recompense_annoncee()
    {
        Generer(difficulte: QuestDifficulty.Difficile, xp: 30).XpReward.Should().Be(30);
    }

    [Fact]
    public void Retient_qu_une_quete_vient_d_un_repli()
    {
        Generer(estRepli: true).IsFallback.Should().BeTrue();
    }

    [Fact]
    public void Ne_marque_pas_repli_une_quete_reellement_generee()
    {
        Generer().IsFallback.Should().BeFalse();
    }

    // Les espaces de bordure sont invisibles à l'affichage mais comptent dans la longueur de
    // colonne ; le titre stocké est celui qui sera lu à l'écran.
    [Fact]
    public void Rogne_les_espaces_de_bordure_du_titre()
    {
        Generer(titre: "  L'Épreuve du Guerrier  ").Title.Should().Be("L'Épreuve du Guerrier");
    }

    [Fact]
    public void Rogne_les_espaces_de_bordure_de_la_description()
    {
        Generer(description: "  Marche à ton rythme.  ").Description.Should().Be("Marche à ton rythme.");
    }

    [Fact]
    public void Refuse_un_Chasseur_sans_identifiant()
    {
        var acte = () => Quest.Generate(
            Guid.Empty,
            QuestDomain.Sport,
            Jour,
            "L'Épreuve du Guerrier",
            "Marche à ton rythme.",
            QuestType.Quotidienne,
            QuestStat.Force,
            QuestDifficulty.Moyenne,
            20,
            isFallback: false);

        acte.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_titre_vide(string titre)
    {
        var acte = () => Generer(titre: titre);

        acte.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_une_description_vide(string description)
    {
        var acte = () => Generer(description: description);

        acte.Should().Throw<ArgumentException>();
    }

    // La borne est aussi la largeur de la colonne : la faire respecter ici évite qu'un chemin
    // d'écriture qui ne passe pas par l'agent construise une quête que le Domain accepte et
    // que PostgreSQL refuse au SaveChangesAsync, loin du site fautif.
    [Fact]
    public void Refuse_un_titre_plus_long_que_la_colonne()
    {
        var acte = () => Generer(titre: new string('a', Quest.LongueurMaximaleTitre + 1));

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Accepte_un_titre_exactement_a_la_borne()
    {
        var titre = new string('a', Quest.LongueurMaximaleTitre);

        Generer(titre: titre).Title.Should().Be(titre);
    }

    [Fact]
    public void Refuse_une_description_plus_longue_que_la_colonne()
    {
        var acte = () => Generer(description: new string('a', Quest.LongueurMaximaleDescription + 1));

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }

    // Le barème est le même que celui que l'agent applique à la réponse du modèle : une quête
    // ne peut pas exister avec une récompense hors de la fourchette de sa difficulté.
    [Fact]
    public void Refuse_une_recompense_hors_de_la_fourchette_de_sa_difficulte()
    {
        var acte = () => Generer(difficulte: QuestDifficulty.Facile, xp: 40);

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Refuse_une_quete_de_penalite_qui_ne_vaut_pas_dix_XP()
    {
        var acte = () => Generer(type: QuestType.Penalite, difficulte: QuestDifficulty.Facile, xp: 15);

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Accepte_une_quete_de_penalite_a_dix_XP()
    {
        var quete = Generer(type: QuestType.Penalite, difficulte: QuestDifficulty.Facile, xp: 10);

        quete.XpReward.Should().Be(10);
    }

    // Une quête de pénalité est facile par conception (doc mécaniques, section 1) : la
    // difficulté qu'un appelant croit bon d'annoncer ne la contredit pas.
    [Fact]
    public void Ramene_une_quete_de_penalite_a_la_difficulte_facile()
    {
        var quete = Generer(type: QuestType.Penalite, difficulte: QuestDifficulty.Difficile, xp: 10);

        quete.Difficulty.Should().Be(QuestDifficulty.Facile);
    }

    // ---------------------------------------------------------------------------------------
    // Réécriture d'une quête de repli : la seule mutation de texte que le modèle admette.
    // Trois secondes d'indisponibilité du Système à 7h00 ne peuvent pas condamner le Chasseur
    // au texte générique jusqu'à minuit.
    // ---------------------------------------------------------------------------------------

    private static Quest ReplePose() => Generer(
        titre: "Éveil du Corps",
        description: "Bouge à ton rythme aujourd'hui, Chasseur.",
        difficulte: QuestDifficulty.Facile,
        xp: 10,
        estRepli: true);

    private static void Reecrire(
        Quest quete,
        string titre = "L'Épreuve du Guerrier",
        int xp = 20,
        bool estRepli = false) =>
        quete.RegenerateFallback(
            titre,
            "Bouge à ton rythme : marche, gainage, étirements.",
            QuestType.Quotidienne,
            QuestStat.Force,
            QuestDifficulty.Moyenne,
            xp,
            estRepli);

    [Fact]
    public void Reecrit_le_titre_d_une_quete_de_repli()
    {
        var quete = ReplePose();

        Reecrire(quete);

        quete.Title.Should().Be("L'Épreuve du Guerrier");
    }

    [Fact]
    public void Ne_marque_plus_comme_repli_une_quete_reecrite_par_le_Systeme()
    {
        var quete = ReplePose();

        Reecrire(quete);

        quete.IsFallback.Should().BeFalse();
    }

    // La quête garde son identité : c'est la même ligne, pas une seconde qui heurterait l'index
    // unique du jour.
    [Fact]
    public void Conserve_l_identifiant_de_la_quete_reecrite()
    {
        var quete = ReplePose();
        var identifiant = quete.Id;

        Reecrire(quete);

        quete.Id.Should().Be(identifiant);
    }

    // Une quête réellement générée ne se réécrit pas : le texte que le Chasseur a lu le matin
    // est celui qu'il retrouve le soir.
    [Fact]
    public void Refuse_de_reecrire_une_quete_reellement_generee()
    {
        var quete = Generer();

        var acte = () => Reecrire(quete);

        acte.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Refuse_de_reecrire_un_repli_avec_une_recompense_hors_bareme()
    {
        var quete = ReplePose();

        var acte = () => Reecrire(quete, xp: 60);

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }

    // Le Système peut être encore indisponible à la seconde tentative : la quête reste alors un
    // repli, et le rafraîchissement suivant retentera.
    [Fact]
    public void Reste_un_repli_quand_le_Systeme_n_a_toujours_rien_rendu()
    {
        var quete = ReplePose();

        Reecrire(quete, estRepli: true);

        quete.IsFallback.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------
    // Complétion. La garde d'idempotence vit ici, dans l'entité : le double-tap sur le bouton et
    // le renvoi réseau du client sont deux chemins distincts vers le même appel, et aucun handler
    // ne peut promettre d'être seul.
    // ---------------------------------------------------------------------------------------

    // 23h30 le 25 à New York, soit déjà le 26 en UTC : l'instant qui distingue le jour du
    // Chasseur du jour du serveur. L'appelant convertit avant d'appeler, exactement comme pour
    // la génération de la quête du jour.
    private static readonly DateTimeOffset VingtTroisHeuresTrenteANewYork =
        new(2026, 7, 25, 23, 30, 0, TimeSpan.FromHours(-4));

    [Fact]
    public void Marque_la_quete_comme_completee()
    {
        var quete = Generer();

        quete.Complete(VingtTroisHeuresTrenteANewYork);

        quete.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void Enregistre_l_instant_absolu_de_la_completion()
    {
        var quete = Generer();

        quete.Complete(VingtTroisHeuresTrenteANewYork);

        quete.CompletedAt.Should().Be(new DateTimeOffset(2026, 7, 26, 3, 30, 0, TimeSpan.Zero));
    }

    // Stocké en UTC : Npgsql refuse d'écrire un DateTimeOffset décalé dans un timestamptz, et
    // l'instant absolu est de toute façon la seule part de cette date qui survive à la relecture.
    //
    // L'assertion porte sur le décalage et non sur l'instant : Should().Be() compare deux
    // DateTimeOffset par leur instant absolu, si bien qu'un CompletedAt resté à -04:00 y
    // passerait sans broncher. C'est la conversion elle-même qu'on éprouve ici.
    [Fact]
    public void Enregistre_l_instant_de_completion_en_UTC()
    {
        var quete = Generer();

        quete.Complete(VingtTroisHeuresTrenteANewYork);

        quete.CompletedAt!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Annonce_avoir_complete_la_quete_au_premier_appel()
    {
        Generer().Complete(VingtTroisHeuresTrenteANewYork).Should().BeTrue();
    }

    // Le retour est ce qui permet au handler de n'attribuer l'XP qu'une fois : deux appels, un
    // seul gain. Sans lui, il faudrait relire IsCompleted avant d'appeler — et deux appels
    // concurrents passeraient tous deux le test.
    [Fact]
    public void Annonce_ne_rien_avoir_complete_a_la_seconde_completion()
    {
        var quete = Generer();
        quete.Complete(VingtTroisHeuresTrenteANewYork);

        quete.Complete(VingtTroisHeuresTrenteANewYork.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void Ne_deplace_pas_l_instant_de_completion_a_la_seconde_completion()
    {
        var quete = Generer();
        quete.Complete(VingtTroisHeuresTrenteANewYork);

        quete.Complete(VingtTroisHeuresTrenteANewYork.AddHours(1));

        quete.CompletedAt.Should().Be(new DateTimeOffset(2026, 7, 26, 3, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Leve_un_evenement_de_completion()
    {
        var quete = Generer();

        quete.Complete(VingtTroisHeuresTrenteANewYork);

        quete.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<QuestCompletedEvent>();
    }

    [Fact]
    public void Rattache_l_evenement_de_completion_au_Chasseur_et_a_sa_quete()
    {
        var quete = Generer();

        quete.Complete(VingtTroisHeuresTrenteANewYork);

        quete.DomainEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                QuestId = quete.Id,
                HunterProfileId = Chasseur,
                Type = QuestType.Quotidienne,
            });
    }

    // Le piège de date de la série : à 23h30 à New York, le serveur est déjà le lendemain en UTC.
    // Compter ce jour-là pour le 26 volerait au Chasseur la journée qu'il vient de tenir.
    [Fact]
    public void Date_l_evenement_du_jour_du_Chasseur_et_non_de_celui_du_serveur()
    {
        var quete = Generer();

        quete.Complete(VingtTroisHeuresTrenteANewYork);

        quete.DomainEvents.Should().ContainSingle()
            .Which.As<QuestCompletedEvent>().JourDuChasseur.Should().Be(new DateOnly(2026, 7, 25));
    }

    // Le risque n°1 de la complétion : deux événements, c'est deux fois l'XP en aval.
    [Fact]
    public void Ne_leve_aucun_evenement_a_la_seconde_completion()
    {
        var quete = Generer();
        quete.Complete(VingtTroisHeuresTrenteANewYork);
        quete.ClearDomainEvents();

        quete.Complete(VingtTroisHeuresTrenteANewYork.AddHours(1));

        quete.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Ne_leve_aucun_evenement_de_completion_a_la_generation()
    {
        Generer().DomainEvents.Should().BeEmpty();
    }
}
