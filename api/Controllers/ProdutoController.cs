using System.IdentityModel.Tokens.Jwt;
using System.Runtime.ConstrainedExecution;
using System.Security.Claims;
using System.Text;
using api.DbContext;
using api.Model.avaliacao;
using api.Model.produto;
using api.Model.usuario;
using api.Model.ViaCep;
using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public class ProdutoModule : CarterModule
{

    private readonly IConfiguration _config;
    private readonly ViaCepService _viaCepService;

    public ProdutoModule(IConfiguration config, ViaCepService viaCepService)
    {
        _config = config;
        _viaCepService = viaCepService;
    }
    public override void AddRoutes(IEndpointRouteBuilder app)
    {

    app.MapPost("/produto", async (HttpRequest request, AppDbContext db) =>
        {
            var form = await request.ReadFormAsync();

            var cpf = form["cpf"].ToString();
            var usuario = await db.usuario.FirstOrDefaultAsync(u => u.Cpf == cpf);
            if (usuario == null)
                return Results.NotFound(new { message = "Usuário com este CPF não encontrado." });

            bool produtoExiste = await db.produto.AnyAsync(p => p.Nome == form["Nome"].ToString() && p.UsuarioId == usuario.Id);
            if (produtoExiste)
                return Results.BadRequest(new { message = "Já existe um produto com este nome para este usuário." });


            var produto = new ProdutoModel
            {
                Nome = form["Nome"],
                Descricao = form["Descricao"],
                Valor = decimal.Parse(form["Valor"]),
                UsuarioId = usuario.Id,
                Desconto = decimal.Parse(form["Desconto"]),
                CategoriaId = int.Parse(form["CategoriaId"]),
                Ativo = bool.Parse(form["Ativo"])
            };

            var imgFile = form.Files["Img"];
            if (imgFile != null && imgFile.Length > 0)
            {
                using var ms = new MemoryStream();
                await imgFile.CopyToAsync(ms);
                produto.Img = Convert.ToBase64String(ms.ToArray());
            }

            db.produto.Add(produto);
            await db.SaveChangesAsync();

            if (form.TryGetValue("QtdEstoque", out var qtdEstoqueString) && int.TryParse(qtdEstoqueString, out var qtdEstoque))
            {
                var estoque = new EstoqueModel
                {
                    ProdutoId = produto.Id,
                    QtdEstoque = qtdEstoque
                };

                db.estoque.Add(estoque);
                await db.SaveChangesAsync();
            }

            return Results.Created($"/produto/{produto.Id}", produto);

        }).Accepts<ProdutoCreateDados>("multipart/form-data").WithTags("Produtos").RequireAuthorization();

    app.MapPost("/categoria", async (AppDbContext db, CategoriaModel categoria) =>
    {

    bool existe = await db.categoria.AnyAsync(c => c.Nome == categoria.Nome);
    if (existe)
        return Results.BadRequest(new { message = "Já existe uma categoria com este nome." });

    var novaCategoria = new CategoriaModel
    {
        Nome = categoria.Nome,
        Ativo = categoria.Ativo
    };

    db.categoria.Add(novaCategoria);
    await db.SaveChangesAsync();

    return Results.Created($"/categoria", novaCategoria);

}).WithTags("Categorias").RequireAuthorization();

    app.MapGet("/produto", async (AppDbContext db, string? vendedorNome, string? categoriaNome, string? nome, decimal? valorMinimo, decimal? valorMaximo) =>
        {
            var query = db.produto.AsQueryable();

            if (!string.IsNullOrEmpty(vendedorNome))
            {
                var usuarios = db.usuario
                                 .Where(u => u.Nome.Contains(vendedorNome))
                                 .Select(u => u.Id);

                query = query.Where(p => usuarios.Contains(p.UsuarioId));
            }

            if (!string.IsNullOrEmpty(categoriaNome))
            {
                var categorias = db.categoria
                                   .Where(c => c.Nome.Contains(categoriaNome))
                                   .Select(c => c.Id);

                query = query.Where(p => categorias.Contains(p.CategoriaId));
            }

            if (!string.IsNullOrEmpty(nome))
            {
                query = query.Where(p => p.Nome.Contains(nome));
            }

            if (valorMinimo.HasValue)
            {
                query = query.Where(p => p.Valor >= valorMinimo.Value);
            }

            if (valorMaximo.HasValue)
            {
                query = query.Where(p => p.Valor <= valorMaximo.Value);
            }

            var produtos = await query.ToListAsync();
            return Results.Ok(produtos);
        }).WithTags("Produtos");

    app.MapGet("/categoria", async (AppDbContext db, string? nome, int? id) =>
    {
        var query = db.categoria.AsQueryable();

        if (!string.IsNullOrEmpty(nome))
        {
            query = query.Where(p => p.Nome.Contains(nome));
        }

        if (id.HasValue)
        {
            query = query.Where(p => p.Id == id.Value);
        }

        var categorias = await query.ToListAsync();
        return Results.Ok(categorias);
    }).WithTags("Categorias");

    app.MapDelete("/produto/{id:int}", async (int id, AppDbContext db) =>
    {
        var produto = await db.produto.FindAsync(id);
        if (produto == null)
        {
            return Results.NotFound(new { mensagem = "produto não encontrado." });
        }

        db.produto.Remove(produto);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }).WithTags("Produtos").RequireAuthorization();

    app.MapDelete("/categoria/{id:int}", async (int id, AppDbContext db) =>
    {
        var categoria = await db.categoria.FindAsync(id);
        if (categoria == null)
        {
            return Results.NotFound(new { mensagem = "categoria não encontrado." });
        }

        db.categoria.Remove(categoria);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }).WithTags("Categorias").RequireAuthorization();

    app.MapPatch("/produto/{id:int}", async (int id, HttpRequest request, AppDbContext db) =>
{
    var produtoExistente = await db.produto.FindAsync(id);
    if (produtoExistente == null)
        return Results.NotFound(new { message = "Produto não encontrado." });

    var form = await request.ReadFormAsync();

    if (form.ContainsKey("Nome"))
        produtoExistente.Nome = form["Nome"];

    if (form.ContainsKey("Descricao"))
        produtoExistente.Descricao = form["Descricao"];

    if (form.ContainsKey("Valor"))
        produtoExistente.Valor = decimal.Parse(form["Valor"]);

    if (form.ContainsKey("Desconto"))
        produtoExistente.Desconto = decimal.Parse(form["Desconto"]);

    if (form.ContainsKey("CategoriaId"))
        produtoExistente.CategoriaId = int.Parse(form["CategoriaId"]);

    if (form.ContainsKey("Ativo"))
        produtoExistente.Ativo = bool.Parse(form["Ativo"]);

    var imgFile = form.Files["Img"];
    if (imgFile != null && imgFile.Length > 0)
    {
        using var ms = new MemoryStream();
        await imgFile.CopyToAsync(ms);
        produtoExistente.Img = Convert.ToBase64String(ms.ToArray());
    }

    await db.SaveChangesAsync();

    return Results.Ok(produtoExistente);
}).Accepts<IFormFile>("multipart/form-data").WithTags("Produtos").RequireAuthorization();

    app.MapPatch("/categoria/{id:int}", async (int id, AppDbContext db, CategoriaPatchDados categoriaAtualizada) =>
    {
    var categoriaExistente = await db.categoria.FindAsync(id);
    if (categoriaExistente == null)
        return Results.NotFound(new { message = "Categoria não encontrada." });

    if (!string.IsNullOrEmpty(categoriaAtualizada.Nome))
        categoriaExistente.Nome = categoriaAtualizada.Nome;

    if (categoriaAtualizada.Ativo.HasValue)
        categoriaExistente.Ativo = categoriaAtualizada.Ativo.Value;

    await db.SaveChangesAsync();

    return Results.Ok(categoriaExistente);
    }).WithTags("Categorias").RequireAuthorization();

    app.MapPost("/estoque", async (AppDbContext db, EstoqueModel estoque) =>
        {

        var produto = await db.produto.FirstOrDefaultAsync(p => p.Id == estoque.ProdutoId);
        if (produto == null)
            return Results.NotFound(new { message = "Produto não encontrado." });

        bool existe = await db.estoque.AnyAsync(e => e.ProdutoId == estoque.ProdutoId);
        if (existe)
            return Results.BadRequest(new { message = "Já existe estoque para esse produto." });


        db.estoque.Add(estoque);
        await db.SaveChangesAsync();

        return Results.Created($"/estoque", estoque);

    }).WithTags("Estoque").RequireAuthorization();

    app.MapGet("/estoque", async (AppDbContext db, int? ProdutoId) =>
        {
            var query = db.estoque.AsQueryable();

            if (ProdutoId.HasValue)
                query = query.Where(e => e.ProdutoId == ProdutoId.Value);

            var estoques = await query.ToListAsync();
            return Results.Ok(estoques);

        }).WithTags("Estoque");

    app.MapDelete("/estoque/{id:int}", async (int id, AppDbContext db) =>
    {
        var estoque = await db.estoque.FindAsync(id);
        if (estoque == null)
        {
            return Results.NotFound(new { mensagem = "estoque não encontrado." });
        }

        db.estoque.Remove(estoque);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }).WithTags("Estoque").RequireAuthorization();

    app.MapPatch("/estoque/{id:int}", async (int id, AppDbContext db, EstoquePatchDados estoqueAtualizado) =>
    {
    var estoqueExistente = await db.estoque.FindAsync(id);
    if (estoqueExistente == null)
        return Results.NotFound(new { message = "Categoria não encontrada." });

    if (estoqueAtualizado.QtdEstoque.HasValue)
            estoqueExistente.QtdEstoque = estoqueAtualizado.QtdEstoque.Value;

    if (estoqueAtualizado.Ativo.HasValue)
            estoqueExistente.Ativo = estoqueAtualizado.Ativo.Value;

    await db.SaveChangesAsync();

    return Results.Ok(estoqueExistente);
    }).WithTags("Estoque").RequireAuthorization();

    app.MapPost("/avaliacao", async (AppDbContext db, AvaliacaoModel avaliacao) =>
    {
        if (avaliacao.Numero < 1 || avaliacao.Numero > 5)
         return Results.BadRequest(new { message = "A avaliação deve ser um número entre 1 e 5." });

        var produto = await db.produto.FirstOrDefaultAsync(p => p.Id == avaliacao.ProdutoId);
        if (produto == null)
         return Results.NotFound(new { message = "Produto não encontrado." });

        db.avaliacao.Add(avaliacao);
         await db.SaveChangesAsync();

        return Results.Created($"/avaliacao/{avaliacao.Id}", avaliacao);
    }).WithTags("Avaliação").RequireAuthorization();

    app.MapGet("/avaliacao", async (AppDbContext db, [FromQuery] int produtoId) =>
    {
    var existeProduto = await db.produto.AnyAsync(p => p.Id == produtoId);
    if (!existeProduto)
        return Results.NotFound(new { message = "Produto não encontrado." });

    var avaliacoes = await db.avaliacao
        .Where(a => a.ProdutoId == produtoId && a.Ativo == true)
        .ToListAsync();

    if (avaliacoes.Count == 0)
        return Results.Ok(new { media = 0f });

    float soma = avaliacoes.Sum(a => a.Numero);
    float media = soma / avaliacoes.Count;

    return Results.Ok(new { media });
    }).WithTags("Avaliação");

    app.MapPost("/produtoImagem", async (HttpRequest request, AppDbContext db) =>
        {
            var form = await request.ReadFormAsync();

            if (!form.TryGetValue("produtoId", out var produtoIdString) || !int.TryParse(produtoIdString, out var produtoId))
                return Results.BadRequest(new { message = "ProdutoId é obrigatório e deve ser um número." });

            var arquivo = form.Files["imagem"];
            if (arquivo == null || arquivo.Length == 0)
                return Results.BadRequest(new { message = "Nenhum arquivo enviado." });

            string base64String;
            using (var ms = new MemoryStream())
            {
                await arquivo.CopyToAsync(ms);
                base64String = Convert.ToBase64String(ms.ToArray());
            }

            var img = new ProdutoImagem
            {
                ProdutoId = produtoId,
                Imagem = base64String
            };

            db.produtoImagem.Add(img);
            await db.SaveChangesAsync();

            return Results.Created($"/produtoImagem/{img.Id}", new
            {
                message = "Imagem adicionada ao produto.",
                img
            });

    }).Accepts<ProdutoImagem>("multipart/form-data").WithTags("ProdutoImagem").RequireAuthorization();

    app.MapGet("/produtoImagem/{produtoId}", async (AppDbContext db, int produtoId) =>
    {
    var imagens = await db.produtoImagem
        .Where(i => i.ProdutoId == produtoId)
        .ToListAsync();

    if (imagens.Count == 0)
        return Results.NotFound(new { message = "Nenhuma imagem encontrada para este produto." });

    return Results.Ok(imagens);
    }).WithTags("ProdutoImagem").RequireAuthorization();


    }
}