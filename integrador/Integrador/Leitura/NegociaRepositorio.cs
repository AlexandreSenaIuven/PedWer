using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>`negocia` — container próprio do WER, tabela única e global (RF-075).</summary>
public sealed class NegociaRepositorio
{
    public NegociaDto? BuscarPorChave(string codCli, string grupo, string referencia, string tipoPrc)
    {
        using var conn = VfpConexao.Abrir(VfpConexao.Caminho("negocia.dbc"));
        using var cmd = new OleDbCommand(
            "SELECT cod_cli, grupo, referencia, tipo_prc, dt_venc, preco, cod_valida " +
            "FROM negocia WHERE cod_cli = ? AND grupo = ? AND referencia = ? AND tipo_prc = ?",
            conn);
        cmd.Parameters.AddWithValue("@cod_cli", codCli);
        cmd.Parameters.AddWithValue("@grupo", grupo);
        cmd.Parameters.AddWithValue("@referencia", referencia);
        cmd.Parameters.AddWithValue("@tipo_prc", tipoPrc);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var codCliLido = Str(reader, "cod_cli");
            var grupoLido = Str(reader, "grupo");
            var referenciaLida = Str(reader, "referencia");
            var tipoPrcLido = Str(reader, "tipo_prc");
            if (!ChaveExata(codCliLido, codCli) || !ChaveExata(grupoLido, grupo) ||
                !ChaveExata(referenciaLida, referencia) || !ChaveExata(tipoPrcLido, tipoPrc))
                continue;

            return new NegociaDto(
                codCliLido,
                grupoLido,
                referenciaLida,
                tipoPrcLido,
                DataOpcional(reader, "dt_venc"),
                Dec(reader, "preco"),
                Dec(reader, "cod_valida"));
        }

        return null;
    }

    /// <summary>Tabela viva completa (11.996 registros medidos) — sync a cada ciclo; vigência/autorização são checadas no uso.</summary>
    public IReadOnlyList<NegociaDto> ListarTodos()
    {
        using var conn = VfpConexao.Abrir(VfpConexao.Caminho("negocia.dbc"));
        using var cmd = new OleDbCommand(
            "SELECT cod_cli, grupo, referencia, tipo_prc, dt_venc, preco, cod_valida FROM negocia", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<NegociaDto>();
        while (reader.Read())
        {
            resultado.Add(new NegociaDto(
                Str(reader, "cod_cli"),
                Str(reader, "grupo"),
                Str(reader, "referencia"),
                Str(reader, "tipo_prc"),
                DataOpcional(reader, "dt_venc"),
                Dec(reader, "preco"),
                Dec(reader, "cod_valida")));
        }

        return resultado;
    }
}
