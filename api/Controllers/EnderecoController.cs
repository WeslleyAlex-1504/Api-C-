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
                Pais = "Brasil",
                Ativo = true
            };

            db.endereco.Add(endereco2);
            await db.SaveChangesAsync();

            return Results.Created($"/endereco", endereco2);

        }).WithTags("Endereços");


        app.MapGet("/endereco", async (AppDbContext db, string? cpf, string? cep, string? cidade, string? estado) =>
        {
            var query = db.endereco.AsQueryable();

            if (!string.IsNullOrEmpty(cpf))
            {
                var usuario = await db.usuario.FirstOrDefaultAsync(u => u.Cpf == cpf);
                if (usuario == null)
                    return Results.NotFound(new { message = "Usuário com este CPF não encontrado." });

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
            {
                return Results.NotFound(new { mensagem = "endereco não encontrado." });
            }

            db.endereco.Remove(endereco);
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

    }
}