public class Pagamento
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FPagamentoId { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataPagamento { get; set; }
    public bool Ativo { get; set; }
    public List<PagamentoProduto> Produtos { get; set; } = new();
}

public class PagamentoProduto
{
    public int Id { get; set; }
    public int pagamentoId { get; set; }
    public int ProdutoId { get; set; }
    public int Qtd { get; set; }
}


public class PagamentoPatch
{
    public string? Status { get; set; } 
}