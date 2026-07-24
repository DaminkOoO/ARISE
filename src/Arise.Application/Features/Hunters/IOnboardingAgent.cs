using Arise.Application.Common.Abstractions;

namespace Arise.Application.Features.Hunters;

/// <summary>
/// L'agent qui écrit la narration de l'écran Éveil. Son seul rôle : transformer les objectifs
/// déclarés par le Chasseur en 1 à 3 phrases dans la voix du Système (doc mécaniques,
/// section 4). Il ne touche jamais aux valeurs numériques du profil créé — celles-ci sont des
/// constantes déterministes posées par <see cref="Arise.Domain.Hunters.HunterProfile.Create"/>.
/// </summary>
public interface IOnboardingAgent : IAgent<OnboardingAgentRequest, OnboardingAgentResult>
{
}

/// <summary>Objectifs déclarés par le Chasseur à l'écran « Objectifs » de l'onboarding.</summary>
public sealed record OnboardingAgentRequest(IReadOnlyList<HunterGoal> Objectifs);

/// <summary>
/// Résultat de la génération de la narration d'Éveil.
///
/// <para><see cref="EstRepli"/> distingue un texte réellement validé de Gemini d'un contenu
/// de secours neutre — la décision documentée sur <see cref="IAgent{TRequest,TResult}"/> :
/// chaque DTO de résultat concret porte son propre champ <c>EstRepli</c> plutôt qu'un wrapper
/// générique.</para>
/// </summary>
public sealed record OnboardingAgentResult(string AwakeningNarrative, bool EstRepli);
