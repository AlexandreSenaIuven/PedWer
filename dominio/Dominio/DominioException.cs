namespace Dominio;

/// <summary>Violação de invariante do agregado — sempre com código estável (catálogo V01-V87 / RF-xxx).</summary>
public sealed class DominioException(string codigo, string message) : Exception(message)
{
    public string Codigo { get; } = codigo;
}
