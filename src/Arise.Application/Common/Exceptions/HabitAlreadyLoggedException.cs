namespace Arise.Application.Common.Exceptions;

/// <summary>
/// Ce jour était déjà journalisé pour cette habitude.
///
/// <para>Signal <b>interne</b> : levée par le repository quand la contrainte d'unicité tranche
/// une course entre deux taps simultanés, elle est rattrapée par
/// <c>LogHabitCommandHandler</c> et ne remonte jamais au Chasseur — pour qui l'habitude est
/// simplement tenue. Le message reste néanmoins en français, aucune exception de cette couche
/// n'ayant vocation à s'afficher en anglais si un chemin l'oubliait.</para>
/// </summary>
public sealed class HabitAlreadyLoggedException()
    : Exception("Cette habitude est déjà validée pour aujourd'hui.");
