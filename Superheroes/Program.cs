using Superheroes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<ICharactersProvider, CharactersProvider>();
builder.Services.AddScoped<IBattleService, BattleService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapControllers();

app.Run();

public partial class Program { }
