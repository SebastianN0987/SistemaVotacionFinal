using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemaVoto.Data.Data;
using SistemaVoto.MVC.Services;
using SistemaVoto.ApiConsumer;
using SistemaVoto.Modelos;
using Microsoft.AspNetCore.HttpOverrides;

namespace SistemaVoto.MVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ============================================================
            // 1. CONFIGURACIÓN DE LA API (PUNTO DE CONEXIÓN)
            // ============================================================
            var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://sistema-voto-api.onrender.com";
            apiBaseUrl = apiBaseUrl.TrimEnd('/');

            if (string.IsNullOrEmpty(apiBaseUrl))
            {
                apiBaseUrl = "https://sistema-voto-api.onrender.com";
            }

            // Limpieza de URL para evitar errores de formato
            apiBaseUrl = apiBaseUrl.TrimEnd('/');

            // Configurar URLs globales para el consumidor de API (Crud<T>)
            Crud<Eleccion>.UrlBase = $"{apiBaseUrl}/api/elecciones";
            Crud<Candidato>.UrlBase = $"{apiBaseUrl}/api/candidatos";
            Crud<Voto>.UrlBase = $"{apiBaseUrl}/api/votos";
            Crud<Lista>.UrlBase = $"{apiBaseUrl}/api/listas";
            Crud<Ubicacion>.UrlBase = $"{apiBaseUrl}/api/ubicaciones";
            Crud<RecintoElectoral>.UrlBase = $"{apiBaseUrl}/api/recintos";
            Crud<EleccionUbicacion>.UrlBase = $"{apiBaseUrl}/api/eleccionubicaciones";

            // ============================================================
            // 2. CONFIGURACIÓN DE BASE DE DATOS (POSTGRESQL)
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
            });

            // ============================================================
            // 4. REGISTRO DE SERVICIOS
            // ============================================================
            builder.Services.AddHttpContextAccessor();

            // ---> NUEVO: CONFIGURACIÓN DEL PROXY PARA RENDER <---
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // Limpiamos las redes para aceptar el balanceador de carga de Render
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // Cliente HTTP configurado con la URL de la API de Render
            builder.Services.AddHttpClient<ApiService>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl + "/");
            });

            builder.Services.AddScoped<JwtAuthService>();
            builder.Services.AddScoped<CalculoEscanosService>();
            builder.Services.AddScoped<LocalCrudService>();
            builder.Services.AddScoped<ElectionManagerService>();

            // Servicio en segundo plano para procesar estados de elecciones
            builder.Services.AddHostedService<ElectionBackgroundService>();

            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();

            var app = builder.Build();

            // ============================================================
            // 5. PIPELINE DE SOLICITUDES HTTP
            // ============================================================

            // ---> NUEVO: MIDDLEWARE DEL PROXY (Debe ir al inicio del pipeline) <---
            app.UseForwardedHeaders();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapRazorPages();

            // Seed de roles y administrador al iniciar la aplicación
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