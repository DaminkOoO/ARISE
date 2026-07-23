using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Arise.Api;
using Arise.Application;
using Arise.Application.Features.Auth.Commands.Login;
using Arise.Application.Features.Auth.Commands.RegisterUser;
using Arise.Infrastructure;
using Arise.Infrastructure.Auth;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();

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

app.Run();

// Exposé pour WebApplicationFactory<Program> : les tests d'intégration bootent le vrai hôte.
public partial class Program;

// Identité du Chasseur authentifié, telle que /auth/moi la renvoie.
internal sealed record ReponseMoi(Guid UserId, string Username);
