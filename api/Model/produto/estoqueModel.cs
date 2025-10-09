namespace api.Model.produto
{
    public class EstoqueModel
    {
        public int Id { get; set; }

        public int QtdEstoque { get; set; }

        public int ProdutoId { get; set; }

        public bool Ativo { get; set; } = true;
    }

    public class EstoquePatchDados
    {
        public int? QtdEstoque { get; set; }

        public int? ProdutoId { get; set; }

        public bool? Ativo { get; set; }
    }
}
