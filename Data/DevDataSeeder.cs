using Nagorik.Api;
using Nagorik.Api.Models;

public static class DevDataSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var users = db.Set<User>();

        async Task Add(string name, string email, string role)
        {
            if (users.Any(u => u.Email == email)) return;

            users.Add(new User
            {
                Name = name,
                Email = email,
                PhoneNumber = "",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@12345"),
                Role = role
            });

            await db.SaveChangesAsync();
        }

        await Add("Resident One", "resident1@test.com", "Resident");
        await Add("Resident Two", "resident2@test.com", "Resident");
        await Add("Dispatcher One", "dispatcher1@test.com", "Dispatcher");
        await Add("Crew One", "crew1@test.com", "Crew");
        await Add("Crew Two", "crew2@test.com", "Crew");


        // T-004.1: Seed crews
        var crew1User = users.FirstOrDefault(
            u => u.Email == "crew1@test.com");

        var crew2User = users.FirstOrDefault(
            u => u.Email == "crew2@test.com");

        if (crew1User != null &&
            !db.Crews.Any(c => c.UserId == crew1User.Id))
        {
            db.Crews.Add(new Crew
            {
                Name = "Crew A",
                UserId = crew1User.Id
            });
        }

        if (crew2User != null &&
            !db.Crews.Any(c => c.UserId == crew2User.Id))
        {
            db.Crews.Add(new Crew
            {
                Name = "Crew B",
                UserId = crew2User.Id
            });
        }

        await db.SaveChangesAsync();
    }
}