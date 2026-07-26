using Arise.Domain.Habits;
using FluentAssertions;

namespace Arise.Domain.Tests.Habits;

/// <summary>
/// L'habitude telle qu'un Chasseur la déclare : un nom, un rythme attendu, et le Chasseur
/// auquel elle appartient. Sa série vit ailleurs (doc mécaniques, section 2 : « <c>Habit</c> a
/// sa propre série calculée depuis <c>HabitLog</c> ») — l'entité ne la porte donc pas encore, et
/// rien ici ne journalise quoi que ce soit.
/// </summary>
public class HabitTests
{
    private static readonly Guid Chasseur = Guid.NewGuid();

    private static readonly DateTimeOffset Creation =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    private static Habit Creer(
        string nom = "Boire deux litres d'eau",
        HabitFrequency frequence = HabitFrequency.Quotidienne) =>
        Habit.Create(Chasseur, nom, frequence, Creation);

    [Fact]
    public void Cree_une_habitude_rattachee_au_Chasseur_vise()
    {
        Creer().HunterProfileId.Should().Be(Chasseur);
    }

    [Fact]
    public void Cree_une_habitude_dotee_d_un_identifiant()
    {
        Creer().Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Cree_une_habitude_portant_le_nom_demande()
    {
        Creer(nom: "Lire vingt minutes").Name.Should().Be("Lire vingt minutes");
    }

    [Fact]
    public void Cree_une_habitude_au_rythme_demande()
    {
        Creer(frequence: HabitFrequency.Hebdomadaire).Frequency
            .Should().Be(HabitFrequency.Hebdomadaire);
    }

    [Fact]
    public void Cree_une_habitude_datee_de_l_instant_recu()
    {
        Creer().CreatedAt.Should().Be(Creation);
    }

    // L'archivage existe dès la création pour que la liste du Chasseur n'ait pas à distinguer
    // une habitude « jamais archivée » d'une habitude archivée : elle naît simplement active.
    [Fact]
    public void Cree_une_habitude_active()
    {
        Creer().IsArchived.Should().BeFalse();
    }

    // Les espaces de bordure sont invisibles dans une liste : sans rognage, « Courir » et
    // « Courir  » deviennent deux habitudes distinctes, aux séries séparées.
    [Fact]
    public void Rogne_les_espaces_de_bordure_du_nom()
    {
        Creer(nom: "  Courir  ").Name.Should().Be("Courir");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_nom_vide(string nom)
    {
        var acte = () => Creer(nom: nom);

        acte.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Refuse_un_nom_plus_long_que_la_colonne()
    {
        var acte = () => Creer(nom: new string('a', Habit.LongueurMaximaleNom + 1));

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Accepte_un_nom_exactement_a_la_borne()
    {
        var nom = new string('a', Habit.LongueurMaximaleNom);

        Creer(nom: nom).Name.Should().Be(nom);
    }

    // La borne porte sur le nom rogné, qui est celui que la colonne stockera : la mesurer sur
    // le brut refuserait une saisie que l'entité aurait acceptée une fois rognée.
    [Fact]
    public void Accepte_un_nom_dont_seuls_les_espaces_de_bordure_depassent_la_borne()
    {
        var nom = new string('a', Habit.LongueurMaximaleNom);

        Creer(nom: $"  {nom}  ").Name.Should().Be(nom);
    }

    [Fact]
    public void Refuse_une_habitude_sans_Chasseur()
    {
        var acte = () => Habit.Create(
            Guid.Empty, "Boire deux litres d'eau", HabitFrequency.Quotidienne, Creation);

        acte.Should().Throw<ArgumentException>();
    }

    // Une habitude rangée n'est ni un échec ni une suppression : elle quitte la liste du jour et
    // garde son histoire. C'est ce qui permet au Chasseur de faire le ménage sans rien perdre.
    [Fact]
    public void Range_l_habitude_qu_on_archive()
    {
        var habitude = Creer();

        habitude.Archive();

        habitude.IsArchived.Should().BeTrue();
    }

    // Deux appareils, ou un double-tap sur le bouton : l'archivage est une bascule, pas un
    // compteur, et le second appel n'a rien à faire échouer.
    [Fact]
    public void Reste_archivee_apres_un_second_archivage()
    {
        var habitude = Creer();

        habitude.Archive();
        habitude.Archive();

        habitude.IsArchived.Should().BeTrue();
    }

    // La colonne est un timestamp with time zone, sur lequel Npgsql refuse un DateTimeOffset
    // décalé. Sans cette garde, un appelant qui passe DateTimeOffset.Now ne l'apprend qu'au
    // SaveChangesAsync, loin d'ici.
    [Fact]
    public void Refuse_un_instant_de_creation_decale()
    {
        var acte = () => Habit.Create(
            Chasseur,
            "Boire deux litres d'eau",
            HabitFrequency.Quotidienne,
            new DateTimeOffset(2026, 7, 26, 9, 0, 0, TimeSpan.FromHours(2)));

        acte.Should().Throw<ArgumentException>();
    }
}
