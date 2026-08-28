using System.Data;

namespace Integrador.Leitura;

internal static class LeitorHelpers
{
    // Campos char do VFP são de largura fixa (padded com espaço) — sempre trim.
    public static string Str(IDataRecord r, string campo) => (r[campo] as string ?? "").Trim();

    public static decimal Dec(IDataRecord r, string campo) =>
        r[campo] is DBNull ? 0m : Convert.ToDecimal(r[campo]);

    public static DateTime? DataOpcional(IDataRecord r, string campo) =>
        r[campo] is DBNull ? null : Convert.ToDateTime(r[campo]);

    public static bool Logico(IDataRecord r, string campo) => r[campo] is bool b && b;

    /// <summary>
    /// Confere igualdade EXATA em C# depois da consulta — nunca confiar em "=" puro
    /// contra campo char do VFP, que sob SET EXACT OFF casa por prefixo (RF-105/D6).
    /// Esta é a rede de segurança contra esse defeito, independente de como o
    /// provider OLE DB resolveu a comparação internamente.
    /// </summary>
    public static bool ChaveExata(string valorLido, string valorBuscado) =>
        string.Equals(valorLido.Trim(), valorBuscado.Trim(), StringComparison.Ordinal);
}
