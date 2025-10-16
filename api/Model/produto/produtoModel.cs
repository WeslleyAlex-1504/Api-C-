public class ProdutoModel
{
    public int Id { get; set; }
    public string Nome { get; set; } 
    public string Descricao { get; set; } 
    public decimal Valor { get; set; }
    public string Img { get; set; } 
    public int UsuarioId { get; set; }
    public decimal Desconto { get; set; } = 0;
    public int CategoriaId { get; set; }
    public bool Ativo { get; set; } = true;
}

public class ProdutoPatchDados
{
    public string? Nome { get; set; }
    public string? Descricao { get; set; }
    public decimal? Valor { get; set; }
    public string? Img { get; set; }
    public decimal? Desconto { get; set; }
    public int? CategoriaId { get; set; }
    public bool? Ativo { get; set; }
}

public class ProdutoCreateDados
{
    public string Nome { get; set; } 
    public string Descricao { get; set; } 
    public decimal Valor { get; set; }
    public string cpf { get; set; }
    public decimal? Desconto { get; set; } = 0;
    public int CategoriaId { get; set; }
    public bool? Ativo { get; set; } = true;
    public IFormFile? Img { get; set; } 
}

public class ProdutoImagem
{
    public int Id { get; set; } 
    public int ProdutoId { get; set; }    
    public string Imagem { get; set; } = string.Empty; 
}