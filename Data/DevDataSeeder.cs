using Microsoft.AspNetCore.Identity;
public static class DevDataSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var r in new[] { "Resident", "Dispatcher", "Crew" })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        async Task Add(string email, string role)
        {
            if (await users.FindByEmailAsync(email) != null) return;
            var u = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            await users.CreateAsync(u, "Test@12345");
            await users.AddToRoleAsync(u, role);
        }
        await Add("resident1@test.com", "Resident");
        await Add("resident2@test.com", "Resident");
        await Add("dispatcher1@test.com", "Dispatcher");
        await Add("crew1@test.com", "Crew");
        await Add("crew2@test.com", "Crew");
    }
}