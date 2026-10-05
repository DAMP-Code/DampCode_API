using MongoDB.Driver; // Importa o driver do MongoDB para C#

namespace DampCode_API.Data
{
    public class MongoDbService
    {
        // Permite acessar o appsettings.json (configurações da aplicação)
        private readonly IConfiguration _configuration;

        // Representa o banco de dados MongoDB (ex: "damp_code")
        private readonly IMongoDatabase? _database;

        // Construtor da classe (é chamado quando o serviço é criado)
        public MongoDbService(IConfiguration configuration)
        {
            // Recebe a configuração do ASP.NET e guarda na variável
            _configuration = configuration;

            // Pega a connection string do appsettings.json
            // Ex: "mongodb://localhost:27017/damp_code"
            var connectionString = _configuration.GetConnectionString("DbConnection");

            // Converte a string em um objeto MongoUrl
            // (permite acessar partes como DatabaseName, Host, etc)
            var mongoUrl = MongoUrl.Create(connectionString);

            // Cria o cliente do MongoDB (abre a conexão com o servidor)
            var mongoClient = new MongoClient(mongoUrl);

            // Acessa o banco de dados usando o nome da connection string (ex: "damp_code")
            _database = mongoClient.GetDatabase(mongoUrl.DatabaseName);
        }

        // Propriedade pública para permitir acesso ao banco
        // Outras classes (ex: Controllers) usam isso para acessar collections
        public IMongoDatabase? Database => _database;
    }
}