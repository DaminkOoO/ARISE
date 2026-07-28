using Arise.Domain.Tasks;
using FluentAssertions;

namespace Arise.Domain.Tests.Tasks;

/// <summary>
/// Une tâche ponctuelle : quelque chose à faire une fois, par opposition à l'habitude qui est une
/// intention qui revient. Elle porte donc une <b>échéance facultative</b> et une complétion
/// définitive, là où l'habitude porte un rythme et un journal.
///
/// <para>Comme <c>Habit</c>, elle n'accorde aucun XP et ne lève aucun événement de domaine : la
/// série d'engagement du profil ne se nourrit que de quêtes (doc mécaniques, section 2).</para>
/// </summary>
public class TaskItemTests
{
    private static readonly Guid Chasseur = Guid.NewGuid();

    private static readonly DateTimeOffset Creation =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Tap =
        new(2026, 7, 27, 18, 15, 0, TimeSpan.Zero);

    private static TaskItem Creer(
        string titre = "Appeler le dentiste",
        DateOnly? echeance = null,
        Guid? chasseur = null) =>
        TaskItem.Create(chasseur ?? Chasseur, titre, echeance, Creation);

    [Fact]
    public void Cree_une_tache_rattachee_au_Chasseur_vise()
    {
        Creer().HunterProfileId.Should().Be(Chasseur);
    }

    [Fact]
    public void Cree_une_tache_dotee_d_un_identifiant()
    {
        Creer().Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Cree_une_tache_portant_le_titre_demande()
    {
        Creer(titre: "Envoyer les documents").Title.Should().Be("Envoyer les documents");
    }

    [Fact]
    public void Cree_une_tache_datee_de_l_instant_recu()
    {
        Creer().CreatedAt.Should().Be(Creation);
    }

    // L'échéance est facultative : « ranger le garage » n'a pas de date, et lui en inventer une
    // afficherait un retard que le Chasseur ne s'est jamais donné.
    [Fact]
    public void Cree_une_tache_sans_echeance_quand_aucune_n_est_donnee()
    {
        Creer().DueDate.Should().BeNull();
    }

    [Fact]
    public void Cree_une_tache_portant_l_echeance_demandee()
    {
        Creer(echeance: new DateOnly(2026, 8, 1)).DueDate.Should().Be(new DateOnly(2026, 8, 1));
    }

    [Fact]
    public void Cree_une_tache_non_faite()
    {
        Creer().IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void Cree_une_tache_sans_instant_de_completion()
    {
        Creer().CompletedAt.Should().BeNull();
    }

    // Même rognage que sur Habit : les espaces de bordure sont invisibles dans une liste mais
    // comptent dans la colonne.
    [Fact]
    public void Rogne_les_espaces_de_bordure_du_titre()
    {
        Creer(titre: "  Payer le loyer  ").Title.Should().Be("Payer le loyer");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_titre_vide(string titre)
    {
        var acte = () => Creer(titre: titre);

        acte.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Refuse_un_titre_plus_long_que_la_colonne()
    {
        var acte = () => Creer(titre: new string('a', TaskItem.LongueurMaximaleTitre + 1));

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Accepte_un_titre_exactement_a_la_borne()
    {
        var titre = new string('a', TaskItem.LongueurMaximaleTitre);

        Creer(titre: titre).Title.Should().Be(titre);
    }

    [Fact]
    public void Refuse_une_tache_sans_Chasseur()
    {
        var acte = () => Creer(chasseur: Guid.Empty);

        acte.Should().Throw<ArgumentException>();
    }

    // Même garde que sur Habit et HabitLog : Npgsql refuse un DateTimeOffset décalé dans un
    // timestamptz, et sans elle l'appelant ne l'apprend qu'au SaveChangesAsync.
    [Fact]
    public void Refuse_un_instant_de_creation_decale()
    {
        var acte = () => TaskItem.Create(
            Chasseur,
            "Appeler le dentiste",
            null,
            new DateTimeOffset(2026, 7, 26, 9, 0, 0, TimeSpan.FromHours(2)));

        acte.Should().Throw<ArgumentException>();
    }

    // --- Complétion ---------------------------------------------------------------------------

    [Fact]
    public void Marque_la_tache_faite()
    {
        var tache = Creer();

        tache.Complete(Tap);

        tache.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void Retient_l_instant_de_completion()
    {
        var tache = Creer();

        tache.Complete(Tap);

        tache.CompletedAt.Should().Be(Tap);
    }

    [Fact]
    public void Annonce_avoir_complete_la_tache_au_premier_appel()
    {
        Creer().Complete(Tap).Should().BeTrue();
    }

    // Même invariant que Quest.Complete : double-tap, renvoi réseau, deux appareils mènent au
    // même appel, et aucun handler ne peut promettre d'être seul. La garde vit donc dans
    // l'entité, pas dans un « if » recopié par chaque handler de complétion.
    [Fact]
    public void N_annonce_pas_une_seconde_completion()
    {
        var tache = Creer();

        tache.Complete(Tap);

        tache.Complete(Tap.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void Garde_l_instant_de_la_premiere_completion()
    {
        var tache = Creer();

        tache.Complete(Tap);
        tache.Complete(Tap.AddHours(1));

        tache.CompletedAt.Should().Be(Tap);
    }

    [Fact]
    public void Refuse_un_instant_de_completion_decale()
    {
        var tache = Creer();

        var acte = () => tache.Complete(
            new DateTimeOffset(2026, 7, 27, 18, 15, 0, TimeSpan.FromHours(2)));

        acte.Should().Throw<ArgumentException>();
    }
}
