namespace Arise.Domain.Hunters;

/// <summary>
/// Rang du Chasseur, dérivé de son niveau (voir <see cref="HunterProfile.RankFor"/>). L'ordre
/// des membres suit l'ordre de progression : ne pas réordonner sans vérifier tout code qui
/// compare des rangs par leur valeur numérique sous-jacente.
/// </summary>
public enum HunterRank
{
    E,
    D,
    C,
    B,
    A,
    S,
}
