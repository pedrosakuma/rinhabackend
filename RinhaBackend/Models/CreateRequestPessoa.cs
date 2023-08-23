namespace RinhaBackend.Models
{
    public record CreateRequestPessoa(string Apelido, string Nome, DateOnly Nascimento, string[]? Stack);
}
