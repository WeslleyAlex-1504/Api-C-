namespace api.Model.usuario
{
    public class EnderecoModel
    {
        public int Id { get; set; }

        public string Cidade { get; set; } 

        public string Estado { get; set; } 

        public string Pais { get; set; } 

        public string Rua { get; set; }

        public int Numero { get; set; }

        public int UsuarioId { get; set; }

        public string Cep { get; set; }

        public bool Ativo { get; set; } = true;
    }

    public class EnderecoCreateDados
    {
        public string Cep { get; set; } 
        public int Numero { get; set; }
        public string UsuarioCpf { get; set; }
    }

    public class EnderecoPatchDados
    {
        public string? Cep { get; set; }
        public int? Numero { get; set; }
        public bool? Ativo { get; set; }
    }
}