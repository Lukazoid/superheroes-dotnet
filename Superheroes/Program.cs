using Superheroes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHybridCache();
builder.Services.Configure<CharactersCacheOptions>(builder.Configuration.GetSection("Characters"));

builder.Services.AddSingleton<ICharactersProvider, CharactersProvider>();
builder.Services.Decorate<ICharactersProvider, CachingCharactersProvider>();
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
