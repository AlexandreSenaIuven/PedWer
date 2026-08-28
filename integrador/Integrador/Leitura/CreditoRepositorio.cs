using System.Data.OleDb;
using System.Text;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>
/// A leitura mais cara do fluxo de pedido (§3.3a do doc de integração): hoje
/// o VFP varre TODOS os títulos de cada cliente do grupo e filtra `dt_pag`
/// DEPOIS de ler — até 10.631 linhas para decidir um único pedido, quando o
/// saldo devedor real cabe em poucas dezenas. Esta versão filtra
/// `dt_pag IS NULL` (título aberto) DENTRO da consulta SQL, antes de trazer
/// qualquer linha para o processo — é o ganho que o documento aponta como "o
/// que torna o endpoint viável", não uma otimização cosmética.
/// </summary>
public sealed class CreditoRepositorio
{
    public IReadOnlyList<TituloAbertoDto> BuscarTitulosAbertosDoGrupo(IReadOnlyCollection<string> codigosClientes)
    {
        if (codigosClientes.Count == 0) return Array.Empty<TituloAbertoDto>();

        using var conn = VfpConexao.AbrirBase();
        var placeholders = string.Join(",", codigosClientes.Select(_ => "?"));
        var sql = new StringBuilder()
            .Append("SELECT codigo, valor, dt_vencim, cod_vencim FROM artficha ")
            .Append("WHERE dt_pag IS NULL AND codigo IN (").Append(placeholders).Append(')');

        using var cmd = new OleDbCommand(sql.ToString(), conn);
        foreach (var codigo in codigosClientes)
        {
            cmd.Parameters.AddWithValue("@codigo", codigo);
        }

        using var reader = cmd.ExecuteReader();
        var codigosValidos = codigosClientes.Select(c => c.Trim()).ToHashSet(StringComparer.Ordinal);

        var titulos = new List<TituloAbertoDto>();
        while (reader.Read())
        {
            // Rede de segurança: confere de novo em C# que o título é mesmo do
            // grupo pedido, mesmo já filtrado na cláusula IN da consulta.
            var codigo = Str(reader, "codigo");
            if (!codigosValidos.Contains(codigo)) continue;

            titulos.Add(new TituloAbertoDto(
                codigo,
                Dec(reader, "valor"),
                DataOpcional(reader, "dt_vencim"),
                Str(reader, "cod_vencim")));
        }

        return titulos;
    }

    /// <summary>
    /// Todos os títulos abertos da base — usado pelo laço de sincronização
    /// (o console empurra isto periodicamente para o servidor central, que
    /// faz a agregação por grupo econômico do lado de lá). Medido: só
    /// ~1.004 títulos abertos em toda a base (§3.3a) — tabela pequena, sync
    /// completo é barato.
    /// </summary>
    public IReadOnlyList<TituloAbertoDto> ListarTodosAbertos()
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand("SELECT codigo, valor, dt_vencim, cod_vencim FROM artficha WHERE dt_pag IS NULL", conn);
        using var reader = cmd.ExecuteReader();

        var titulos = new List<TituloAbertoDto>();
        while (reader.Read())
        {
            titulos.Add(new TituloAbertoDto(
                Str(reader, "codigo"),
                Dec(reader, "valor"),
                DataOpcional(reader, "dt_vencim"),
                Str(reader, "cod_vencim")));
        }

        return titulos;
    }
}
