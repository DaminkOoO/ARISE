using Arise.Application.Features.Tasks.Commands.CreateTask;
using Arise.Domain.Tasks;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Tasks.Commands;

/// <summary>
/// Contrôles de forme de la déclaration d'une tâche. Aucun contrôle sur l'échéance : une date
/// passée est une saisie légitime — le Chasseur qui note « Rappeler la banque » un mardi pour une
/// échéance de vendredi dernier sait très bien qu'il est en retard, et le lui refuser
/// l'empêcherait d'écrire ce qu'il a à faire.
/// </summary>
public class CreateTaskCommandValidatorTests
{
    private static readonly Guid ProfilValide = Guid.NewGuid();

    private static readonly CreateTaskCommandValidator Validator = new();

    private static ValidationResult Valide(
        Guid? hunterProfileId = null,
        string titre = "Appeler le dentiste",
        DateOnly? echeance = null) =>
        Validator.Validate(new CreateTaskCommand(
            hunterProfileId ?? ProfilValide, titre, echeance));

    private static string PremiereErreurSur(ValidationResult resultat, string propriete) =>
        resultat.Errors.Should().Contain(erreur => erreur.PropertyName == propriete)
            .Which.ErrorMessage;

    [Fact]
    public void Accepte_une_tache_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Accepte_une_tache_sans_echeance()
    {
        Valide(echeance: null).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Accepte_une_echeance_passee()
    {
        Valide(echeance: new DateOnly(2020, 1, 1)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_identifiant_de_profil_vide()
    {
        PremiereErreurSur(Valide(hunterProfileId: Guid.Empty), nameof(CreateTaskCommand.HunterProfileId))
            .Should().Be("Le profil de Chasseur ciblé est obligatoire.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_titre_vide(string titre)
    {
        PremiereErreurSur(Valide(titre: titre), nameof(CreateTaskCommand.Title))
            .Should().Be("Le titre de la tâche est obligatoire.");
    }

    [Fact]
    public void Refuse_un_titre_plus_long_que_la_colonne()
    {
        var resultat = Valide(titre: new string('a', TaskItem.LongueurMaximaleTitre + 1));

        PremiereErreurSur(resultat, nameof(CreateTaskCommand.Title))
            .Should().Be(
                $"Le titre de la tâche ne peut pas dépasser {TaskItem.LongueurMaximaleTitre} caractères.");
    }

    // Les contrôles portent sur le titre rogné, car c'est celui que le handler enregistrera :
    // mesurer le brut refuserait une saisie que l'entité aurait acceptée une fois rognée.
    [Fact]
    public void Accepte_un_titre_dont_seuls_les_espaces_de_bordure_depassent_la_borne()
    {
        var titre = new string('a', TaskItem.LongueurMaximaleTitre);

        Valide(titre: $"  {titre}  ").IsValid.Should().BeTrue();
    }
}
