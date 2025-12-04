using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using api.DbContext;
using api.Model.usuario;
using api.Model.ViaCep;
using Carter;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public class EnderecoModule : CarterModule
{

    private readonly IConfiguration _config;
    private readonly ViaCepService _viaCepService;

    public EnderecoModule(IConfiguration config, ViaCepService viaCepService)
    {
        _config = config;
        _viaCepService = viaCepService;
    }
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/endereco", async (AppDbContext db, EnderecoCreateDados endereco) =>
        {
            var usuario = await db.usuario.FirstOrDefaultAsync(u => u.Id == endereco.UsuarioId);
            if (usuario == null)
                return Results.NotFound(new { message = "Usuário com este ID não encontrado." });

            bool existe = await db.endereco.AnyAsync(e => e.Cep == endereco.Cep && e.UsuarioId == usuario.Id);
            if (existe)
                return Results.BadRequest(new { message = "Este CEP já está salvo nos seus endereços" });

            var dadosCep = await _viaCepService.BuscarPorCep(endereco.Cep);

            if (dadosCep == null || string.IsNullOrEmpty(dadosCep.Logradouro))
                return Results.BadRequest(new { message = "CEP inválido." });

            var endereco2 = new EnderecoModel
            {
                UsuarioId = usuario.Id,
                Cep = endereco.Cep,
                Numero = endereco.Numero,
                Rua = dadosCep.Logradouro,
                Cidade = dadosCep.Localidade,
                Estado = dadosCep.Uf,
                Bairro = dadosCep.Bairro,
                Pais = "Brasil",
                Ativo = true
            };

            db.endereco.Add(endereco2);
            await db.SaveChangesAsync();

            // 🔥 Verificar se já existe endereço principal
            bool jaTemPrincipal = await db.enderecoPrincipal
                .AnyAsync(ep => ep.UsuarioId == usuario.Id);

            // 🔥 Se NÃO tiver, criamos automaticamente
            if (!jaTemPrincipal)
            {
                var principal = new EnderecoPrincipal
                {
                    UsuarioId = usuario.Id,
                    EnderecoId = endereco2.Id
                };

                db.enderecoPrincipal.Add(principal);
                await db.SaveChangesAsync();
            }

            return Results.Created($"/endereco", endereco2);

        }).WithTags("Endereços");

        app.MapGet("/endereco", async (AppDbContext db, int? id, string? cep, string? cidade, string? estado) =>
        {
            var query = db.endereco.AsQueryable();

            if (id != 0) 
            {
                var usuario = await db.usuario.FirstOrDefaultAsync(u => u.Id == id);
                if (usuario == null)
                    return Results.NotFound(new { message = "Usuário com este Id não encontrado." });

                query = query.Where(e => e.UsuarioId == usuario.Id);
            }

            if (!string.IsNullOrEmpty(cep))
                query = query.Where(e => e.Cep.Contains(cep));

            if (!string.IsNullOrEmpty(cidade))
                query = query.Where(e => e.Cidade.Contains(cidade));

            if (!string.IsNullOrEmpty(estado))
                query = query.Where(e => e.Estado.Contains(estado));

            var enderecos = await query.ToListAsync();
            return Results.Ok(enderecos);
        }).WithTags("Endereços");

        app.MapDelete("/endereco/{id:int}", async (int id, AppDbContext db) =>
        {
            var endereco = await db.endereco.FindAsync(id);
            if (endereco == null)
                return Results.NotFound(new { mensagem = "Endereço não encontrado." });

            // Capturar o usuário dono desse endereço
            var usuarioId = endereco.UsuarioId;

            // Encontrar o registro de endereço principal que aponta para este endereço
            var principal = await db.enderecoPrincipal
                .FirstOrDefaultAsync(ep => ep.EnderecoId == id);

            // Remover o endereço
            db.endereco.Remove(endereco);

            // Se ele era principal, remover e tentar promover outro
            if (principal != null)
            {
                db.enderecoPrincipal.Remove(principal);

                // Buscar outro endereço do usuário
                var outroEndereco = await db.endereco
                    .Where(e => e.UsuarioId == usuarioId && e.Id != id)
                    .FirstOrDefaultAsync();

                if (outroEndereco != null)
                {
                    // Criar novo principal
                    var novoPrincipal = new EnderecoPrincipal
                    {
                        UsuarioId = usuarioId,
                        EnderecoId = outroEndereco.Id
                    };

                    await db.enderecoPrincipal.AddAsync(novoPrincipal);
                }
            }

            await db.SaveChangesAsync();
            return Results.NoContent();

        }).WithTags("Endereços");

        app.MapPatch("/endereco/{id:int}", async (int id, AppDbContext db, EnderecoPatchDados enderecoAtualizado) =>
        {

            var enderecoExistente = await db.endereco.FindAsync(id);
            if (enderecoExistente == null)
                return Results.NotFound(new { message = "Endereço não encontrado." });


            if (!string.IsNullOrEmpty(enderecoAtualizado.Cep))
            {

                var dadosCep = await _viaCepService.BuscarPorCep(enderecoAtualizado.Cep);
                if (dadosCep != null)
                {
                    enderecoExistente.Rua = dadosCep.Logradouro;
                    enderecoExistente.Cidade = dadosCep.Localidade;
                    enderecoExistente.Estado = dadosCep.Uf;
                    enderecoExistente.Pais = "Brasil";
                }

                enderecoExistente.Cep = enderecoAtualizado.Cep;
            }

            if (enderecoAtualizado.Numero.HasValue)
                enderecoExistente.Numero = enderecoAtualizado.Numero.Value;

            if (enderecoAtualizado.Ativo.HasValue)
                enderecoExistente.Ativo = enderecoAtualizado.Ativo.Value;

            await db.SaveChangesAsync();

            return Results.Ok(enderecoExistente);
        }).WithTags("Endereços");

        app.MapPost("/endereco-principal", async (AppDbContext db, EnderecoPrincipalCreate dto) =>
        {
            var usuario = await db.usuario.FindAsync(dto.UsuarioId);
            if (usuario == null)
                return Results.NotFound(new { message = "Usuário não encontrado." });

            var endereco = await db.endereco.FindAsync(dto.EnderecoId);
            if (endereco == null)
                return Results.NotFound(new { message = "Endereço não encontrado." });

            // ❗ Verificar se este endereço realmente pertence ao usuário
            if (endereco.UsuarioId != dto.UsuarioId)
                return Results.BadRequest(new { message = "Este endereço não pertence ao usuário informado." });

            // Verifica se já existe endereço principal
            var existente = await db.enderecoPrincipal
                .FirstOrDefaultAsync(e => e.UsuarioId == dto.UsuarioId);

            if (existente != null)
                return Results.BadRequest(new { message = "Usuário já possui um endereço principal." });

            var novo = new EnderecoPrincipal
            {
                UsuarioId = dto.UsuarioId,
                EnderecoId = dto.EnderecoId
            };

            db.enderecoPrincipal.Add(novo);
            await db.SaveChangesAsync();

            return Results.Created("/endereco-principal", novo);

        }).WithTags("Endereço Principal");


        // READ
        app.MapGet("/endereco-principal/{usuarioId:int}", async (AppDbContext db, int usuarioId) =>
        {
            var enderecoPrincipal = await db.enderecoPrincipal
                .Include(e => e.Endereco)
                .FirstOrDefaultAsync(e => e.UsuarioId == usuarioId);

            if (enderecoPrincipal == null)
                return Results.NotFound(new { message = "Endereço principal não encontrado para este usuário." });

            return Results.Ok(enderecoPrincipal);
        }).WithTags("Endereço Principal");


        // UPDATE (PATCH)
        app.MapPatch("/endereco-principal/{id:int}", async (AppDbContext db, int id, EnderecoPrincipalPatch dto) =>
        {
            var existente = await db.enderecoPrincipal.FindAsync(id);
            if (existente == null)
                return Results.NotFound(new { message = "Registro não encontrado." });

            // Validar troca de usuário
            if (dto.UsuarioId.HasValue)
            {
                var u = await db.usuario.FindAsync(dto.UsuarioId.Value);
                if (u == null)
                    return Results.BadRequest(new { message = "Usuário inválido." });

                existente.UsuarioId = dto.UsuarioId.Value;
            }

            // Validar troca de endereço
            if (dto.EnderecoId.HasValue)
            {
                var e = await db.endereco.FindAsync(dto.EnderecoId.Value);
                if (e == null)
                    return Results.BadRequest(new { message = "Endereço inválido." });

                // Verificar se o endereço pertence ao usuário
                // Observação: aqui assumo que sua tabela Endereco possui UsuarioId
                if (e.UsuarioId != existente.UsuarioId)
                {
                    return Results.BadRequest(new
                    {
                        message = "Você não pode usar um endereço que não pertence ao usuário vinculado."
                    });
                }

                existente.EnderecoId = dto.EnderecoId.Value;
            }

            await db.SaveChangesAsync();
            return Results.Ok(existente);

        }).WithTags("Endereço Principal");


        // DELETE
        app.MapDelete("/endereco-principal/{id:int}", async (AppDbContext db, int id) =>
        {
            var existente = await db.enderecoPrincipal.FindAsync(id);
            if (existente == null)
                return Results.NotFound(new { message = "Registro não encontrado." });

            db.enderecoPrincipal.Remove(existente);
            await db.SaveChangesAsync();

            return Results.NoContent();
        }).WithTags("Endereço Principal");
    }
}