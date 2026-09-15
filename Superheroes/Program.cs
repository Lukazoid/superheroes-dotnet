using Superheroes.Adapters.S3;
using Superheroes.Application.Battles;
using Superheroes.Application.Caching;
using Superheroes.Application.Ports;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHybridCache();
builder.Services.Configure<CharactersCacheOptions>(builder.Configuration.GetSection("Characters"));

builder.Services.AddSingleton<ICharacterLoader, S3CharacterLoader>();
builder.Services.Decorate<ICharacterLoader, CachingCharacterLoader>();
builder.Services.AddScoped<IBattleService, BattleService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

public partial class Program { }
