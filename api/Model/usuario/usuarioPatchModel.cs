namespace api.Model.usuario
{
    public class usuarioPatchModel
    {
        public string? Nome { get; set; }
        public string? Email { get; set; }
        public string? Senha { get; set; }
        public string? Telefone { get; set; }
        public string? Cpf { get; set; }
        public int? Idade { get; set; }
        public bool? Ativo { get; set; }
        public bool? Admin { get; set; }
    }
}
