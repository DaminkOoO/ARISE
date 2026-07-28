using Arise.Application.Features.Tasks.Queries.GetTasks;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Tasks.Queries;

public class GetTasksQueryValidatorTests
{
    private static readonly GetTasksQueryValidator Validator = new();

    private static ValidationResult Valide(Guid? hunterProfileId = null) =>
        Validator.Validate(new GetTasksQuery(hunterProfileId ?? Guid.NewGuid()));

    [Fact]
    public void Accepte_une_lecture_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_identifiant_de_profil_vide()
    {
        var resultat = Valide(hunterProfileId: Guid.Empty);

        resultat.Errors.Should()
            .Contain(erreur => erreur.PropertyName == nameof(GetTasksQuery.HunterProfileId))
            .Which.ErrorMessage.Should().Be("Le profil de Chasseur ciblé est obligatoire.");
    }
}
