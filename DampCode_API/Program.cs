using DampCode_API.Data;
using DampCode_API.Dto;
using DampCode_API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type == typeof(ParticipantDto) ? "ParticipanteDto"
        : type == typeof(CompanyDto) ? "EmpresaDto"
        : type == typeof(CreateHackathonDto) ? "CreateHackathonDTO"
        : type.Name);
});
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddDbContext<DampCodeDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("PostgreSql");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Configure a connection string 'PostgreSql' por User Secrets ou pela variável " +
            "ConnectionStrings__PostgreSql.");
    }
    options.UseNpgsql(connectionString);
});
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowReact");
app.UseAuthorization();
app.MapControllers();
app.Run();
