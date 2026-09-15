using Microsoft.Extensions.Caching.Memory;
using Superheroes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CharactersProvider>();

var cacheDuration =
    builder.Configuration.GetValue<TimeSpan?>("Characters:CacheDuration")
    ?? TimeSpan.FromMinutes(5);

builder.Services.AddSingleton<ICharactersProvider>(sp =>
    new CachingCharactersProvider(
        sp.GetRequiredService<CharactersProvider>(),
        sp.GetRequiredService<IMemoryCache>(),
        cacheDuration,
        sp.GetRequiredService<ILogger<CachingCharactersProvider>>()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapControllers();

app.Run();

public partial class Program { }
