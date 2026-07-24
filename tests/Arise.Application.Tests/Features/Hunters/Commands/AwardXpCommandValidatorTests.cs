using Arise.Application.Features.Hunters.Commands.AwardXp;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Hunters.Commands;

public class AwardXpCommandValidatorTests
{
    private static readonly Guid ProfilValide = Guid.NewGuid();

    private static readonly AwardXpCommandValidator Validator = new();

    private static ValidationResult Valide(Guid? hunterProfileId = null, int montant = 100) =>
        Validator.Validate(new AwardXpCommand(hunterProfileId ?? ProfilValide, montant));

    private static string PremiereErreurSur(ValidationResult resultat, string propriete) =>
        resultat.Errors.Should().Contain(erreur => erreur.PropertyName == propriete)
            .Which.ErrorMessage;

    [Fact]
    public void Accepte_une_attribution_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_montant_nul()
    {
        var resultat = Valide(montant: 0);

        PremiereErreurSur(resultat, nameof(AwardXpCommand.Montant))
            .Should().Be("Le montant d'XP doit être strictement positif.");
    }

    [Fact]
    public void Refuse_un_montant_negatif()
    {
        var resultat = Valide(montant: -10);

        PremiereErreurSur(resultat, nameof(AwardXpCommand.Montant))
            .Should().Be("Le montant d'XP doit être strictement positif.");
    }

    [Fact]
    public void Refuse_un_identifiant_de_profil_vide()
    {
        var resultat = Valide(hunterProfileId: Guid.Empty);

        PremiereErreurSur(resultat, nameof(AwardXpCommand.HunterProfileId))
            .Should().Be("Le profil de Chasseur ciblé est obligatoire.");
    }
}
