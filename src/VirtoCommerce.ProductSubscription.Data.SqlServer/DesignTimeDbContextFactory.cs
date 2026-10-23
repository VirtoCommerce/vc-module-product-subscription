using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VirtoCommerce.ProductSubscription.Data.Repositories;

namespace VirtoCommerce.ProductSubscription.Data.SqlServer;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProductSubscriptionDbContext>
{
    public ProductSubscriptionDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ProductSubscriptionDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=(local);User=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseSqlServer(
            connectionString,
            options => options.MigrationsAssembly(typeof(SqlServerDataAssemblyMarker).Assembly.GetName().Name));

        return new ProductSubscriptionDbContext(builder.Options);
    }
}
