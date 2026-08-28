using System.Data.OleDb;

namespace Integrador.Escrita;

/// <summary>
/// Achado técnico (20/08/2026): via VFPOLEDB, um `INSERT INTO tabela (colA,
/// colB) VALUES (?,?)` com lista PARCIAL de colunas falha — as colunas
/// omitidas não caem no valor-padrão do tipo (0 / "" / data vazia) como um
/// `APPEND BLANK` nativo do VFP faria; o provider tenta grava NULL nelas, e
/// praticamente toda coluna de `pedido.dbf` está marcada `NOT NULL` no
/// schema. A saída é montar o INSERT com TODAS as colunas da tabela,
/// preenchendo com o zero-valor do tipo o que não temos, e sobrepondo com o
/// que sabemos de verdade — exatamente o que `APPEND BLANK` + `REPLACE`
/// faz no `pcondpg2.prg` original.
///
/// Datas não informadas (24/08/2026): saem como DATA VAZIA de verdade — o
/// literal `CTOD('')` do VFP embutido no SQL — em vez do placeholder com a
/// data do pedido usado na 1ª versão (que poluía `data_fim`, `data_ent`,
/// `data_rota` etc. com uma data que não é dado de negócio).
/// </summary>
public static class GravadorGenerico
{
    public static int InserirLinhaCompleta(
        OleDbConnection conn,
        OleDbTransaction? transacao,
        string tabela,
        IReadOnlyDictionary<string, object?> valoresConhecidos)
    {
        var todasAsColunas = EsquemaTabelaColunas(conn, tabela);

        var nomes = new List<string>();
        var slots = new List<string>(); // "?" (parâmetro) ou literal CTOD('') para data vazia
        var valoresParametro = new List<object>();

        foreach (var (nome, tipoOleDb) in todasAsColunas)
        {
            nomes.Add(nome);

            if (valoresConhecidos.TryGetValue(nome, out var valor) && valor is not null)
            {
                slots.Add("?");
                valoresParametro.Add(valor);
                continue;
            }

            if (tipoOleDb is 133 or 135) // DBTYPE_DBDATE / DBTYPE_DBTIMESTAMP
            {
                // data vazia do VFP — não dá para parametrizar (o provider rejeita DBNull
                // em coluna NOT NULL), então vai como expressão literal do VFP no SQL
                slots.Add("CTOD('')");
                continue;
            }

            slots.Add("?");
            valoresParametro.Add(ValorZeroPadrao(tipoOleDb));
        }

        var sql = $"INSERT INTO {tabela} ({string.Join(",", nomes)}) VALUES ({string.Join(",", slots)})";

        using var cmd = transacao is null ? new OleDbCommand(sql, conn) : new OleDbCommand(sql, conn, transacao);
        foreach (var valor in valoresParametro)
        {
            cmd.Parameters.AddWithValue("@p", valor);
        }

        return cmd.ExecuteNonQuery();
    }

    private static IReadOnlyList<(string Nome, int TipoOleDb)> EsquemaTabelaColunas(OleDbConnection conn, string tabela)
    {
        var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object?[] { null, null, tabela, null });
        var colunas = new List<(string, int)>();
        foreach (System.Data.DataRow row in schema!.Rows)
        {
            colunas.Add((
                Convert.ToString(row["COLUMN_NAME"]) ?? "",
                row["DATA_TYPE"] is DBNull ? 0 : Convert.ToInt32(row["DATA_TYPE"])));
        }
        return colunas;
    }

    /// <summary>Zero-valor por tipo OLE DB — o mesmo que `APPEND BLANK` do VFP produz para um campo não tocado.</summary>
    private static object ValorZeroPadrao(int tipoOleDb) => tipoOleDb switch
    {
        129 or 130 => "",           // DBTYPE_STR / DBTYPE_WSTR (char)
        128 => "",                  // DBTYPE_BYTES (memo, tratado como string aqui)
        131 => 0m,                  // DBTYPE_NUMERIC
        3 => 0,                     // DBTYPE_I4 (ex.: ender_ent, campo numérico apesar do nome)
        11 => false,                // DBTYPE_BOOL
        _ => DBNull.Value,
    };
}
