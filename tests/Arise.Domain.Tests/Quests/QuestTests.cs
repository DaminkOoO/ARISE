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
}
