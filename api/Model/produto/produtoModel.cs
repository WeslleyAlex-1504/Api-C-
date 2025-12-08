using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using api.Model.usuario;

public class ProdutoModel
{
    public int Id { get; set; }
    public string Nome { get; set; } 
    public string Descricao { get; set; } 
    public decimal Valor { get; set; }
    public string Img { get; set; } 
    public int UsuarioId { get; set; }
    [ForeignKey("UsuarioId")]
    public usuarioModel Usuario { get; set; } = null!;
    public decimal Desconto { get; set; } = 0;
    public int CategoriaId { get; set; }
    [ForeignKey("CategoriaId")]
    public CategoriaModel Categoria { get; set; } = null!;
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
    [Key]
    public int Id { get; set; }

    [Column("usuarioId")]
    public int UsuarioId { get; set; }

    [ForeignKey("UsuarioId")]
    public usuarioModel Usuario { get; set; } = null!;

    [Column("dataCriacao")]
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    [Column("ativo")]
    public bool Ativo { get; set; } = true;

    public List<CheckoutItemModel> Itens { get; set; } = new();
}

public class CheckoutItemModel
{
    [Key]
    public int Id { get; set; }

    [Column("checkoutId")]
    public int CheckoutId { get; set; }

    [ForeignKey("CheckoutId")]
    [JsonIgnore]
    public CheckoutModel Checkout { get; set; } = null!;

    [Column("produtoId")]
    public int ProdutoId { get; set; }

    [ForeignKey("ProdutoId")]
    public ProdutoModel Produto { get; set; } = null!;

    [Column("quantidade")]
    public int Quantidade { get; set; }
}

public class CriarCheckoutDTO
{
    public int UsuarioId { get; set; }
    public List<ItemDTO> Itens { get; set; } = new();
}

public class ItemDTO
{
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
}