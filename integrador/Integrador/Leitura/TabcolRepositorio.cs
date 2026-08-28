using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>`tabcol` é POR EMPRESA. `Nome` é char mas guarda um número (VAL(tabcol.Nome) — 2ª parcela do teto, RF-083).</summary>
public sealed class TabcolRepositorio
{
    public TabcolDto? BuscarPorCodigo(string codigoEmpresa, string codigo)
    {
        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        using var cmd = new OleDbCommand("SELECT codigo, nome FROM tabcol WHERE codigo = ?", conn);
        cmd.Parameters.AddWithValue("@codigo", codigo);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var lido = Str(reader, "codigo");
            if (!ChaveExata(lido, codigo)) continue;

            return new TabcolDto(lido, Str(reader, "nome"));
        }

        return null;
    }

    /// <summary>Tabela pequena por empresa (2 registros medidos) — sync completo a cada ciclo.</summary>
    public IReadOnlyList<TabcolDto> ListarTodos(string codigoEmpresa)
    {
        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        using var cmd = new OleDbCommand("SELECT codigo, nome FROM tabcol", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<TabcolDto>();
        while (reader.Read())
        {
            resultado.Add(new TabcolDto(Str(reader, "codigo"), Str(reader, "nome")));
        }

        return resultado;
    }

    /// <summary>RF-083: `TabCol.Nome` é char mas guarda um número — VAL() explícito, com fallback 0 se não numérico.</summary>
    public static decimal NomeComoValor(TabcolDto? tabcol)
    {
        if (tabcol is null) return 0m;
        return decimal.TryParse(tabcol.Nome, out var valor) ? valor : 0m;
    }
}
