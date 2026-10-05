namespace DampCode_API.Dto
{
    public class CreateHackathonDto
    {
        public string? Titulo { get; set; }

        public string? Descricao { get; set; }

        public string? Empresa { get; set; }

        public string? Area { get; set; }

        public List<string>? Tecnologias { get; set; }

        public string? Metodo { get; set; }

        public string? Ranking { get; set; }

        public decimal Premiacao { get; set; }

        public string? corPrincipal { get; set; }

        public string? corSecundaria { get; set; }

        public string? corFundo { get; set; }

        public string? Logo { get; set; }

        public DateTime DataCriacao { get; set; }

        public DateTime DataFinal { get; set; }

        public bool status { get; set; }
    }
}