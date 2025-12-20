using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QingFeng.Data;

public class QingFengDbContextFactory : IDesignTimeDbContextFactory<QingFengDbContext>
{
    public QingFengDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<QingFengDbContext>();
        optionsBuilder.UseSqlite("Data Source=qingfeng.db");

        return new QingFengDbContext(optionsBuilder.Options);
    }
}
