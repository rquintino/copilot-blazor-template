using Microsoft.AspNetCore.Identity;

namespace BlazorDemo.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
