using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ARS.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            
            // Use a design-time connection string
            // You can update this with your actual MySQL credentials
            optionsBuilder.UseMySql(
                "Server=localhost;Database=ARSDatabase;User=Shiro;Password=white;",
                new MySqlServerVersion(new Version(8, 0, 21))
            );

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
