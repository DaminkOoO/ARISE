using Arise.Application.Features.Habits.Commands.CreateHabit;
using Arise.Domain.Habits;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Habits.Commands;

/// <summary>
/// Contrôles de forme de la déclaration d'une habitude. Les messages sont écrits explicitement :
/// ils remontent jusqu'à l'écran (règle n°7), en français et au tutoiement, et disent quoi
/// corriger plutôt que de constater un refus.
/// </summary>
public class CreateHabitCommandValidatorTests
{
    private static readonly Guid ProfilValide = Guid.NewGuid();

    private static readonly CreateHabitCommandValidator Validator = new();

    private static ValidationResult Valide(
        Guid? hunterProfileId = null,
        string nom = "Boire deux litres d'eau",
        HabitFrequency frequence = HabitFrequency.Quotidienne) =>
        Validator.Validate(
            new CreateHabitCommand(hunterProfileId ?? ProfilValide, nom, frequence));

    private static string PremiereErreurSur(ValidationResult resultat, string propriete) =>
        resultat.Errors.Should().Contain(erreur => erreur.PropertyName == propriete)
            .Which.ErrorMessage;

    [Fact]
    public void Accepte_une_declaration_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Accepte_un_rythme_hebdomadaire()
    {
        Valide(frequence: HabitFrequency.Hebdomadaire).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_identifiant_de_profil_vide()
    {
        var resultat = Valide(hunterProfileId: Guid.Empty);

        PremiereErreurSur(resultat, nameof(CreateHabitCommand.HunterProfileId))
            .Should().Be("Le profil de Chasseur ciblé est obligatoire.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_nom_absent(string nom)
    {
        var resultat = Valide(nom: nom);

        PremiereErreurSur(resultat, nameof(CreateHabitCommand.Name))
            .Should().Be("Le nom de l'habitude est obligatoire.");
    }

    [Fact]
    public void Refuse_un_nom_plus_long_que_la_borne()
    {
        var resultat = Valide(nom: new string('a', Habit.LongueurMaximaleNom + 1));

        PremiereErreurSur(resultat, nameof(CreateHabitCommand.Name))
            .Should().Be(
                $"Le nom de l'habitude ne peut pas dépasser {Habit.LongueurMaximaleNom} caractères.");
    }

    // Le contrôle porte sur le nom rogné, car c'est celui que le handler enregistrera : mesurer
    // le brut refuserait une saisie que l'entité aurait acceptée une fois rognée.
    [Fact]
    public void Accepte_un_nom_dont_seuls_les_espaces_de_bordure_depassent_la_borne()
    {
        var nom = new string('a', Habit.LongueurMaximaleNom);

        Valide(nom: $"  {nom}  ").IsValid.Should().BeTrue();
    }

    // Le rythme arrive du client en JSON : un entier hors énumération se lie sans broncher et
    // finirait en base sous forme d'un texte que rien ne sait relire.
    [Fact]
    public void Refuse_un_rythme_inconnu()
    {
        var resultat = Valide(frequence: (HabitFrequency)42);

        PremiereErreurSur(resultat, nameof(CreateHabitCommand.Frequency))
            .Should().Be("Ce rythme d'habitude est inconnu.");
    }
}
