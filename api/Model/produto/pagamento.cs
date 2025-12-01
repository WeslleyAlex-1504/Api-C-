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
    public string Metodo { get; set; }   // "pix", "credit_card", "bolbradesco"
    public string Email { get; set; }
    public string Nome { get; set; }
    public string Sobrenome { get; set; }
    public string Cpf { get; set; }
    public int Parcela { get; set; }
    public string IssuerId { get; set; }
    public string Bandeira { get; set; }
    public string Cep { get; set; }
    // Street number como int para evitar o erro de conversão
    public int Numero { get; set; }
    public string Rua { get; set; }
    public string Bairro { get; set; }
    public string Cidade { get; set; }
    public string Estado { get; set; }   // adicionado

    public string TokenCartao { get; set; } // caso cartão
    public List<CriarPagamentoProdutoDTO> Produtos { get; set; } = new();
    public decimal Frete { get; set; }
}
public class CriarPagamentoProdutoDTO
{
    public int ProdutoId { get; set; }
    public int Qtd { get; set; }
}

public class CartaoDTO
{
    public string CardNumber { get; set; }
    public string Cvv { get; set; }
    public string ExpMonth { get; set; }
    public string ExpYear { get; set; }
    public string CardholderName { get; set; }
    public string Cpf { get; set; }
}w

