using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DampCode_API.Models
{
    [BsonIgnoreExtraElements]
    public class Hackathon
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? HackathonId { get; set; }

        [BsonElement("titulo")]
        public string? Titulo { get; set; }

        [BsonElement("descricao")]
        public string? Descricao { get; set; }

        [BsonElement("empresa")]
        public string? Empresa { get; set; }

        [BsonElement("area")]
        public string? Area { get; set; }

        [BsonElement("tecnologias")]
        public List<string>? Tecnologias { get; set; }

        [BsonElement("metodo")]
        public string? Metodo { get; set; }

        [BsonElement("ranking")]
        public string? Ranking { get; set; }

        [BsonElement("premiacao")]
        public decimal Premiacao { get; set; }

        [BsonElement("corPrincipal")]
        public string? corPrincipal { get; set; }

        [BsonElement("corSecundaria")]
        public string? corSecundaria { get; set; }

        [BsonElement("corFundo")]
        public string? corFundo { get; set; }

        [BsonElement("Logo")]
        public string? Logo { get; set; }

        [BsonElement("DataCriacao")]
        public DateTime DataCriacao { get; set; }

        [BsonElement("DataFinal")]
        public DateTime DataFinal { get; set; }

        [BsonElement("status")]
        public bool status { get; set; }
    }
}