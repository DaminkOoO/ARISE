using Arise.Application.Features.Hunters;
using Arise.Application.Features.Hunters.Commands.OnboardHunter;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Hunters.Commands;

public class OnboardHunterCommandValidatorTests
{
    private static readonly OnboardHunterCommandValidator Validator = new();

    private static ValidationResult Valide(params HunterGoal[] objectifs) =>
        Validator.Validate(new OnboardHunterCommand(objectifs));

    [Fact]
    public void Accepte_un_objectif_declare()
    {
        Valide(HunterGoal.Sport).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Accepte_plusieurs_objectifs_declares()
    {
        Valide(HunterGoal.Sport, HunterGoal.Budget).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_une_liste_d_objectifs_vide()
    {
        var resultat = Valide();

        resultat.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Explique_le_refus_en_francais()
    {
        var resultat = Valide();

        resultat.Errors.Should().Contain(
            erreur => erreur.PropertyName == nameof(OnboardHunterCommand.Objectifs))
            .Which.ErrorMessage.Should().Be("Choisis au moins un objectif pour commencer ton Éveil.");
    }
}
