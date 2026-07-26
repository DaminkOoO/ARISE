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
}
