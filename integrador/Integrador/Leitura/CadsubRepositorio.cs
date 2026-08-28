using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>`cadsub` é POR EMPRESA — MVA/ST/pauta (§4.6 da análise). 649 registros medidos.</summary>
public sealed class CadsubRepositorio
{
    private const string Colunas =
        "grupo, referencia, uf, enquadra, ncm, val_icm, val_icm_su, icmproprio, reducbase, icm_reduc, prcmedio, fcp, subs_trib, " +
        "cst_icms, cfop, cfopsubst";

    public CadsubDto? BuscarPorChave(string codigoEmpresa, string grupo, string referencia, string uf)
    {
        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM cadsub WHERE grupo = ? AND referencia = ? AND uf = ?", conn);
        cmd.Parameters.AddWithValue("@grupo", grupo);
        cmd.Parameters.AddWithValue("@referencia", referencia);
        cmd.Parameters.AddWithValue("@uf", uf);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var grupoLido = Str(reader, "grupo");
            var referenciaLida = Str(reader, "referencia");
            var ufLida = Str(reader, "uf");
            if (!ChaveExata(grupoLido, grupo) || !ChaveExata(referenciaLida, referencia) || !ChaveExata(ufLida, uf))
                continue;

            return Mapear(reader);
        }

        return null;
    }

    /// <summary>Tabela pequena (649 registros) — sync completo a cada ciclo.</summary>
    public IReadOnlyList<CadsubDto> ListarTodos(string codigoEmpresa)
    {
        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM cadsub", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<CadsubDto>();
        while (reader.Read())
        {
            resultado.Add(Mapear(reader));
        }

        return resultado;
    }

    private static CadsubDto Mapear(OleDbDataReader reader) => new(
        Str(reader, "grupo"),
        Str(reader, "referencia"),
        Str(reader, "uf"),
        Str(reader, "enquadra"),
        Str(reader, "ncm"),
        Dec(reader, "val_icm"),
        Dec(reader, "val_icm_su"),
        Dec(reader, "icmproprio"),
        Dec(reader, "reducbase"),
        Dec(reader, "icm_reduc"),
        Dec(reader, "prcmedio"),
        Dec(reader, "fcp"),
        Dec(reader, "subs_trib"),
        Str(reader, "cst_icms"),
        Str(reader, "cfop"),
        Str(reader, "cfopsubst"));
}
