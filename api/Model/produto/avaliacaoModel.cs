namespace api.Model.avaliacao
{
    public class AvaliacaoModel
    {
        public int Id { get; set; }
        public float Numero { get; set; }
        public int ProdutoId { get; set; }
        public bool? Ativo { get; set; } = true;
    }

    public class AvaliacaoPatchDados
    {
        public int? Id { get; set; }
        public int? Numero { get; set; }
        public int? ProdutoId { get; set; }
        public bool? Ativo { get; set; }
    }
}