using Microsoft.AspNetCore.Identity;

namespace CopilotBlazorTemplate.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
