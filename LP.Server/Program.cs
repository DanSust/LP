using DeepSeek.ApiClient.Extensions;
using LP.Common.Interfaces;
using LP.Entity;
using LP.Entity.Interfaces;
using LP.Entity.Store;
using LP.Server.Extensions;
using LP.Server.OAuth;
using LP.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.DotNet.Scaffolding.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Http.Logging;



namespace LP.Server;
class Program
{
    private const string ApiVersion = "v1";
    private const string ApiName = "StatMonitoring API";
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("Redis");
            options.InstanceName = "LP_";
        });


        var AIkey = builder.Configuration["DeepSeek:Key"];

        builder.Services.AddDeepSeekClient(builder.Configuration["DeepSeek:Key"]);
        builder.Services.Configure<SmtpConfig>(builder.Configuration.GetSection("Smtp"));

        var connection = builder.Configuration.GetConnectionString("DefaultConnection");
        builder.Services.AddDbContext<ApplicationContext>(
            o => o.UseSqlServer(connection, m => m.MigrationsAssembly("LP.Entity"))
                .LogTo(Console.WriteLine, LogLevel.Information));
        builder.Services.Configure<CookiePolicyOptions>(options =>
         {
             options.MinimumSameSitePolicy = SameSiteMode.Lax;
             options.Secure = CookieSecurePolicy.SameAsRequest;
         });
        builder.Services.AddCors(options =>
        {
            //options.AddDefaultPolicy(policy =>
            //{
            //    policy.AllowAnyOrigin();
            //    policy.AllowAnyHeader();
            //    policy.AllowAnyMethod();
            //});
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(
                    "https://oauth.telegram.org",
                    //"http://localhost:4200", 
                    "https://localhost:4200", 
                    "https://localhost:7010",
                    //"http://localhost",
                    "https://localhost",
                    "http://127.0.0.1:7010",
                    "https://127.0.0.1:7010",
                    "http://127.0.0.1",
                    "https://127.0.0.1",
                    "https://127.0.0.1:443"
                    );
                policy.AllowAnyHeader();
                policy.AllowAnyMethod();
                policy.AllowCredentials();
            });
        });


        //builder.Services.AddSwaggerGen();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        builder.Services.Configure<IdentityOptions>(o =>
        {
            o.Lockout.AllowedForNewUsers = true;
            o.User.RequireUniqueEmail = true;
        });

        builder.Services.AddAppAuthentication(builder.Configuration);

        //builder.Services.AddAuthentication(o =>
        //    {
        //        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        //    })
        //    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
        //    {
        //        o.Cookie.Name = "auth"; // имя куки
        //        o.Cookie.HttpOnly = true;
        //        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        //        o.Cookie.SameSite = SameSiteMode.Lax;
        //        o.Cookie.Domain = null;
        //        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        //        o.SlidingExpiration = true;
        //        o.AccessDeniedPath = "/NoRights";
        //        o.LoginPath = "/auth";
        //    });
        
        
        builder.Services.AddAuthorization(o =>
        {
            o.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()        // ← ключевое
                .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
                .Build();
        });

        

        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient();
        //builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("OAuth"));
        //builder.Services.AddSingleton<GoogleProvider>();
        //builder.Services.AddSingleton<MailruProvider>();
        //builder.Services.AddSingleton<VkProvider>();
        //builder.Services.AddSingleton<IOAuthProvider>(x=>x.GetRequiredService<GoogleProvider>());
        //builder.Services.AddSingleton<IOAuthProvider>(x => x.GetRequiredService<MailruProvider>());
        //builder.Services.AddSingleton<IEnumerable<IOAuthProvider>>(x => x.GetServices<IOAuthProvider>());
        builder.Services.AddSingleton<LocalAIService>();
        builder.Services.AddSingleton<AIAnalyzerService>();
        builder.Services.AddScoped<IUserStore, UserStore>();
        builder.Services.AddScoped<ICityLoader, CityLoader>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<ILikeRestrictionService, LikeRestrictionService>();
        builder.Services.AddScoped<LP.Common.JwtTokenParser>();
        builder.Services.AddScoped<InterestsStore>();
        builder.Services.AddSingleton<IEmailService, EmailService>();

        //builder.Services.AddHttpClient<GoogleProvider>().AddLogger<IHttpClientLogger>();   // 
        builder.Logging.AddConsole();
        builder.Services.AddControllers();

        var app = builder.Build();
        app.UseHttpsRedirection();
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.MapOpenApi();

            //app.UseSwagger();
            //app.UseSwaggerUI(o =>
            //{
            //    o.RoutePrefix = "swagger";
            //    o.SwaggerEndpoint($"{ApiVersion}/swagger.json", ApiName);
            //    o.DisplayOperationId();
            //    o.DisplayRequestDuration();
            //    o.OAuthClientId("swagger-ui");
            //});
            //app.MapEndpoints(endpoints =>
            //{
            //    // Your own endpoints go here, and then...
            //    endpoints.MapSwagger();
            //});
        }

        //app.Use(async (ctx, next) =>
        //{
        //    if (ctx.Request.Path == "/api/auth/logout")
        //    {
        //        Console.WriteLine("!!! LOGOUT PATH HIT BEFORE AUTH !!!");
        //        Console.WriteLine($"Method: {ctx.Request.Method}");
        //        Console.WriteLine($"Has auth cookie: {ctx.Request.Cookies.ContainsKey("auth")}");
        //        Console.WriteLine($"Has UserId cookie: {ctx.Request.Cookies.ContainsKey("UserId")}");

        //        // ВРЕМЕННО - пропускаем без авторизации
        //        var authService = ctx.RequestServices.GetRequiredService<IAuthService>();
        //        var userStore = ctx.RequestServices.GetRequiredService<IUserStore>();

        //        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        //        ctx.Response.Cookies.Delete("auth");
        //        ctx.Response.Cookies.Delete("UserId");

        //        await ctx.Response.WriteAsJsonAsync(new { success = true });
        //        return;
        //    }
        //    await next();
        //});

        //app.UseCors(b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        app.UseRouting();
        //app.UseCookiePolicy();
        app.UseCors("AllowFrontend");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(builder.Environment.ContentRootPath, "..", "img")),
            RequestPath = "/img"
        });

        //using (var scope = app.Services.CreateScope())
        //{
        //    var cityLoader = scope.ServiceProvider.GetRequiredService<ICityLoader>();
        //    var count = await cityLoader.LoadFromTextFileAsync(@"d:\\Work\\LP\\LP.Entity\\Need\\goroda.txt");
        //    Console.WriteLine($"Loaded {count} cities");
        //}

        await app.RunAsync();
    }
}
