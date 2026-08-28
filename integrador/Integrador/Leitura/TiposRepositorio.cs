using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>`tipos` (RF-020) — governa ~30 decisões a partir de um único registro por tipo de operação.</summary>
public sealed class TiposRepositorio
{
    private const string Colunas =
        "tipo, descricao, tipo_es, ind_qtd, natureza, ind_valb, posicao, ind_comiss, ind_empenho, ind_senha, cod_vencim, operacao, " +
        "piscofins, nfc_e, cod_contro";

    public TiposDto? BuscarPorCodigo(string tipo)
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM tipos WHERE tipo = ?", conn);
        cmd.Parameters.AddWithValue("@tipo", tipo);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var lido = Str(reader, "tipo");
            if (!ChaveExata(lido, tipo)) continue;

            return Mapear(reader);
        }

        return null;
    }

    /// <summary>Tabela pequena (487 registros medidos) — sync completo a cada ciclo.</summary>
    public IReadOnlyList<TiposDto> ListarTodos()
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM tipos", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<TiposDto>();
        while (reader.Read())
        {
            resultado.Add(Mapear(reader));
        }

        return resultado;
    }

    private static TiposDto Mapear(OleDbDataReader reader) => new(
        Str(reader, "tipo"),
        Str(reader, "descricao"),
        Str(reader, "tipo_es"),
        Str(reader, "ind_qtd"),
        Str(reader, "natureza"),
        Str(reader, "ind_valb"),
        Str(reader, "posicao"),
        Str(reader, "ind_comiss"),
        Str(reader, "ind_empenho"),
        Str(reader, "ind_senha"),
        Str(reader, "cod_vencim"),
        Dec(reader, "operacao"),
        Str(reader, "piscofins"),
        Str(reader, "nfc_e"),
        Dec(reader, "cod_contro"));
}
