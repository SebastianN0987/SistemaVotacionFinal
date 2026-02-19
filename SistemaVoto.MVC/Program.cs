using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using SistemaVoto.Data.Data;
using SistemaVoto.MVC.Services;
using SistemaVoto.ApiConsumer;
using SistemaVoto.Modelos;

namespace SistemaVoto.MVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ============================================================
            // 1. CONFIGURACIÓN DE LA API
            // ============================================================
            var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://sistema-voto-api.onrender.com";
            apiBaseUrl = apiBaseUrl.TrimEnd('/');

            if (string.IsNullOrEmpty(apiBaseUrl))
            {
                apiBaseUrl = "https://sistema-voto-api.onrender.com";
            }

            apiBaseUrl = apiBaseUrl.TrimEnd('/');

            Crud<Eleccion>.UrlBase = $"{apiBaseUrl}/api/elecciones";
            Crud<Candidato>.UrlBase = $"{apiBaseUrl}/api/candidatos";
            Crud<Voto>.UrlBase = $"{apiBaseUrl}/api/votos";
            Crud<Lista>.UrlBase = $"{apiBaseUrl}/api/listas";
            Crud<Ubicacion>.UrlBase = $"{apiBaseUrl}/api/ubicaciones";
            Crud<RecintoElectoral>.UrlBase = $"{apiBaseUrl}/api/recintos";
            Crud<EleccionUbicacion>.UrlBase = $"{apiBaseUrl}/api/eleccionubicaciones";

            // ============================================================
            // 2. CONFIGURACIÓN DE BASE DE DATOS
            // ============================================================
            var connectionString = builder.Configuration.GetConnectionString("DbContext.postgres-render")
                ?? builder.Configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddDbContext<SistemaVotoDbContext>(options =>
                options.UseNpgsql(connectionString));

            // ============================================================
            // 3. IDENTIDAD Y SEGURIDAD
            // ============================================================
            builder.Services.AddDefaultIdentity<IdentityUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 4;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<SistemaVotoDbContext>();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Auth/Login";
                options.LogoutPath = "/Auth/Logout";
                options.AccessDeniedPath = "/Auth/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromHours(4);
                options.SlidingExpiration = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });

            // ============================================================
            // 4. REGISTRO DE SERVICIOS Y PROXY
            // ============================================================
            builder.Services.AddHttpContextAccessor();

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.All;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.Services.AddAntiforgery(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });

            builder.Services.AddHttpClient<ApiService>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl + "/");
            });

            builder.Services.AddScoped<JwtAuthService>();
            builder.Services.AddScoped<CalculoEscanosService>();
            builder.Services.AddScoped<LocalCrudService>();
            builder.Services.AddScoped<ElectionManagerService>();
            builder.Services.AddHostedService<ElectionBackgroundService>();

            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();

            var app = builder.Build();

            // ============================================================
            // 5. PIPELINE DE SOLICITUDES HTTP
            // ============================================================
            app.UseForwardedHeaders();

            app.Use(async (context, next) =>
            {
                context.Request.Scheme = "https";
                await next();
            });

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapRazorPages();

            using (var scope = app.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                SeedRolesAndAdminAsync(roleManager, userManager).GetAwaiter().GetResult();
            }

            app.Run();
        }

        private static async Task SeedRolesAndAdminAsync(
            RoleManager<IdentityRole> roleManager,
            UserManager<IdentityUser> userManager)
        {
            string[] roles = { "Administrador", "Usuario" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var adminEmail = "admin@sistemavoto.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrador");
                }
            }
        }
    }
}