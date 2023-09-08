using System.Text.Json.Nodes;

namespace RinhaBackend.Models
{
    public record CreateRequestPessoa(string Apelido, string Nome, string Nascimento, JsonNode Stack);
}
