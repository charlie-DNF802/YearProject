using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ward_Management_System.Data;
using Ward_Management_System.Models;

namespace Ward_Management_System.Services
{
    public class SeedServices
    {
        public static async Task SeedDatabase(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Users>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedServices>>();

            try
            {
                //Ensures that the database is ready
                logger.LogInformation("Ensuring database is created.");
                await context.Database.EnsureCreatedAsync();


                //Adds Roles
                logger.LogInformation("Seeding roles.");
                await AddRoleAsync(roleManager, "Admin");
                await AddRoleAsync(roleManager, "User");
                await AddRoleAsync(roleManager, "WardAdmin");
                await AddRoleAsync(roleManager, "Nurse");
                await AddRoleAsync(roleManager, "Sister");
                await AddRoleAsync(roleManager, "Doctor");


                //Add Admin User
                logger.LogInformation("Seeding admin user");
                var adminEmail = "admin@devsquad.com";
                if(await userManager.FindByEmailAsync(adminEmail) == null)
                {
                    var adminUser = new Users
                    {
                        FullName = "Charlie",
                        UserName = adminEmail,
                        Email = adminEmail,
                        NormalizedEmail = adminEmail.ToUpper(),
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString(),
                         Age = 37,
                        PhoneNumber = "0781113961",
                        Address = "2 Myrtle Avenue",
                        IdNumber = "0311305485089",
                        Gender = "Male"
                    };
                    var result = await userManager.CreateAsync(adminUser, "Admin@123");
                    if (result.Succeeded)
                    {
                        logger.LogInformation("Assigning All roles to the admin user.");
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                        await userManager.AddToRoleAsync(adminUser, "Doctor");
                        await userManager.AddToRoleAsync(adminUser, "Nurse");
                        await userManager.AddToRoleAsync(adminUser, "WardAdmin");
                        await userManager.AddToRoleAsync(adminUser, "Sister");
                        await userManager.AddToRoleAsync(adminUser, "User");
                    }
                    else
                    {
                        logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "An error occured while seeding the database."); 
            }
        }
        private static async Task AddRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if(!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }
}
