using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>
/// `cadmov` (por empresa, ~900k linhas — confirmado 915.759 na fenwin02 em
/// 02/09/2026) é histórico de movimentação, nunca sincronizada por inteiro.
/// "Últimas Compras" é consulta sob demanda, um cliente por vez.
///
/// `tipos` (usado só para o filtro `ind_fatura`) fica na RAIZ da base, não
/// na pasta da empresa — impossível fazer o JOIN num único SELECT via
/// VFPOLEDB (cada conexão enxerga só uma pasta). Resolvido com duas
/// consultas e cruzamento em C#, replicando `query_cadmov_vendas.qpr`.
/// </summary>
public sealed class CadmovRepositorio
{
    private static HashSet<string> BuscarTiposFaturaveis()
    {
        var tiposFaturaveis = new HashSet<string>();
        using var connBase = VfpConexao.AbrirBase();
        using var cmdTipos = new OleDbCommand("SELECT tipo, ind_fatura FROM tipos", connBase);
        using var readerTipos = cmdTipos.ExecuteReader();
        while (readerTipos.Read())
        {
            if (Str(readerTipos, "ind_fatura") == "S") tiposFaturaveis.Add(Str(readerTipos, "tipo"));
        }
        return tiposFaturaveis;
    }

    public IReadOnlyList<ItemCompraDto> ListarUltimasCompras(string codigoEmpresa, string codigoCliente, int limite)
    {
        var tiposFaturaveis = BuscarTiposFaturaveis();

        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        using var cmd = new OleDbCommand(
            "SELECT cli_for, data_mov, nota_fisc, tipo_oper, grupo, referencia, qtd_mov, valor_mov " +
            "FROM cadmov WHERE cli_for = ? AND es_mov = 'S' ORDER BY data_mov DESC",
            conn);
        cmd.Parameters.AddWithValue("@cli_for", codigoCliente);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<ItemCompraDto>();
        while (reader.Read() && resultado.Count < limite)
        {
            if (!ChaveExata(Str(reader, "cli_for"), codigoCliente)) continue; // RF-105
            if (!tiposFaturaveis.Contains(Str(reader, "tipo_oper"))) continue;

            resultado.Add(new ItemCompraDto(
                reader.GetDateTime(reader.GetOrdinal("data_mov")),
                Str(reader, "nota_fisc"),
                Str(reader, "grupo"),
                Str(reader, "referencia"),
                Dec(reader, "qtd_mov"),
                Dec(reader, "valor_mov")));
        }

        return resultado;
    }

    /// <summary>Botão "Giro" — mesma tabela/filtro de "Últimas Compras", trocando o filtro de cliente por produto.</summary>
    public IReadOnlyList<ItemGiroDto> ListarGiro(string codigoEmpresa, string grupo, string referencia, int limite)
    {
        var tiposFaturaveis = BuscarTiposFaturaveis();

        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        using var cmd = new OleDbCommand(
            "SELECT grupo, referencia, cli_for, data_mov, nota_fisc, tipo_oper, qtd_mov, valor_mov " +
            "FROM cadmov WHERE grupo = ? AND referencia = ? AND es_mov = 'S' ORDER BY data_mov DESC",
            conn);
        cmd.Parameters.AddWithValue("@grupo", grupo);
        cmd.Parameters.AddWithValue("@referencia", referencia);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<ItemGiroDto>();
        while (reader.Read() && resultado.Count < limite)
        {
            if (!ChaveExata(Str(reader, "grupo"), grupo) || !ChaveExata(Str(reader, "referencia"), referencia)) continue; // RF-105
            if (!tiposFaturaveis.Contains(Str(reader, "tipo_oper"))) continue;

            resultado.Add(new ItemGiroDto(
                reader.GetDateTime(reader.GetOrdinal("data_mov")),
                Str(reader, "nota_fisc"),
                Str(reader, "cli_for"),
                Dec(reader, "qtd_mov"),
                Dec(reader, "valor_mov")));
        }

        return resultado;
    }
}
