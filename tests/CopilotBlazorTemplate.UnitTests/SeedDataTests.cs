using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CopilotBlazorTemplate.Core.Data;
using CopilotBlazorTemplate.Core.Entities;

namespace CopilotBlazorTemplate.UnitTests;

public class SeedDataTests
{
    private ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SeedData_Creates_Two_Roles()
    {
        using var sp = CreateServiceProvider();
        using var scope = sp.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        await SeedData.InitializeAsync(scope.ServiceProvider);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True(await roleManager.RoleExistsAsync("Admin"));
        Assert.True(await roleManager.RoleExistsAsync("User"));
    }

    [Fact]
    public async Task SeedData_Creates_Two_Users()
    {
        using var sp = CreateServiceProvider();
        using var scope = sp.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        await SeedData.InitializeAsync(scope.ServiceProvider);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync("admin@template.local");
        var user = await userManager.FindByEmailAsync("user@template.local");

        Assert.NotNull(admin);
        Assert.Equal("Administrator", admin.DisplayName);
        Assert.NotNull(user);
        Assert.Equal("Demo User", user.DisplayName);
    }

    [Fact]
    public async Task SeedData_Is_Idempotent()
    {
        using var sp = CreateServiceProvider();
        using var scope = sp.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        await SeedData.InitializeAsync(scope.ServiceProvider);
        await SeedData.InitializeAsync(scope.ServiceProvider); // Run twice

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var users = userManager.Users.ToList();
        Assert.Equal(2, users.Count);
    }
}
