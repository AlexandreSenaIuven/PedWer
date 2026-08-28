using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>`cadicm` é POR EMPRESA — alíquota de ICMS por UF+enquadramento (55 registros medidos).</summary>
public sealed class CadicmRepositorio
{
    private const string Colunas =
        "estado, enquadra, val_icm, val_icm_e, val_icm_su, subs_trib, aliq_inter, aliq_fcp, incide_fcp";

    public CadicmDto? BuscarPorEstado(string codigoEmpresa, string estado)
    {
        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM cadicm WHERE estado = ?", conn);
        cmd.Parameters.AddWithValue("@estado", estado);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var lido = Str(reader, "estado");
            if (!ChaveExata(lido, estado)) continue;

            return Mapear(reader);
        }

        return null;
    }

    /// <summary>Tabela pequena (55 registros) — sync completo a cada ciclo.</summary>
    public IReadOnlyList<CadicmDto> ListarTodos(string codigoEmpresa)
    {
        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM cadicm", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<CadicmDto>();
        while (reader.Read())
        {
            resultado.Add(Mapear(reader));
        }

        return resultado;
    }

    private static CadicmDto Mapear(OleDbDataReader reader) => new(
        Str(reader, "estado"),
        Str(reader, "enquadra"),
        Dec(reader, "val_icm"),
        Dec(reader, "val_icm_e"),
        Dec(reader, "val_icm_su"),
        Dec(reader, "subs_trib"),
        Dec(reader, "aliq_inter"),
        Dec(reader, "aliq_fcp"),
        Str(reader, "incide_fcp"));
}
