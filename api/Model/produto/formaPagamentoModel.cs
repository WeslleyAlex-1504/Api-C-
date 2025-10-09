public class FormaPagamentoModel
{
    public int? Id { get; set; }
    public string Nome { get; set; } 
    public bool? Ativo { get; set; } = true;
}

public class FormaPagamentoPatchDados
{
    public string? Nome { get; set; }
    public bool? Ativo { get; set; }
}