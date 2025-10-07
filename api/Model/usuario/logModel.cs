namespace api.Model.usuario
{
    public class logModel
    {
        public class ControleLogModel
        {
            public int Id { get; set; }

            public int UsuarioId { get; set; }

            public DateTime LoginEm { get; set; }

            public bool Ativo { get; set; } = true;

        }

    }
}
