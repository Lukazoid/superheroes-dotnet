using Superheroes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.Configure<CharactersCacheOptions>(builder.Configuration.GetSection("Characters"));

builder.Services.AddKeyedSingleton<ICharactersProvider, CharactersProvider>(CachingCharactersProvider.SourceProviderKey);
builder.Services.AddSingleton<ICharactersProvider, CachingCharactersProvider>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapControllers();

app.Run();

public partial class Program { }
