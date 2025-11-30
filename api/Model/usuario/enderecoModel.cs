namespace api.Model.usuario
{
    public class EnderecoModel
    {
        public int Id { get; set; }

        public string Cidade { get; set; } 

        public string Estado { get; set; } 

        public string Pais { get; set; } 

        public string Rua { get; set; }
        public string Bairro { get; set;}

        public int Numero { get; set; }

        public int UsuarioId { get; set; }

        public string Cep { get; set; }

        public bool Ativo { get; set; } = true;
    }

    public class EnderecoCreateDados
    {
        public string Cep { get; set; } 
        public int Numero { get; set; }
        public int UsuarioId { get; set; }
    }

    public class EnderecoPatchDados
    {
        public string? Cep { get; set; }
        public int? Numero { get; set; }
        public bool? Ativo { get; set; }
    }

    public class EnderecoPrincipal
    {
        public int Id { get; set; }

        public int UsuarioId { get; set; }

        public int EnderecoId { get; set; }

        public EnderecoModel Endereco { get; set; }
    }

    public class EnderecoPrincipalCreate
    {
        public int UsuarioId { get; set; }
        public int EnderecoId { get; set; }
    }

    public class EnderecoPrincipalPatch
    {
        public int? UsuarioId { get; set; }
        public int? EnderecoId { get; set; }
    }
}