using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DampCode_API.Dto
{
    public class CompanyDto
    {
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }

        //Empresa
        public string? Cnpj { get; set; }
        public string? Descricao { get; set; }
        public string? Area { get; set; }
        public bool? Verificado { get; set; }
        public List<string>? HackatonsIds { get; set; }
    }
}
