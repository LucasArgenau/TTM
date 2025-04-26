using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TorneioTenisMesa.Models;

var builder = WebApplication.CreateBuilder(args);

// Configura o DbContext para utilizar o SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configura Identity com chave primária do tipo int
builder.Services.AddIdentity<User, IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Middleware para controle de erros e ambiente de desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Configuração de autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();

// Configura a rota padrão
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Chama a função assíncrona para rodar o seed
await SeedDatabaseAsync(app);

app.Run();

// Função para Seed de dados (roles e usuários)
async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<User>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<int>>>();

    Console.WriteLine("Executando seed de usuários e roles...");

    // Criar roles (Admin, Player)
    var roles = new[] { "Admin", "Player" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<int>(role));
            Console.WriteLine($"Role '{role}' criada.");
        }
    }

    // Criar usuário Admin
    var adminEmail = "admin@example.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        var newAdmin = new User
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(newAdmin, "AdminPassword123!");
        if (result.Succeeded)
        {
            newAdmin.SecurityStamp = Guid.NewGuid().ToString();
            await userManager.UpdateAsync(newAdmin);
            await userManager.AddToRoleAsync(newAdmin, "Admin");
            Console.WriteLine("Usuário admin@example.com criado.");
        }
        else
        {
            foreach (var error in result.Errors)
                Console.WriteLine($"Erro ao criar admin: {error.Description}");
        }
    }

    // Criar usuário Player
    var playerEmail = "player@example.com";
    var playerUser = await userManager.FindByEmailAsync(playerEmail);
    if (playerUser == null)
    {
        var newPlayer = new User
        {
            UserName = playerEmail,
            Email = playerEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(newPlayer, "PlayerPassword123!");
        if (result.Succeeded)
        {
            newPlayer.SecurityStamp = Guid.NewGuid().ToString();
            await userManager.UpdateAsync(newPlayer);
            await userManager.AddToRoleAsync(newPlayer, "Player");
            Console.WriteLine("Usuário player@example.com criado.");
        }
        else
        {
            foreach (var error in result.Errors)
                Console.WriteLine($"Erro ao criar player: {error.Description}");
        }
    }
}
