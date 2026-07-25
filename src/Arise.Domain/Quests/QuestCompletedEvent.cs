using Arise.Domain.Common;

namespace Arise.Domain.Quests;

/// <summary>
/// Fait qu'une quête vient d'être complétée. Levé une seule fois par quête, quoi qu'il arrive :
/// une seconde complétion — double-tap sur le bouton, renvoi réseau du client — ne lève rien.
/// </summary>
/// <param name="JourDuChasseur">
/// Le jour de calendrier <b>tel que le Chasseur le vit</b>, celui que la série compte
/// (doc mécaniques, section 2) : le jour auquel la quête a été posée, et non celui du tap. À
/// 23h30 à New York le serveur est déjà le lendemain en UTC, et une séance du 25 se déclare
/// parfois à 00h05 le 26 — dans les deux cas, dater la série autrement volerait au Chasseur la
/// journée qu'il vient de tenir.
/// </param>
public sealed record QuestCompletedEvent(
    Guid QuestId,
    Guid HunterProfileId,
    QuestDomain Domain,
    QuestType Type,
    DateOnly JourDuChasseur) : IDomainEvent;
