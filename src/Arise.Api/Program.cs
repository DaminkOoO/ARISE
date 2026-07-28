using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Arise.Api;
using Arise.Application;
using Arise.Application.Common.Abstractions;
using Arise.Application.Features.Auth.Commands.Login;
using Arise.Application.Features.Auth.Commands.RegisterUser;
using Arise.Application.Features.Habits.Commands.CreateHabit;
using Arise.Application.Features.Habits.Commands.LogHabit;
using Arise.Application.Features.Habits.Commands.SuggestHabits;
using Arise.Application.Features.Habits.Queries.GetHabits;
using Arise.Application.Features.Hunters;
using Arise.Application.Features.Hunters.Commands.OnboardHunter;
using Arise.Application.Features.Tasks.Commands.CompleteTask;
using Arise.Application.Features.Tasks.Commands.CreateTask;
using Arise.Application.Features.Tasks.Queries.GetTasks;
using Arise.Domain.Habits;
using Arise.Infrastructure;
using Arise.Infrastructure.Agents;
using Arise.Infrastructure.Auth;
using Arise.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();

// Les énumérations voyagent en texte, dans les deux sens : « Quotidienne » plutôt que 0.
// Sans ce convertisseur, le contrat dépendrait de l'ordre de déclaration des membres — ajouter
// un rythme d'habitude au milieu de l'énumération changerait silencieusement le sens des corps
// déjà envoyés par les clients installés.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Les exceptions métier deviennent des ProblemDetails français au bon code HTTP.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AuthExceptionHandler>();

// La clé est celle qu'injecte docker-compose (ConnectionStrings__Postgres) ; absente, mieux
// vaut échouer au démarrage avec un message clair que plus tard sur la première requête.
var chaineDeConnexion = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "La chaîne de connexion 'Postgres' est absente de la configuration.");

builder.Services.AddInfrastructure(chaineDeConnexion);

// La section Jwt alimente à la fois l'émetteur (Infrastructure) et le middleware de
// validation ci-dessous : une seule source pour la clé, l'émetteur et l'audience.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));

// Même modèle que Jwt : la clé Gemini vient de l'environnement (ou d'un secret utilisateur),
// jamais d'un appsettings.json versionné — la section n'y figure donc pas.
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.Section));

