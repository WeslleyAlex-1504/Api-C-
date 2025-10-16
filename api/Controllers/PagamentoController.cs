using System.IdentityModel.Tokens.Jwt;
using System.Runtime.ConstrainedExecution;
using System.Security.Claims;
using System.Text;
using api.DbContext;
using api.Model.carrinho;
using api.Model.produto;
using api.Model.usuario;
using api.Model.ViaCep;
using Carter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public class PagamentoModule : CarterModule
{

    private readonly IConfiguration _config;
    private readonly ViaCepService _viaCepService;

    public PagamentoModule(IConfiguration config, ViaCepService viaCepService)
    {
        _config = config;
        _viaCepService = viaCepService;
    }
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/itemCarrinho", async (AppDbContext db, CarrinhoItemModel item) =>
        {
            var usuario = await db.usuario.FirstOrDefaultAsync(u => u.Id == item.UsuarioId);
            if (usuario == null)
                return Results.NotFound(new { message = "Usuário não encontrado." });

            var produto = await db.produto.FirstOrDefaultAsync(p => p.Id == item.ProdutoId);
            if (produto == null)
                return Results.NotFound(new { message = "Produto não encontrado." });

            var estoque = await db.estoque.FirstOrDefaultAsync(e => e.ProdutoId == item.ProdutoId);
            if (estoque == null || estoque.QtdEstoque <= 0)
                return Results.BadRequest(new { message = "Produto sem estoque disponível." });

            bool existe = await db.itemCarrinho.AnyAsync(ci =>
                ci.UsuarioId == item.UsuarioId && ci.ProdutoId == item.ProdutoId);
            if (existe)
                return Results.BadRequest(new { message = "Este produto já está no carrinho." });

            if (item.Qtd > estoque.QtdEstoque)
                return Results.BadRequest(new { message = $"Quantidade solicitada maior que o estoque disponível ({estoque.QtdEstoque})." });

            var novoItem = new CarrinhoItemModel
            {
                ProdutoId = item.ProdutoId,
                Qtd = item.Qtd,
                UsuarioId = item.UsuarioId
            };

            db.itemCarrinho.Add(novoItem);

            estoque.QtdEstoque -= item.Qtd;
            db.estoque.Update(estoque);

            await db.SaveChangesAsync();

            return Results.Created($"/itemCarrinho", new
            {
                message = "Item adicionado ao carrinho com sucesso.",
                novoItem,
                estoqueAtual = estoque.QtdEstoque
            });

        }).WithTags("Carrinho").RequireAuthorization();

        app.MapGet("/itemCarrinho", async (AppDbContext db, int usuarioId) =>
        {
            var itens = await db.itemCarrinho
                .Where(ci => ci.UsuarioId == usuarioId)
                .ToListAsync();

            if (itens.Count == 0)
                return Results.NotFound(new { message = "Nenhum item encontrado para este usuário." });

            return Results.Ok(itens);
        }).WithTags("Carrinho").RequireAuthorization();

        app.MapDelete("/itemCarrinho/{id}", async (AppDbContext db, int id) =>
        {
            var item = await db.itemCarrinho.FirstOrDefaultAsync(ci => ci.Id == id);
            if (item == null)
                return Results.NotFound(new { message = "Item do carrinho não encontrado." });

            var estoque = await db.estoque.FirstOrDefaultAsync(e => e.ProdutoId == item.ProdutoId);
            if (estoque != null)
            {
                estoque.QtdEstoque += item.Qtd;
                db.estoque.Update(estoque);
            }

            db.itemCarrinho.Remove(item);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Item removido do carrinho com sucesso." });
        }).WithTags("Carrinho").RequireAuthorization();

        app.MapPatch("/itemCarrinho/{id}", async (AppDbContext db, int id, CarrinhoItemPatchDados patch) =>
        {
            var item = await db.itemCarrinho.FirstOrDefaultAsync(ci => ci.Id == id);
            if (item == null)
                return Results.NotFound(new { message = "Item do carrinho não encontrado." });

            var estoque = await db.estoque.FirstOrDefaultAsync(e => e.ProdutoId == item.ProdutoId);
            if (estoque == null)
                return Results.BadRequest(new { message = "Produto sem estoque disponível." });

            if (patch.Qtd.HasValue)
            {
                var diferenca = patch.Qtd.Value - item.Qtd;

                if (diferenca > 0 && diferenca > estoque.QtdEstoque)
                    return Results.BadRequest(new { message = $"Quantidade solicitada maior que o estoque disponível ({estoque.QtdEstoque})." });

                item.Qtd = patch.Qtd.Value;
                estoque.QtdEstoque -= diferenca;
                db.estoque.Update(estoque);
            }

            if (patch.Ativo.HasValue)
                item.Ativo = patch.Ativo.Value;

            db.itemCarrinho.Update(item);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Item do carrinho atualizado com sucesso.",
                item,
                estoqueAtual = estoque.QtdEstoque
            });
        }).WithTags("Carrinho").RequireAuthorization();

        app.MapPost("/formaPagamento", async (FormaPagamentoModel dados, AppDbContext db) =>
        {
            bool existe = await db.formaPagamento.AnyAsync(f => f.Nome == dados.Nome);
            if (existe)
                return Results.BadRequest(new { message = "Já existe uma forma de pagamento com este nome." });

            db.formaPagamento.Add(dados);
            await db.SaveChangesAsync();

            return Results.Created($"/formaPagamento/{dados.Id}", dados);
        }).WithTags("FormaPagamento").RequireAuthorization();

        app.MapGet("/formaPagamento", async (AppDbContext db, string? nome) =>
        {
            IQueryable<FormaPagamentoModel> query = db.formaPagamento;

            if (!string.IsNullOrWhiteSpace(nome))
                query = query.Where(f => f.Nome.Contains(nome));

            var formas = await query.ToListAsync();

            if (formas.Count == 0)
                return Results.NotFound(new { message = "Nenhuma forma de pagamento encontrada." });

            return Results.Ok(formas);
        }).WithTags("FormaPagamento").RequireAuthorization();

        app.MapDelete("/formaPagamento/{id}", async (int id, AppDbContext db) =>
        {
            var forma = await db.formaPagamento.FirstOrDefaultAsync(f => f.Id == id);
            if (forma == null)
                return Results.NotFound(new { message = "Forma de pagamento não encontrada." });

            db.formaPagamento.Remove(forma);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Forma de pagamento excluída com sucesso." });
        }).WithTags("FormaPagamento").RequireAuthorization();

        app.MapPatch("/formaPagamento/{id}", async (int id, FormaPagamentoPatchDados dados, AppDbContext db) =>
        {
            var forma = await db.formaPagamento.FirstOrDefaultAsync(f => f.Id == id);
            if (forma == null)
                return Results.NotFound(new { message = "Forma de pagamento não encontrada." });

            if (!string.IsNullOrEmpty(dados.Nome))
                forma.Nome = dados.Nome;

            if (dados.Ativo.HasValue)
                forma.Ativo = dados.Ativo.Value;

            db.formaPagamento.Update(forma);
            await db.SaveChangesAsync();

            return Results.Ok(forma);
        }).WithTags("FormaPagamento").RequireAuthorization();

        app.MapPost("/pagamento", async (AppDbContext db, Pagamento pagamento) =>
        {
            var novoPagamento = new Pagamento
            {
                Status = pagamento.Status,
                FPagamentoId = pagamento.FPagamentoId,
                Ativo = pagamento.Ativo,
                DataCriacao = DateTime.Now,
                DataPagamento = null
            };

            db.pagamento.Add(novoPagamento);
            await db.SaveChangesAsync();

            foreach (var prod in pagamento.Produtos)
            {
                var pagamentoProduto = new PagamentoProduto
                {
                    pagamentoId = novoPagamento.Id,
                    ProdutoId = prod.ProdutoId,
                    Qtd = prod.Qtd
                };
                db.pagamentoProduto.Add(pagamentoProduto);
            }

            await db.SaveChangesAsync();

            return Results.Created($"/pagamento/{novoPagamento.Id}", new
            {
                message = "Pagamento criado com sucesso.",
                pagamento = novoPagamento
            });
        }).WithTags("Pagamento").RequireAuthorization();

        app.MapGet("/pagamento", async (AppDbContext db, int? usuarioId) =>
        {
            IQueryable<Pagamento> query = db.pagamento.Include(p => p.Produtos);

            if (usuarioId.HasValue)
            {
                query = query.Where(p => p.FPagamentoId == usuarioId.Value);
            }

            var pagamentos = await query.ToListAsync();

            if (pagamentos.Count == 0)
                return Results.NotFound(new { message = "Nenhum pagamento encontrado." });

            return Results.Ok(pagamentos);
        }).WithTags("Pagamento").RequireAuthorization();

        app.MapPatch("/pagamento/{id}", async (AppDbContext db, int id, PagamentoPatch pagamentoDados) =>
        {
            var pagamento = await db.pagamento.Include(p => p.Produtos).FirstOrDefaultAsync(p => p.Id == id);

            if (pagamento == null)
                return Results.NotFound(new { message = "Pagamento não encontrado." });


            if (!string.IsNullOrWhiteSpace(pagamentoDados.Status))
            {
                pagamento.Status = pagamentoDados.Status;

                pagamento.DataPagamento = DateTime.Now;
            }

            db.pagamento.Update(pagamento);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                message = "Pagamento atualizado com sucesso.",
                pagamento
            });
        }).WithTags("Pagamento").RequireAuthorization();

    }
}