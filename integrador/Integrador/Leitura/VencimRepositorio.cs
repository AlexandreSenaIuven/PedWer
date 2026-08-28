using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

public sealed class VencimRepositorio
{
    private const string Colunas = "codigo, desc_ocor, resumido, lim_venda, ind_dia_pf, cod_oper, dia_pref";

    public VencimDto? BuscarPorCodigo(string codigo)
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM vencim WHERE codigo = ?", conn);
        cmd.Parameters.AddWithValue("@codigo", codigo);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            if (!ChaveExata(Str(reader, "codigo"), codigo)) continue;
            return Mapear(reader);
        }

        return null;
    }

    /// <summary>Tabela pequena (99 registros medidos) — sync completo a cada ciclo.</summary>
    public IReadOnlyList<VencimDto> ListarTodos()
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM vencim", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<VencimDto>();
        while (reader.Read())
        {
            resultado.Add(Mapear(reader));
        }

        return resultado;
    }

    public VencimrDto? BuscarPorResumido(string resumido)
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand("SELECT resumido, ind_credit FROM vencimr WHERE resumido = ?", conn);
        cmd.Parameters.AddWithValue("@resumido", resumido);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var lido = Str(reader, "resumido");
            if (!ChaveExata(lido, resumido)) continue;

            return new VencimrDto(lido, Str(reader, "ind_credit"));
        }

        return null;
    }

    private static VencimDto Mapear(OleDbDataReader reader) => new(
        Str(reader, "codigo"),
        Str(reader, "desc_ocor"),
        Str(reader, "resumido"),
        Dec(reader, "lim_venda"),
        Str(reader, "ind_dia_pf"),
        Dec(reader, "cod_oper"),
        Dec(reader, "dia_pref"));
}
