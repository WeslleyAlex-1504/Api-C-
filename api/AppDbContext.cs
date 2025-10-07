using Microsoft.EntityFrameworkCore;
using api.Model.usuario;
using api.Model.viaCep;


namespace api.DbContext
{
    public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<usuarioModel> usuario { get; set; }
        public DbSet<logModel.ControleLogModel> controleLog { get; set; }
        public DbSet<EnderecoModel> endereco { get; set; }
        public DbSet<ProdutoModel> produto { get; set; }
        public DbSet<CategoriaModel> categoria { get; set; }
    }
}