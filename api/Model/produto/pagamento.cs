public class Ordem
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "pendente";
    public DateTime DataCriacao { get; set; }
    public DateTime? DataFinalizacao { get; set; }


    public List<OrdemItem> Itens { get; set; } = new();
}


public class OrdemItem
{
    public int Id { get; set; }
    public int OrdemId { get; set; }
    public int ProdutoId { get; set; }
    public int Qtd { get; set; }
    public decimal PrecoUnitario { get; set; }
}


public class Pagamento
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FPagamentoId { get; set; }
    public int OrdemId { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataPagamento { get; set; }
    public bool Ativo { get; set; }


    public List<PagamentoProduto> Produtos { get; set; } = new();
}


public class PagamentoProduto
{
    public int Id { get; set; }
    public int PagamentoId { get; set; }
    public int ProdutoId { get; set; }
    public int Qtd { get; set; }
}


public class PagamentoPatch
{
    public string? Status { get; set; }
}

public class CriarPagamentoDTO
{
    public int UsuarioId { get; set; }
    public List<CriarPagamentoProdutoDTO> Produtos { get; set; } = new();

    // PIX | CARTAO | BOLETO
    public string Metodo { get; set; }

    // Usado nos 3 métodos
    public string Email { get; set; }

    // Usado no cartão
    public string? TokenCartao { get; set; }  // criado no frontend pelo Mercado Pago
    public int? Parcelas { get; set; }

    // Usado no boleto
    public string? Nome { get; set; }
    public string? Sobrenome { get; set; }
    public string? Cpf { get; set; }
}

public class CriarPagamentoProdutoDTO
{
    public int ProdutoId { get; set; }
    public int Qtd { get; set; }
}