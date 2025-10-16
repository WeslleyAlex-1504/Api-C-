namespace api.Model.usuario
{
    public class usuarioModel
    {
        public int Id { get; set; }

        public string Nome { get; set; }

        public string Email { get; set; }

        public string Senha { get; set; }

        public string Telefone { get; set; }

        public string Cpf { get; set; }

        public int Idade { get; set; }

        public bool Admin { get; set; } = false;

        public bool Ativo { get; set; } = true;
    }

    public class UsuarioImagem
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Imagem { get; set; } = string.Empty;
    }
}
