using Arise.Application.Common.Abstractions;
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
    IHunterProfileRepository hunterProfiles)
    : IRequestHandler<OnboardHunterCommand, OnboardHunterResult>
{
    public async Task<OnboardHunterResult> Handle(
        OnboardHunterCommand request, CancellationToken cancellationToken)
    {
        var narration = await onboardingAgent.ExecuteAsync(
            new OnboardingAgentRequest(request.Objectifs), cancellationToken);

        // Niveau, rang et XP de départ sont des constantes déterministes : elles ne dépendent
        // ni des objectifs déclarés, ni de ce que l'agent a rendu (validé ou en repli).
        var profile = HunterProfile.Create();

        await hunterProfiles.SaveAsync(profile, cancellationToken);

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
