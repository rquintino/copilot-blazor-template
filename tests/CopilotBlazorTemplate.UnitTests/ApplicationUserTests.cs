using CopilotBlazorTemplate.Core.Entities;

namespace CopilotBlazorTemplate.UnitTests;

public class ApplicationUserTests
{
    [Fact]
    public void DisplayName_Can_Be_Set()
    {
        var user = new ApplicationUser();
        user.DisplayName = "Test User";
        Assert.Equal("Test User", user.DisplayName);
    }

    [Fact]
    public void DisplayName_Defaults_To_Empty()
    {
        var user = new ApplicationUser();
        Assert.Equal(string.Empty, user.DisplayName);
    }
}
