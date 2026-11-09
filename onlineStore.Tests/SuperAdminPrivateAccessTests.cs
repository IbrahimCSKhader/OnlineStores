using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using onlineStore.Controllers;
using onlineStore.Data;
using onlineStore.Models.Identity;

namespace onlineStore.Tests;

public class SuperAdminPrivateAccessTests
{
    [Theory]
    [InlineData("  ADMIN@example.test  ", true, true, false, 204)]
    [InlineData("other@example.test", true, true, false, 403)]
    [InlineData("admin@example.test", false, true, false, 403)]
    [InlineData("admin@example.test", true, false, false, 403)]
    [InlineData("admin@example.test", true, true, true, 403)]
    [InlineData(null, true, true, false, 503)]
    public async Task VerifiesOnlyTheConfiguredActiveSuperAdmin(
        string? allowedEmail, bool hasRole, bool active, bool deleted, int expectedStatus)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<AppUser>().AddRoles<AppRole>().AddEntityFrameworkStores<AppDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var account = new AppUser
        {
            UserName = "admin@example.test", Email = "admin@example.test",
            FirstName = "Test", LastName = "Admin", IsActive = active,
        };
        Assert.True((await manager.CreateAsync(account)).Succeeded);
        if (hasRole)
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            Assert.True((await roles.CreateAsync(new AppRole { Name = "SuperAdmin" })).Succeeded);
            Assert.True((await manager.AddToRoleAsync(account, "SuperAdmin")).Succeeded);
        }
        if (deleted)
        {
            account.IsDeleted = true;
            Assert.True((await manager.UpdateAsync(account)).Succeeded);
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Role, "SuperAdmin"),
        ], "test"));
        var controller = new SuperAdminDashboardController(null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } },
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SecretAdmin:Email"] = allowedEmail,
        }).Build();

        var result = await controller.VerifyPrivateAccess(config, manager);

        if (expectedStatus == 403) Assert.IsType<ForbidResult>(result);
        else if (expectedStatus == 204) Assert.IsType<NoContentResult>(result);
        else Assert.Equal(expectedStatus, Assert.IsType<ObjectResult>(result).StatusCode);
    }
}
