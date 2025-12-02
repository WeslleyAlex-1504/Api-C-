using System.Globalization;
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
using MercadoPago.Client.Common;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Preference;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using api.Hubs;

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

        app.MapPost("/pagamento", async (AppDbContext db, CriarPagamentoDTO dto, IHubContext < MyHub > hub) =>
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

            total += dto.Frete;

            ordem.Total = total;
            await db.SaveChangesAsync();

            ordem = await db.Ordem
                .Include(o => o.Itens)
                .FirstOrDefaultAsync(o => o.Id == ordem.Id);

            await hub.Clients.All.SendAsync("ReceiveMessage", $"Ordem {ordem.Id} criada com total {ordem.Total:C}");

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

            // 4. 🔥 MERCADO PAGO - PAYMENTS API
            MercadoPagoConfig.AccessToken = "APP_USR-967472367753134-112716-d2979df8a36ce96b8337a5898b0cbf91-3021463594";

            var paymentRequest = new PaymentCreateRequest
            {
                TransactionAmount = decimal.Parse(total.ToString("0.00", CultureInfo.InvariantCulture)),
                Description = $"Pagamento ordem {ordem.Id}",
                PaymentMethodId = dto.Metodo, // "pix", "credit_card", "ticket"
                ExternalReference = pagamento.Id.ToString(),
                NotificationUrl = "https://api-c-atha.onrender.com/webhook/mp",

                // OBRIGATÓRIO PARA TODOS
                Payer = new PaymentPayerRequest
                {
                    Email = dto.Email,
                    FirstName = dto.Nome
                }
            };

            // PIX não precisa mais nada
            // Cartão requer token + payer info
            if (dto.Metodo == "credit_card")
            {
                paymentRequest.Token = dto.TokenCartao;

                if (!string.IsNullOrEmpty(dto.Bandeira))
                    paymentRequest.PaymentMethodId = dto.Bandeira;

                // installments OBRIGATÓRIO (mesmo 1)
                paymentRequest.Installments = dto.Parcela > 0 ? dto.Parcela : 1;

                if (!string.IsNullOrEmpty(dto.IssuerId))
                    paymentRequest.IssuerId = dto.IssuerId;

                // payer e identification
                paymentRequest.Payer = new PaymentPayerRequest
                {
                    Email = dto.Email,
                    FirstName = dto.Nome,
                    Identification = new IdentificationRequest
                    {
                        Type = "CPF",
                        Number = dto.Cpf?.Replace(".", "").Replace("-", "") // só números
                    }
                };
            }

            // Boleto (ticket)
            if (dto.Metodo == "bolbradesco")
            {
                int numeroRua = dto.Numero;

                paymentRequest.Payer = new PaymentPayerRequest
                {
                    Email = dto.Email,
                    FirstName = dto.Nome,
                    LastName = dto.Sobrenome,
                    Identification = new IdentificationRequest
                    {
                        Type = "CPF",
                        Number = dto.Cpf
                    },
                    Address = new PaymentPayerAddressRequest
                    {
                        ZipCode = dto.Cep,
                        StreetName = dto.Rua,
                        StreetNumber = dto.Numero,
                        Neighborhood = dto.Bairro,
                        City = dto.Cidade,
                        FederalUnit = dto.Estado
                    }
                };
            }
            var client = new PaymentClient();
            var payment = await client.CreateAsync(paymentRequest);

            return Results.Created($"/pagamento/{pagamento.Id}", new
            {
                ordem,
                pagamento,
                mp_payment_id = payment.Id,
                status = payment.Status,
                metodo = dto.Metodo,
                // PIX retorna QR CODE
                qr_code = payment.PointOfInteraction?.TransactionData?.QrCode,
                qr_base64 = payment.PointOfInteraction?.TransactionData?.QrCodeBase64,
                boleto = payment.TransactionDetails?.ExternalResourceUrl
            });

        }).WithTags("MercadoPago").RequireAuthorization();

        app.MapPost("/webhook/mp", async (HttpRequest request, AppDbContext db, IHubContext<MyHub> hub) =>
        {
            MercadoPagoConfig.AccessToken = "APP_USR-967472367753134-112716-d2979df8a36ce96b8337a5898b0cbf91-3021463594";

            using var reader = new StreamReader(request.Body);
            string body = await reader.ReadToEndAsync();
            var json = JsonDocument.Parse(body).RootElement;

            string? paymentId = null;

            // Obter paymentId do webhook
            if (json.TryGetProperty("data.id", out var id1))
                paymentId = id1.GetString();

            if (json.TryGetProperty("id", out var id2))
                paymentId = id2.GetInt64().ToString();

            if (paymentId == null)
                return Results.Ok(); // nada a fazer

            // Buscar pagamento no MercadoPago
            var paymentClient = new MercadoPago.Client.Payment.PaymentClient();
            var mpPayment = await paymentClient.GetAsync(long.Parse(paymentId));

            // Buscar pagamento local via ExternalReference
            if (!int.TryParse(mpPayment.ExternalReference, out int pagamentoId))
                return Results.Ok();

            var pagamento = await db.Pagamento.Include(p => p.Ordem).FirstOrDefaultAsync(p => p.Id == pagamentoId);
            if (pagamento == null)
                return Results.Ok();

            // Atualizar status local
            pagamento.Status = mpPayment.Status;
            if (mpPayment.Status == "approved")
            {
                pagamento.Ordem.Status = "finalizada";
                pagamento.Ordem.DataFinalizacao = DateTime.Now;
                pagamento.DataPagamento = DateTime.Now;
            }

            await db.SaveChangesAsync();

            // Enviar status via SignalR para todos conectados
            await hub.Clients.All.SendAsync("ReceiveMessage", new
            {
                PagamentoId = pagamento.Id,
                OrdemId = pagamento.OrdemId,
                Status = pagamento.Status,
                Metodo = mpPayment.PaymentMethodId
            });

            return Results.Ok();
        });

        app.MapGet("/metodos-mp", async () =>
        {
            try
            {
                // Use seu token de TESTE ou PRODUÇÃO
                MercadoPagoConfig.AccessToken = "APP_USR-967472367753134-112716-d2979df8a36ce96b8337a5898b0cbf91-3021463594";

                var client = new MercadoPago.Client.PaymentMethod.PaymentMethodClient();
                var methods = await client.ListAsync();

                // Retornar somente o essencial
                var result = methods.Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    payment_type = m.PaymentTypeId,
                    status = m.Status
                });

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Erro ao consultar métodos: {ex.Message}");
            }
        });
        app.MapPost("/gerar-token-cartao", async (CartaoDTO cartao, HttpClient httpClient) =>
        {
            try
            {
                var payload = new
                {
                    card_number = cartao.CardNumber,
                    security_code = cartao.Cvv,
                    expiration_month = cartao.ExpMonth,
                    expiration_year = cartao.ExpYear,
                    cardholder = new
                    {
                        name = cartao.CardholderName,
                        identification = new
                        {
                            type = "CPF",
                            number = cartao.Cpf
                        }
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/v1/card_tokens");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "TEST-967472367753134-112716-5d3416bdeb66cea697a186ab484a68fd-3021463594");
                request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return Results.Ok(json); // Retorna o JSON do token
            }
            catch (Exception ex)
            {
                return Results.Problem($"Erro ao gerar token do cartão: {ex.Message}");
            }
        }).WithTags("MercadoPago").RequireAuthorization();
    }
}