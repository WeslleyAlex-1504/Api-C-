using api.Model.usuario;

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
    public string Estado { get; set; }  
    public string Cep { get; set; }
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
    public string? Estado { get; set; } 
    public string? Cep { get; set; }
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
    public string Estado { get; set; }
    public string Cep { get; set; }
}

public class ProdutoImagem
{
    public int Id { get; set; } 
    public int ProdutoId { get; set; }    
    public string Imagem { get; set; } = string.Empty; 
}

public class CheckoutModel
{
    public int id { get; set; }

    public int usuarioId { get; set; }
    public usuarioModel Usuario { get; set; }

    public DateTime dataCriacao { get; set; } = DateTime.Now;

    public bool ativo { get; set; } = true;

    public List<CheckoutItemModel> Itens { get; set; }
}

public class CheckoutItemModel
{
    public int Id { get; set; }

    public int CheckoutId { get; set; }
    public CheckoutModel Checkout { get; set; }

    public int ProdutoId { get; set; }
    public ProdutoModel Produto { get; set; }

    public int Quantidade { get; set; }
}

public class CriarCheckoutDTO
{
    public int UsuarioId { get; set; }

    public List<ItemDTO> Itens { get; set; }
}

public class ItemDTO
{
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
}