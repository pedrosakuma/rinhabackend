using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using RinhaBackend.Grpc;
using RinhaBackend.Models;
using RinhaBackend.Repositories;
using RinhaBackend.Services;
using RinhaBackend.Workers;
using System.Buffers;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Runtime;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RinhaBackend
{
    public class Program
    {
        private static readonly byte[] StartArray = new byte[] { (byte)'[' };
        private static readonly byte[] Comma = new byte[] { (byte)',' };
        private static readonly byte[] EndArray = new byte[] { (byte)']' };

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateSlimBuilder(args);
            builder.WebHost.UseKestrelHttpsConfiguration();

            //builder.Services.AddResponseCompression(options =>
            //{
            //    options.Providers.Add<GzipCompressionProvider>();
            //});
            //builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            //{
            //    options.Level = CompressionLevel.Fastest;
            //});

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
            builder.Services.AddSingleton(s => AppJsonSerializerContext.Default);

            builder.Services.AddTransient<PessoasPersistedRepository>();
            builder.Services.AddSingleton<PessoasCacheRepository>();

            builder.Services.AddSingleton<GrpcPessoasService>();

            builder.Services.AddSingleton<PersistencePessoasChannel>();
            builder.Services.AddSingleton<LocalPessoasChannel>();

            builder.Services.AddHostedService<LocalCacheWorker>();
            builder.Services.AddHostedService<PersistenceWorker>();
            builder.Services.AddHostedService<RemoteCacheWorker>();

            var app = builder.Build();

            app.MapGrpcService<GrpcPessoasService>();
            //app.UseResponseCompression();
            app.MapGet("/pessoas/{id}", static async (
                [FromServices] PessoasCacheRepository cacheRepository,
                [FromRoute] Guid id) =>
            {
                byte[]? pessoaJson = await cacheRepository.GetValueAsync(id, TimeSpan.FromSeconds(10));
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
                return Results.Stream(async s =>
                {
                    byte[][] results = ArrayPool<byte[]>.Shared.Rent(50);
                    try
                    {
                        await s.WriteAsync(StartArray);
                        var count = cacheRepository.Search(t, results.AsSpan(0, 50));
                        if (count > 0)
                        {
                            await s.WriteAsync(results[0]);
                            for (int i = 1; i < count; i++)
                            {
                                await s.WriteAsync(Comma);
                                await s.WriteAsync(results[i]);
                            }
                        }
                        await s.WriteAsync(EndArray);
                    }
                    finally
                    {
                        ArrayPool<byte[]>.Shared.Return(results);
                    }
                }, "application/json");
            })
            .WithName("GetPessoas");

            app.MapGet("/contagem-pessoas", static async (
                [FromServices] PessoasPersistedRepository cacheRepository) =>
            {
                return await cacheRepository.CountAsync();
            })
            .WithName("GetContagemPessoas");

            app.MapPost("/pessoas", static (
                [FromServices] LocalPessoasChannel localChannel,
                [FromServices] PersistencePessoasChannel persistenceChannel,
                [FromServices] PessoasCacheRepository cacheRepository,
                [FromBody] CreateRequestPessoa pessoa) =>
            {
                string[] stack = Array.Empty<string>();
                if (!DateOnly.TryParseExact(pessoa.Nascimento, "yyyy-MM-dd", out DateOnly nascimento)
                || cacheRepository.Exists(pessoa.Apelido))
                    return Results.UnprocessableEntity();

                if (pessoa.Stack != null && pessoa.Stack is JsonArray jsonArrayStack)
                {
                    stack = jsonArrayStack
                        .Where(node => node.GetValueKind() == JsonValueKind.String)
                        .Select(node => node.GetValue<string>())
                        .ToArray();
                }
                var id = Guid.NewGuid();
                var p = new Pessoa(id, pessoa.Apelido, pessoa.Nome, nascimento, stack);

                localChannel.Channel.Writer.TryWrite(p);
                persistenceChannel.Channel.Writer.TryWrite(p);

                return Results.CreatedAtRoute("GetPessoaById", new RouteValueDictionary() { { "id", id } });
            })
            .WithName("PostPessoas");

            GC.TryStartNoGCRegion(920125440);
            await app.RunAsync();
        }
    }
}