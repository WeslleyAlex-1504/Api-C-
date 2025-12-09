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
            using var transaction = await db.Database.BeginTransactionAsync();

            try
            {
                var form = await request.ReadFormAsync();

                if (!form.TryGetValue("UsuarioId", out var usuarioIdString) ||
                    !int.TryParse(usuarioIdString, out int usuarioId))
                {
                    return Results.BadRequest(new { message = "UsuarioId inválido ou não enviado." });
                }

                var usuario = await db.usuario.FirstOrDefaultAsync(u => u.Id == usuarioId);
                if (usuario == null)
                    return Results.NotFound(new { message = "Usuário não encontrado." });

                bool produtoExiste = await db.produto.AnyAsync(p =>
                    p.Nome == form["Nome"].ToString() &&
                    p.UsuarioId == usuario.Id
                );

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
                    Ativo = bool.Parse(form["Ativo"]),
                    Estado = form["Estado"],
                    Cep = form["Cep"],
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

                var imagensExtras = form.Files.GetFiles("Imagens");

                foreach (var arquivo in imagensExtras)
                {
                    if (arquivo.Length == 0)
                        continue;

                    using var ms = new MemoryStream();
                    await arquivo.CopyToAsync(ms);

                    db.produtoImagem.Add(new ProdutoImagem
                    {
                        ProdutoId = produto.Id,
                        Imagem = Convert.ToBase64String(ms.ToArray())
                    });
                }

                if (imagensExtras.Count > 0)
                    await db.SaveChangesAsync();

                if (form.TryGetValue("QtdEstoque", out var qtdEstoqueString) &&
                    int.TryParse(qtdEstoqueString, out var qtdEstoque))
                {
                    db.estoque.Add(new EstoqueModel
                    {
                        ProdutoId = produto.Id,
                        QtdEstoque = qtdEstoque
                    });

                    await db.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return Results.Created($"/produto/{produto.Id}", produto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Results.BadRequest(new
                {
                    message = "Erro ao criar o produto.",
                    erro = ex.Message
                });
            }

        }).WithTags("Produtos").RequireAuthorization();

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

    app.MapGet("/produto", async (AppDbContext db,int? id,int? usuarioId, bool ? ativo, string ? vendedorNome, string? categoriaNome, string? nome, decimal? valorMinimo, decimal? valorMaximo, int skip = 0, int take = 20) =>
        {
            var query = db.produto
               .Include(p => p.Categoria)
               .AsQueryable();

            if (usuarioId.HasValue)
            {
                query = query.Where(p => p.UsuarioId == usuarioId.Value);
            }

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

            if (id.HasValue)
            {
                query = query.Where(p => p.Id == id.Value);
            }

            if (valorMaximo.HasValue)
            {
                query = query.Where(p => p.Valor <= valorMaximo.Value);
            }

            if (ativo.HasValue)
            {
                query = query.Where(p => p.Ativo == ativo.Value);
            }

            query = query.Skip(skip).Take(take);

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
        var base64 = Convert.ToBase64String(ms.ToArray());
        var contentType = imgFile.ContentType ?? "image/jpeg";
        produtoExistente.Img = $"data:{contentType};base64,{base64}";
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


    app.MapPost("/checkout", async (AppDbContext db, CriarCheckoutDTO dto) =>
        {
            using var transaction = await db.Database.BeginTransactionAsync();

            try
            {
                var usuarioExiste = await db.usuario.AnyAsync(u => u.Id == dto.UsuarioId);
                if (!usuarioExiste)
                    return Results.BadRequest(new { message = "Usuário não encontrado." });

                var checkout = new CheckoutModel
                {
                    UsuarioId = dto.UsuarioId,
                    Itens = new List<CheckoutItemModel>()
                };

                db.checkout.Add(checkout);
                await db.SaveChangesAsync(); // salva para gerar id

                foreach (var item in dto.Itens)
                {
                    var produtoExiste = await db.produto.AnyAsync(p => p.Id == item.ProdutoId);
                    if (!produtoExiste)
                        return Results.BadRequest(new { message = $"Produto {item.ProdutoId} não encontrado." });

                    checkout.Itens.Add(new CheckoutItemModel
                    {
                        CheckoutId = checkout.Id,
                        ProdutoId = item.ProdutoId,
                        Quantidade = item.Quantidade
                    });
                }

                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                return Results.Created($"/checkout/{checkout.Id}", checkout);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Results.BadRequest(new { message = "Erro ao criar checkout.", erro = ex.Message });
            }

        }).WithTags("Checkout").RequireAuthorization();

    app.MapGet("/checkout/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            // 1. Recuperar o usuário logado pelo token JWT
            var userIdClaim = http.User.FindFirst("id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int usuarioIdLogado))
            {
                return Results.Unauthorized();
            }

            // 2. Buscar o checkout
            var checkout = await db.checkout
                .Include(c => c.Itens)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (checkout == null)
                return Results.NotFound(new { message = "Checkout não encontrado." });

            // 3. Verificar se o checkout pertence ao usuário logado
            if (checkout.UsuarioId != usuarioIdLogado)
                return Results.StatusCode(403); // Forbidden

            // 4. Retornar
            return Results.Ok(checkout);
        }).WithTags("Checkout").RequireAuthorization();

    app.MapDelete("/checkout/{id:int}", async (AppDbContext db, int id) =>
        {
            var checkout = await db.checkout
                .Include(c => c.Itens)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (checkout == null)
                return Results.NotFound(new { message = "Checkout não encontrado." });

            db.checkout_item.RemoveRange(checkout.Itens);
            db.checkout.Remove(checkout);

            await db.SaveChangesAsync();

            return Results.NoContent();
    }).WithTags("Checkout").RequireAuthorization();


    app.MapGet("/api/dashboard/{usuarioId}", async (int usuarioId, AppDbContext db) =>
        {
            
            var totalProdutos = await db.produto
                .Where(p => p.UsuarioId == usuarioId)
                .CountAsync();

            
            var pagamentosUsuario = await db.Pagamento
                .Where(p => p.Status == "approved" && p.Produtos.Any(pp => pp.Produto.UsuarioId == usuarioId))
                .Include(p => p.Ordem)
                .ThenInclude(o => o.Itens)
                .ToListAsync();

            
            var totalFaturado = pagamentosUsuario
                .Sum(p => p.Ordem.Itens
                    .Where(i => db.produto.Any(prod => prod.Id == i.ProdutoId && prod.UsuarioId == usuarioId))
                    .Sum(i => i.PrecoUnitario * i.Qtd));

            
            var totalVendas = pagamentosUsuario.Count;

            
            var ultimoPedido = pagamentosUsuario
                .OrderByDescending(p => p.DataPagamento)
                .Select(p => new
                {
                    p.Ordem.Id,
                    Cliente = p.Ordem.UsuarioId,
                    p.DataPagamento,
                    Itens = p.Ordem.Itens.Count(i => db.produto.Any(prod => prod.Id == i.ProdutoId && prod.UsuarioId == usuarioId)),
                    ValorTotal = p.Ordem.Itens
                        .Where(i => db.produto.Any(prod => prod.Id == i.ProdutoId && prod.UsuarioId == usuarioId))
                        .Sum(i => i.PrecoUnitario * i.Qtd)
                })
                .FirstOrDefault();


            var pagamentosUsuario2 = await db.Pagamento
                .Where(p => p.Status == "approved" && p.Produtos.Any(pp => pp.Produto.UsuarioId == usuarioId))
                .Include(p => p.Ordem)
                    .ThenInclude(o => o.Itens)
                .Include(p => p.Produtos)          // Inclui os produtos
                    .ThenInclude(pp => pp.Produto)
                .ToListAsync();

            var produtoMaisVendido = pagamentosUsuario2
                .SelectMany(p => p.Produtos)
                .Where(pp => pp.Produto.UsuarioId == usuarioId)
                .GroupBy(pp => pp.ProdutoId)
                .Select(g => new
                {
                    Produto = g.First().Produto,        // pega o produto do primeiro item do grupo
                    QtdVendida = g.Sum(x => x.Qtd)
                })
                .OrderByDescending(x => x.QtdVendida)
                .Select(x => new
                {
                    x.Produto.Nome,
                    x.Produto.Img,
                    x.QtdVendida,
                    x.Produto.Valor,
                    ReceitaGerada = x.QtdVendida * x.Produto.Valor
                })
                .FirstOrDefault();

            return Results.Ok(new
            {
                TotalProdutos = totalProdutos,
                TotalFaturado = totalFaturado,
                TotalVendas = totalVendas,
                UltimoPedido = ultimoPedido,
                ProdutoMaisVendido = produtoMaisVendido
            });
        });


    }
}