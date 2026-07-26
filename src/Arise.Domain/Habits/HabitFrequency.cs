namespace Arise.Domain.Habits;

/// <summary>
/// Rythme auquel le Chasseur s'attend à tenir une habitude.
///
/// <para>C'est ce rythme qui donnera son unité à la série de l'habitude — des jours consécutifs
/// pour une quotidienne, des semaines pour une hebdomadaire. La série elle-même se calculera
/// depuis <c>HabitLog</c> (doc mécaniques, section 2) et reste locale à chaque habitude : elle
/// ne se confond pas avec la série d'engagement du profil Chasseur.</para>
/// </summary>
public enum HabitFrequency
{
    Quotidienne,
    Hebdomadaire,
}
