public class CategoriaModel
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public bool Ativo { get; set; } = true;
}

public class CategoriaPatchDados
{
    public string? Nome { get; set; }
    public bool? Ativo { get; set; }
}