using Microsoft.AspNetCore.Mvc;
using RinhaBackend.Grpc;
using RinhaBackend.Models;
using RinhaBackend.Repositories;
using RinhaBackend.Services;
using RinhaBackend.Workers;
using System.Threading.Channels;

namespace RinhaBackend
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateSlimBuilder(args);
            //ThreadPool.SetMinThreads(1024, 1024);
            builder.WebHost.UseKestrelHttpsConfiguration();

            builder.Services.AddGrpc();
            builder.Services.AddNpgsqlDataSource(builder.Configuration["DB_CONNECTION_STRING"]);
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
            });

            builder.Services.AddGrpcClient<Pessoas.PessoasClient>(o =>
            {
                o.Address = new Uri(builder.Configuration["GRPC_CHANNEL"]);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                return handler;
            });
            builder.Services.AddSingleton<GrpcPessoasService>();
            builder.Services.AddSingleton<PessoasCacheRepository>();
            builder.Services.AddSingleton(s => AppJsonSerializerContext.Default);
            builder.Services.AddSingleton((s) => new PersistencePessoasChannel(Channel.CreateUnbounded<Pessoa>(new UnboundedChannelOptions
            {
                SingleReader = true
            })));
            builder.Services.AddSingleton((s) => new LocalPessoasChannel(Channel.CreateUnbounded<Pessoa>(new UnboundedChannelOptions
            {
                SingleReader = true
            })));
            builder.Services.AddSingleton((s) => new LocalSearchPessoasChannel(Channel.CreateUnbounded<Pessoa>(new UnboundedChannelOptions
            {
                SingleReader = true
            })));

            builder.Services.AddHostedService<LocalCacheWorker>();
            builder.Services.AddHostedService<LocalSearchCacheWorker>();
            builder.Services.AddHostedService<PersistenceWorker>();
            builder.Services.AddHostedService<RemoteCacheWorker>();

            var app = builder.Build();

            app.MapGrpcService<GrpcPessoasService>();
            app.MapGet("/pessoas/{id}", static async (
                [FromServices] PessoasCacheRepository cacheRepository, 
                [FromRoute] Guid id) =>
            {
                byte[]? pessoaJson = await cacheRepository.GetValueAsync(id, TimeSpan.FromMilliseconds(500));
                if (pessoaJson == null)
                    return Results.NotFound();
                return Results.Bytes(pessoaJson, "application/json");
            })
            .WithName("GetPessoaById");

            app.MapGet("/pessoas", static (
                [FromServices] PessoasCacheRepository cacheRepository, 
                [FromQuery] string? t) =>
            {
                if (t == null)
                    return Results.BadRequest();
                return Results.Ok(cacheRepository.Search(t));
            })
            .WithName("GetPessoas");

            app.MapGet("/contagem-pessoas", static (
                [FromServices] PessoasPersistedRepository cacheRepository) =>
            {
                return cacheRepository.CountAsync();
            })
            .WithName("GetContagemPessoas");

            app.MapPost("/pessoas", static async (
                [FromServices] LocalPessoasChannel localChannel,
                [FromServices] LocalSearchPessoasChannel localSearchChannel,
                [FromServices] PersistencePessoasChannel persistenceChannel, 
                [FromServices] PessoasCacheRepository cacheRepository, 
                [FromBody] CreateRequestPessoa pessoa) =>
            {
                string[]? stack = null;
                if (!DateOnly.TryParseExact(pessoa.Nascimento, "yyyy-MM-dd", out DateOnly nascimento)
                || (pessoa.Stack != null && !pessoa.Stack.TryGetValue(out stack))
                || cacheRepository.Exists(pessoa.Apelido))
                    return Results.UnprocessableEntity();

                var id = Guid.NewGuid();
                var p = new Pessoa(id, pessoa.Apelido, pessoa.Nome, nascimento, stack ?? Array.Empty<string>());

                await localChannel.Channel.Writer.WriteAsync(p);
                await localSearchChannel.Channel.Writer.WriteAsync(p);
                await persistenceChannel.Channel.Writer.WriteAsync(p);
                
                return Results.CreatedAtRoute("GetPessoaById", new RouteValueDictionary() { { "id", id } });
            })
            .WithName("PostPessoas");

            await app.RunAsync();
        }
    }
}