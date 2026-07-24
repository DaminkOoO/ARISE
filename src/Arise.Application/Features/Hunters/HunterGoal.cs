namespace Arise.Application.Features.Hunters;

/// <summary>
/// Domaine que le Chasseur déclare vouloir suivre à l'écran « Objectifs » de l'onboarding
/// (doc mécaniques, section 4 : Sport / Budget / Habitudes / Calendrier / Tout). Alimente la
/// narration personnalisée de l'écran Éveil — jamais les stats ou le niveau de départ, qui
/// restent des constantes déterministes sur <see cref="Arise.Domain.Hunters.HunterProfile"/>.
/// </summary>
public enum HunterGoal
{
    Sport,
    Budget,
    Habitudes,
    Calendrier,
    Tout,
}
