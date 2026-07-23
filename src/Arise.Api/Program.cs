using Arise.Application;
using Arise.Application.Features.Auth.Commands.RegisterUser;
using Arise.Infrastructure;
using Arise.Infrastructure.Auth;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();

// La clé est celle qu'injecte docker-compose (ConnectionStrings__Postgres) ; absente, mieux
// vaut échouer au démarrage avec un message clair que plus tard sur la première requête.
var chaineDeConnexion = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "La chaîne de connexion 'Postgres' est absente de la configuration.");

builder.Services.AddInfrastructure(chaineDeConnexion);

// La section Jwt alimente à la fois l'émetteur (Infrastructure) et le middleware de
// validation ci-dessous : une seule source pour la clé, l'émetteur et l'audience.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Inscription d'un Chasseur. Le corps se lie sur la commande ; le handler hache le mot de
// passe et pose la ligne. 201 : une ressource de compte vient d'être créée.
app.MapPost("/auth/register", async (RegisterUserCommand commande, ISender mediateur) =>
{
    var resultat = await mediateur.Send(commande);
    return Results.Created((string?)null, resultat);
});

app.Run();

// Exposé pour WebApplicationFactory<Program> : les tests d'intégration bootent le vrai hôte.
public partial class Program;
