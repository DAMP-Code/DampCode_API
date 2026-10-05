using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DampCode_API.Dto
{
    public class ParticipantDto
    {
        public  string? Name { get; set; }
        public  string? Email { get; set; }
        public  string? Password { get; set; }
        public string? Role { get; set; }

        // participante
        public int? Nivel { get; set; }
        public decimal? Xp { get; set; }
        public List<string>? Tecnologias { get; set; }
    }
}
