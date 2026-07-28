using FluentValidation;

namespace Arise.Application.Features.Tasks.Queries.GetTasks;

public sealed class GetTasksQueryValidator : AbstractValidator<GetTasksQuery>
{
    public GetTasksQueryValidator()
    {
        RuleFor(requete => requete.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");
    }
}
