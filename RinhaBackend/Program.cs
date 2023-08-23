using Microsoft.AspNetCore.Mvc;
using RinhaBackend.Models;
using RinhaBackend.Repositories;
using RinhaBackend.Workers;
using StackExchange.Redis;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace RinhaBackend
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            //builder.Services.AddAuthorization();
            builder.Services.AddNpgsqlDataSource(builder.Configuration["DB_CONNECTION_STRING"]);
            builder.Services.AddSingleton(s => ConnectionMultiplexer.Connect(builder.Configuration["CACHE_CONNECTION_STRING"]));
            builder.Services.AddSingleton<PessoasCacheRepository>();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddResponseCompression();

            builder.Services.AddHostedService<LocalCacheWorker>();
            builder.Services.AddHostedService<PersistenceWorker>();

            var app = builder.Build();

            app.UseResponseCompression();
            //app.UseResponseCaching();
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            //app.UseAuthorization();
            // Configure the HTTP request pipeline.
            app.MapGet("/pessoas/{id}", async ([FromServices] ConnectionMultiplexer connection, [FromServices] PessoasCacheRepository cacheRepository, Guid id) =>
            {
                if (cacheRepository.TryGetValue(id, out byte[] pessoaJson))
                    return Results.Bytes(pessoaJson, "application/json");

                var database = connection.GetDatabase();
                var cached = await database.StringGetAsync($"Pessoas{id}");
                if (!cached.IsNull)
                    return Results.Bytes(cached, "application/json");
                return Results.NotFound();
            })
            .WithName("GetPessoaById")
            .WithOpenApi();

            app.MapGet("/pessoas", async ([FromServices] PessoasCacheRepository cacheRepository, [Required] string t) =>
            {
                return cacheRepository.Search(t);
            })
            .WithName("GetPessoas")
            .WithOpenApi();

            app.MapGet("/contagem-pessoas", ([FromServices] PessoasCacheRepository cacheRepository) =>
            {
                return cacheRepository.Count();
            })
            .WithName("GetContagemPessoas")
            .WithOpenApi();

            app.MapPost("/pessoas", async ([FromServices]ConnectionMultiplexer connection, [FromServices] PessoasCacheRepository cacheRepository, CreateRequestPessoa pessoa) =>
            {
                if (cacheRepository.Exists(pessoa.Apelido))
                    return Results.UnprocessableEntity();

                var id = Guid.NewGuid();
                var database = connection.GetDatabase();
                var p = new Pessoa(id, pessoa.Apelido, pessoa.Nome, pessoa.Nascimento, pessoa.Stack ?? Array.Empty<string>());
                var rawData = JsonSerializer.SerializeToUtf8Bytes(p);
                await database.StreamAddAsync("PessoasStream", "Raw",
                    rawData, 
                    flags: CommandFlags.FireAndForget);
                await database.StringSetAsync($"Pessoa{id}", rawData, flags: CommandFlags.FireAndForget);
                await database.PublishAsync(
                    new RedisChannel("PessoasChannel", RedisChannel.PatternMode.Literal),
                    rawData,
                    CommandFlags.FireAndForget);

                return Results.AcceptedAtRoute("GetPessoaById", new { id });
            })
            .WithName("PostPessoas")
            .WithOpenApi();

            await app.RunAsync();
        }
    }
}