// Le middleware de validation lit la même section que l'émetteur : clé, émetteur et audience
// ne peuvent donc pas diverger entre celui qui signe et celui qui vérifie.
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()
    ?? throw new InvalidOperationException("La section 'Jwt' est absente de la configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // On garde les noms de claims du jeton (« sub », « unique_name ») tels quels au lieu de
        // les laisser remapper vers les URI ClaimTypes : l'endpoint protégé les relit sous le
        // nom exact que JwtTokenGenerator a écrit.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            // Aucune tolérance : un jeton expiré l'est à la seconde près, comme ExpiresAt le promet.
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Le schéma avant la première requête : sans cela, la pile docker compose démarre sur un volume
// vierge et /auth/register échoue en « 42P01: relation "users" does not exist ». Les raisons du
// choix (au démarrage, sans condition d'environnement, sans reprise) sont détaillées sur
// MigrationsDeDemarrage, côté Infrastructure.
await app.Services.AppliquerLesMigrationsEnAttenteAsync();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Inscription d'un Chasseur. Le corps se lie sur la commande ; le handler hache le mot de
// passe et pose la ligne. 201 : une ressource de compte vient d'être créée.
app.MapPost("/auth/register", async (RegisterUserCommand commande, ISender mediateur) =>
{
    var resultat = await mediateur.Send(commande);
    return Results.Created((string?)null, resultat);
});

// Connexion : rend le jeton et sa péremption. Un échec (nom inconnu ou mot de passe faux)
// lève InvalidCredentialsException, traduite en 401 sans dire lequel des deux est en cause.
app.MapPost("/auth/login", async (LoginCommand commande, ISender mediateur) =>
{
    var resultat = await mediateur.Send(commande);
    return Results.Ok(resultat);
});

// Endpoint protégé : sans jeton valide, le middleware d'authentification répond 401 avant même
// d'entrer ici. Avec un jeton valide, on relit l'identité du Chasseur telle qu'elle est inscrite
// dans les claims — pas de nouvel accès à la base pour ce que le jeton porte déjà.
app.MapGet("/auth/moi", (ClaimsPrincipal chasseur) =>
{
    var id = Guid.Parse(chasseur.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    var nom = chasseur.FindFirstValue(JwtRegisteredClaimNames.UniqueName)!;
    return Results.Ok(new ReponseMoi(id, nom));
})
.RequireAuthorization();

// --- Éveil -------------------------------------------------------------------------------

// Crée le profil de progression du compte authentifié et le lui rattache. Le compte vient du
// jeton, jamais du corps : c'est cette liaison qui rend sûrs tous les endpoints ci-dessous.
app.MapPost("/hunters/eveil", async (
    CorpsEveil corps,
    ClaimsPrincipal chasseur,
    ISender mediateur,
    CancellationToken jeton) =>
{
    var idDuCompte = Guid.Parse(chasseur.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    var resultat = await mediateur.Send(
        new OnboardHunterCommand(idDuCompte, corps.Objectifs), jeton);

    return Results.Created((string?)null, resultat);
})
.RequireAuthorization();

// --- Habitudes ---------------------------------------------------------------------------

app.MapGet("/habitudes", async (
    ClaimsPrincipal chasseur, IUserRepository comptes, ISender mediateur, CancellationToken jeton) =>
{
    var profil = await ProfilDuChasseurAuthentifie.ResoudreAsync(chasseur, comptes, jeton);

    return Results.Ok(await mediateur.Send(new GetHabitsQuery(profil), jeton));
})
.RequireAuthorization();

app.MapPost("/habitudes", async (
    CorpsCreationHabitude corps,
    ClaimsPrincipal chasseur,
    IUserRepository comptes,
    ISender mediateur,
    CancellationToken jeton) =>
{
    var profil = await ProfilDuChasseurAuthentifie.ResoudreAsync(chasseur, comptes, jeton);

    var resultat = await mediateur.Send(
        new CreateHabitCommand(profil, corps.Name, corps.Frequency), jeton);

    return Results.Created((string?)null, resultat);
})
.RequireAuthorization();

// Journalise l'habitude pour la journée du Chasseur. Le fuseau vient du client, seul à le
// connaître tant que HunterProfile ne le porte pas.
app.MapPost("/habitudes/{habitId:guid}/journal", async (
    Guid habitId,
    CorpsJournalisation corps,
    ClaimsPrincipal chasseur,
    IUserRepository comptes,
    ISender mediateur,
    CancellationToken jeton) =>
{
    var profil = await ProfilDuChasseurAuthentifie.ResoudreAsync(chasseur, comptes, jeton);

    return Results.Ok(await mediateur.Send(
        new LogHabitCommand(profil, habitId, corps.FuseauHoraire), jeton));
})
.RequireAuthorization();

app.MapPost("/habitudes/suggestions", async (
    ClaimsPrincipal chasseur, IUserRepository comptes, ISender mediateur, CancellationToken jeton) =>
{
    var profil = await ProfilDuChasseurAuthentifie.ResoudreAsync(chasseur, comptes, jeton);

    return Results.Ok(await mediateur.Send(new SuggestHabitsCommand(profil), jeton));
})
.RequireAuthorization();

// --- Tâches ------------------------------------------------------------------------------

app.MapGet("/taches", async (
    ClaimsPrincipal chasseur, IUserRepository comptes, ISender mediateur, CancellationToken jeton) =>
{
    var profil = await ProfilDuChasseurAuthentifie.ResoudreAsync(chasseur, comptes, jeton);

    return Results.Ok(await mediateur.Send(new GetTasksQuery(profil), jeton));
})
.RequireAuthorization();

app.MapPost("/taches", async (
    CorpsCreationTache corps,
    ClaimsPrincipal chasseur,
    IUserRepository comptes,
    ISender mediateur,
    CancellationToken jeton) =>
{
    var profil = await ProfilDuChasseurAuthentifie.ResoudreAsync(chasseur, comptes, jeton);

    var resultat = await mediateur.Send(
        new CreateTaskCommand(profil, corps.Title, corps.DueDate), jeton);

    return Results.Created((string?)null, resultat);
})
.RequireAuthorization();

app.MapPost("/taches/{taskId:guid}/completion", async (
    Guid taskId,
    CorpsCompletionTache corps,
    ClaimsPrincipal chasseur,
    IUserRepository comptes,
    ISender mediateur,
    CancellationToken jeton) =>
{
    var profil = await ProfilDuChasseurAuthentifie.ResoudreAsync(chasseur, comptes, jeton);

    return Results.Ok(await mediateur.Send(
        new CompleteTaskCommand(profil, taskId, corps.FuseauHoraire), jeton));
})
.RequireAuthorization();

app.Run();

// Exposé pour WebApplicationFactory<Program> : les tests d'intégration bootent le vrai hôte.
public partial class Program;

// Identité du Chasseur authentifié, telle que /auth/moi la renvoie.
internal sealed record ReponseMoi(Guid UserId, string Username);

// Les corps de requête ne portent jamais d'identifiant de profil : celui-ci se déduit du jeton
// (voir ProfilDuChasseurAuthentifie). Un champ de plus dans ces enregistrements serait une porte
// ouverte sur les données d'un autre Chasseur.
internal sealed record CorpsEveil(IReadOnlyList<HunterGoal> Objectifs);

internal sealed record CorpsCreationHabitude(string Name, HabitFrequency Frequency);

internal sealed record CorpsJournalisation(string FuseauHoraire);

internal sealed record CorpsCreationTache(string Title, DateOnly? DueDate);

internal sealed record CorpsCompletionTache(string FuseauHoraire);
