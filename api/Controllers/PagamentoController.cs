using System.IdentityModel.Tokens.Jwt;
using System.Runtime.ConstrainedExecution;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using api.DbContext;
using api.Model.carrinho;
using api.Model.produto;
using api.Model.usuario;
using api.Model.ViaCep;
using Carter;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Preference;
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

        app.MapPost("/pagamento", async (AppDbContext db, CriarPagamentoDTO dto) =>
        {
            // 1. Criar ORDEM
            var ordem = new Ordem
            {
                UsuarioId = dto.UsuarioId,
                DataCriacao = DateTime.Now,
                Status = "pendente"
            };

            db.Ordem.Add(ordem);
            await db.SaveChangesAsync();

            decimal total = 0;

            // 2. Criar itens da ORDEM
            foreach (var item in dto.Produtos)
            {
                var produto = await db.produto.FirstOrDefaultAsync(p => p.Id == item.ProdutoId);

                if (produto == null)
                    return Results.BadRequest($"Produto {item.ProdutoId} não encontrado");

                db.OrdemItem.Add(new OrdemItem
                {
                    OrdemId = ordem.Id,
                    ProdutoId = produto.Id,
                    Qtd = item.Qtd,
                    PrecoUnitario = produto.Valor
                });

                total += produto.Valor * item.Qtd;
            }

            ordem.Total = total;
            await db.SaveChangesAsync();

            ordem = await db.Ordem
                .Include(o => o.Itens)
                .FirstOrDefaultAsync(o => o.Id == ordem.Id);

            // 3. Criar PAGAMENTO local
            var pagamento = new Pagamento
            {
                OrdemId = ordem.Id,
                Status = "pendente",
                FPagamentoId = 1,
                DataCriacao = DateTime.Now,
                Ativo = true
            };

            db.Pagamento.Add(pagamento);
            await db.SaveChangesAsync();

            // 4. Criar itens do PAGAMENTO local
            foreach (var item in dto.Produtos)
            {
                db.PagamentoProduto.Add(new PagamentoProduto
                {
                    PagamentoId = pagamento.Id,
                    ProdutoId = item.ProdutoId,
                    Qtd = item.Qtd
                });
            }

            await db.SaveChangesAsync();

            // 5. 🔥 CRIAR PAGAMENTO NO MERCADO PAGO
            MercadoPagoConfig.AccessToken = _config["MP_TOKEN"];

            var preference = new PreferenceRequest
            {
                Items = ordem.Itens.Select(i => new PreferenceItemRequest
                {
                    Title = $"Produto {i.ProdutoId}",
                    Quantity = i.Qtd,
                    UnitPrice = i.PrecoUnitario
                }).ToList(),

                // Quando MP confirmar, vai chamar seu webhook
                NotificationUrl = "https://api-c-atha.onrender.com/webhook/mp",

                // Muito importante para associar o pagamento
                ExternalReference = pagamento.Id.ToString()
            };

            var client = new PreferenceClient();
            var mpResult = await client.CreateAsync(preference);

            // 🔗 mpResult.InitPoint = URL para pagar
            // 🔗 mpResult.Id = ID da preference

            return Results.Created($"/pagamento/{pagamento.Id}", new
            {
                ordem,
                pagamento,
                mp_url = mpResult.InitPoint,
                mp_preference_id = mpResult.Id
            });

        }).WithTags("MercadoPago").RequireAuthorization();

        app.MapPost("/webhook/mp", async (HttpRequest request, AppDbContext db) =>
        {
            MercadoPagoConfig.AccessToken = Environment.GetEnvironmentVariable("MP_TOKEN");

            using var reader = new StreamReader(request.Body);
            string body = await reader.ReadToEndAsync();

            var json = JsonDocument.Parse(body).RootElement;

            string? paymentId = null;

            // Caso 1 - MercadoPago envia notification_id
            if (json.TryGetProperty("data.id", out var id1))
                paymentId = id1.GetString();

            // Caso 2 - versão nova
            if (json.TryGetProperty("id", out var id2))
                paymentId = id2.GetInt64().ToString();

            if (paymentId == null)
                return Results.Ok();

            var paymentClient = new MercadoPago.Client.Payment.PaymentClient();
            var mpPayment = await paymentClient.GetAsync(long.Parse(paymentId));

            int pagamentoId = int.Parse(mpPayment.ExternalReference);

            var pagamento = await db.Pagamento.FirstOrDefaultAsync(p => p.Id == pagamentoId);
            var ordem = await db.Ordem.FirstOrDefaultAsync(o => o.Id == pagamento.OrdemId);

            pagamento.Status = mpPayment.Status;
            if (mpPayment.Status == "approved")
            {
                ordem.Status = "finalizada";
                ordem.DataFinalizacao = DateTime.Now;
                pagamento.DataPagamento = DateTime.Now;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new { ok = true });
        });
    }
}