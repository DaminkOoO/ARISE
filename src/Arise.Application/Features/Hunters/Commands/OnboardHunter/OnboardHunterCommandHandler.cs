using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Hunters;
using Arise.Domain.Hunters;
using MediatR;

namespace Arise.Application.Features.Hunters.Commands.OnboardHunter;

/// <summary>
/// Éveille un Chasseur : crée son profil de progression au niveau de départ — des constantes
/// déterministes portées par <see cref="HunterProfile.Create"/>, jamais par
/// <see cref="IOnboardingAgent"/> — et l'accompagne de la narration personnalisée que l'agent a
/// générée à partir des objectifs déclarés.
/// </summary>
public sealed class OnboardHunterCommandHandler(
    IOnboardingAgent onboardingAgent,
    IHunterProfileRepository hunterProfiles,
    IUserRepository users)
    : IRequestHandler<OnboardHunterCommand, OnboardHunterResult>
{
    public async Task<OnboardHunterResult> Handle(
        OnboardHunterCommand request, CancellationToken cancellationToken)
    {
        // Avant l'appel à l'agent, qui coûte du temps et de l'argent : un compte inconnu n'a pas
        // à faire travailler le Système.
        var compte = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new UserNotFoundException();

        // Un second éveil écraserait toute la progression du Chasseur : son ancien profil
        // deviendrait inatteignable, XP et séries compris. L'entité le refuse, mais on l'arrête
        // ici pour ne pas payer l'agent avant de découvrir le refus.
        if (compte.HunterProfileId is not null)
        {
            throw new HunterAlreadyAwakenedException();
        }

        var narration = await onboardingAgent.ExecuteAsync(
            new OnboardingAgentRequest(request.Objectifs), cancellationToken);

        // Niveau, rang et XP de départ sont des constantes déterministes : elles ne dépendent
        // ni des objectifs déclarés, ni de ce que l'agent a rendu (validé ou en repli).
        var profile = HunterProfile.Create();

        await hunterProfiles.SaveAsync(profile, cancellationToken);

        // Le profil d'abord, le rattachement ensuite : dans l'autre sens, une panne entre les
        // deux laisserait le compte pointer vers un profil qui n'existe pas. Ici, le pire cas est
        // un profil orphelin, que le Chasseur ne verra jamais et qui ne ment sur rien. Les deux
        // écritures partagent de toute façon la transaction ouverte par TransactionBehavior.
        compte.RattacherLeProfil(profile.Id);

        await users.SaveAsync(compte, cancellationToken);

        return new OnboardHunterResult(
            profile.Id,
            profile.Level,
            profile.Rank,
            profile.CurrentXp,
            profile.XpToNextLevel,
            narration.AwakeningNarrative,
            narration.EstRepli);
    }
}
