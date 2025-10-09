namespace api.Model.carrinho;
public class CarrinhoItemModel
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public int Qtd { get; set; }
    public int UsuarioId { get; set; }
    public bool? Ativo { get; set; } = true;
}

public class CarrinhoItemPatchDados
{
    public int? CarrinhoId { get; set; }
    public int? ProdutoId { get; set; }
    public int? Qtd { get; set; }
    public int? UsuarioId { get; set; }
    public bool? Ativo { get; set; }
}