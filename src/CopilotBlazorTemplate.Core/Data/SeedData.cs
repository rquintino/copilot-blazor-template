using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using CopilotBlazorTemplate.Core.Entities;

namespace CopilotBlazorTemplate.Core.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Ensure roles
        string[] roles = ["Admin", "User"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed admin
        if (await userManager.FindByEmailAsync("admin@template.local") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@template.local",
                Email = "admin@template.local",
                DisplayName = "Administrator",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // Seed user
        if (await userManager.FindByEmailAsync("user@template.local") is null)
        {
            var user = new ApplicationUser
            {
                UserName = "user@template.local",
                Email = "user@template.local",
                DisplayName = "Demo User",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "User123!");
            await userManager.AddToRoleAsync(user, "User");
        }
    }
}
