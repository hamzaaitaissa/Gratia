using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gratia.Domain.Entities; 


namespace Gratia.Infrastructure.Data
{
    public class GratiaDbContext : DbContext
    {
        public GratiaDbContext(DbContextOptions<GratiaDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
    }
}
