namespace Arise.Domain.Quests;

/// <summary>
/// Domaine de vie dont relève une quête — les quatre du produit, comme les objectifs déclarés
/// à l'onboarding.
///
/// <para>Ce n'est pas un champ décoratif : c'est lui qui borne l'unicité « une seule
/// génération par jour ». Sans lui, la quête de Sport du jour empêcherait celle d'Habitudes
/// d'exister le même jour, et le constater à la Phase 2 coûterait une migration de plus.</para>
/// </summary>
public enum QuestDomain
{
    Sport,
    Budget,
    Habitudes,
    Calendrier,
}
