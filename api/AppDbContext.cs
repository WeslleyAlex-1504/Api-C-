using Microsoft.EntityFrameworkCore;
using api.Model.usuario;

namespace api.DbContext
{
    public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<usuarioModel> usuario { get; set; }
        public DbSet<logModel.ControleLogModel> controleLog { get; set; }
    }
}