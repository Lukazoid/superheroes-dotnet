using Superheroes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHybridCache();
builder.Services.Configure<CharactersCacheOptions>(builder.Configuration.GetSection("Characters"));

builder.Services.AddSingleton<ICharactersProvider, CharactersProvider>();
builder.Services.Decorate<ICharactersProvider, CachingCharactersProvider>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapControllers();

app.Run();

public partial class Program { }
