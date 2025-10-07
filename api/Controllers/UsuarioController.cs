using Carter;
using Microsoft.EntityFrameworkCore;
using api.DbContext;
using api.Model.usuario;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

public class UsuarioModule : CarterModule
{

    private readonly IConfiguration _config;

    public UsuarioModule(IConfiguration config)
    {
        _config = config;
    }
    public override void AddRoutes(IEndpointRouteBuilder app)
    {

        app.MapPost("/usuarios", async (AppDbContext db, usuarioModel usuario) =>
        {
            var passwordHasher = new PasswordHasher<usuarioModel>();
            usuario.Senha = passwordHasher.HashPassword(usuario, usuario.Senha);

            db.usuario.Add(usuario);
            await db.SaveChangesAsync();

            var usuarioResponse = new
            {
                usuario.Id,
                usuario.Nome,
                usuario.Email,
                usuario.Telefone,
                usuario.Cpf,
                usuario.Idade,
                usuario.Admin,
                usuario.Ativo
            };

            return Results.Created($"/usuarios/{usuario.Id}", usuarioResponse);
        });

        app.MapPatch("/usuarios/{id:int}", async (int id, AppDbContext db, usuarioPatchModel usuarioAtualizado) =>
        {
            var usuarioExistente = await db.usuario.FindAsync(id);
            if (usuarioExistente == null)
            {
                return Results.NotFound(new { mensagem = "Usuário não encontrado." });
            }

            if (!string.IsNullOrEmpty(usuarioAtualizado.Nome))
                usuarioExistente.Nome = usuarioAtualizado.Nome;

            if (!string.IsNullOrEmpty(usuarioAtualizado.Senha))
                usuarioExistente.Senha = usuarioAtualizado.Senha;

            if (!string.IsNullOrEmpty(usuarioAtualizado.Email))
                usuarioExistente.Email = usuarioAtualizado.Email;

            if (!string.IsNullOrEmpty(usuarioAtualizado.Telefone))
                usuarioExistente.Telefone = usuarioAtualizado.Telefone;

            if (!string.IsNullOrEmpty(usuarioAtualizado.Cpf))
                usuarioExistente.Cpf = usuarioAtualizado.Cpf;

            if (usuarioAtualizado.Idade.HasValue)
                usuarioExistente.Idade = usuarioAtualizado.Idade.Value;

            if (usuarioAtualizado.Ativo.HasValue)
                usuarioExistente.Ativo = usuarioAtualizado.Ativo.Value;

            if (usuarioAtualizado.Admin.HasValue)
                usuarioExistente.Admin = usuarioAtualizado.Admin.Value;

            await db.SaveChangesAsync();

            return Results.Ok(usuarioExistente);

        });

        app.MapDelete("/usuarios/{id:int}", async (int id, AppDbContext db) =>
        {
            var usuario = await db.usuario.FindAsync(id);
            if (usuario == null)
            {
                return Results.NotFound(new { mensagem = "Usuário não encontrado." });
            }

            db.usuario.Remove(usuario);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        app.MapGet("/usuarios", async (AppDbContext db,int? id, string? nome, string? email, string? telefone,  string? cpf, int? idade, bool? admin, bool? ativo) =>
        {
            var query = db.usuario.AsQueryable();

            if (id.HasValue)
                query = query.Where(paran => paran.Id == id.Value);

            if (!string.IsNullOrEmpty(nome))
                query = query.Where(paran => paran.Nome.Contains(nome));

            if (!string.IsNullOrEmpty(email))
                query = query.Where(paran => paran.Email.Contains(email));

            if (!string.IsNullOrEmpty(telefone))
                query = query.Where(paran => paran.Telefone.Contains(telefone));

            if (!string.IsNullOrEmpty(cpf))
                query = query.Where(paran => paran.Cpf.Contains(cpf));

            if (idade.HasValue)
                query = query.Where(paran => paran.Idade == idade.Value);

            if (admin.HasValue)
                query = query.Where(paran => paran.Admin == admin.Value);

            if (ativo.HasValue)
                query = query.Where(paran => paran.Ativo == ativo.Value);

            var usuariosFiltrados = await query.ToListAsync();
            return Results.Ok(usuariosFiltrados);
        });

        app.MapPost("/login", async (AppDbContext db, IConfiguration _config, string email, string senha) =>
        {
            var usuario = await db.usuario.FirstOrDefaultAsync(u => u.Email == email);
            if (usuario == null)
                return Results.Unauthorized();

            var passwordHasher = new PasswordHasher<usuarioModel>();
            var resultado = passwordHasher.VerifyHashedPassword(usuario, usuario.Senha, senha);

            if (resultado == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            var log2 = new logModel.ControleLogModel
            {
                UsuarioId = usuario.Id,
                LoginEm = DateTime.UtcNow,
                Ativo = true
            };

            db.controleLog.Add(log2);
            await db.SaveChangesAsync();

            var key = Encoding.ASCII.GetBytes(_config["Jwt:Secret"]);

            var claims = new[]
            {
        new Claim(JwtRegisteredClaimNames.Sub, usuario.Email),
        new Claim("id", usuario.Id.ToString()),
        new Claim("nome", usuario.Nome),
        new Claim("admin", usuario.Admin.ToString())
    };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return Results.Ok(new { token = tokenString });
        });

        app.MapGet("/retrieve", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var idClaim = user.FindFirst("id")?.Value;
            var adminClaim = user.FindFirst("admin")?.Value;

            if (idClaim == null)
                return Results.Unauthorized();

            if (!int.TryParse(idClaim, out int usuarioId))
                return Results.Unauthorized();

            var usuario = await db.usuario
                .Where(u => u.Id == usuarioId)
                .Select(u => new
                {
                    u.Id,
                    u.Nome,
                    u.Email,
                    u.Telefone,
                    u.Cpf,
                    u.Idade,
                    u.Ativo,
                    Roles = u.Admin ? new[] { "Admin" } : new[] { "User" }
                })
                .FirstOrDefaultAsync();

            if (usuario == null)
                return Results.NotFound(new { mensagem = "Usuário não encontrado." });

            return Results.Ok(usuario);

        });


    }
}
