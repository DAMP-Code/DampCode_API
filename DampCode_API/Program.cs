using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Preserva os nomes públicos dos schemas após renomear as classes internas.
    options.CustomSchemaIds(type => type == typeof(ParticipantDto) ? "ParticipanteDto"
        : type == typeof(CompanyDto) ? "EmpresaDto"
        : type == typeof(CreateHackathonDto) ? "CreateHackathonDTO"
        : type.Name);
});
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<TokenService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact",
        policy => policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseCors("AllowReact"); 

app.Run();
