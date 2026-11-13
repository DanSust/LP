using System.Text.Json.Serialization;
using LP.Entity;
using LP.Journal.Services.Journal;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Сервис запускается отдельно от LP.Server.
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("https://0.0.0.0:7020");
}
else
{
    builder.WebHost.UseUrls("http://0.0.0.0:7020");
}

builder.Services.AddDbContext<ApplicationContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("LP.Entity");
            sqlOptions.EnableRetryOnFailure();
        });
});

builder.Services.AddHttpClient();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddScoped<NewsSlugService>();
builder.Services.AddScoped<NewsSimilarityService>();
builder.Services.AddScoped<ArticleFetcher>();
builder.Services.AddScoped<NewsDraftService>();
builder.Services.AddScoped<INewsDraftGenerator, OpenAiNewsDraftGenerator>();

builder.Services.AddScoped<IJournalSourceReader, RssJournalSourceReader>();
builder.Services.AddScoped<IJournalSourceReader, HtmlJournalSourceReader>();
builder.Services.AddScoped<JournalCrawlService>();
builder.Services.AddHostedService<JournalCrawlBackgroundService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "LP.Journal",
    utc = DateTime.UtcNow
}));

app.MapControllers();

app.Run();
