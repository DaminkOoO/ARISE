using Arise.Application.Common.Messaging;
using Arise.Domain.Habits;

namespace Arise.Application.Features.Habits.Commands.CreateHabit;

/// <summary>
/// Déclare une habitude que le Chasseur veut tenir.
/// </summary>
public sealed record CreateHabitCommand(
    Guid HunterProfileId, string Name, HabitFrequency Frequency)
    : ICommand<CreateHabitResult>;

/// <summary>
/// Ce que la déclaration renvoie : de quoi identifier l'habitude créée et afficher la ligne qui
/// vient d'apparaître, sans relire toute la liste.
/// </summary>
public sealed record CreateHabitResult(Guid HabitId, string Name, HabitFrequency Frequency);
