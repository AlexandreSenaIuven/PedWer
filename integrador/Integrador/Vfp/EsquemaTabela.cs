using System.Data;
using System.Data.OleDb;

namespace Integrador.Vfp;

/// <summary>
/// Introspecção de schema — só leitura de metadados, nunca de linhas. Usado
/// para conferir nomes de campo reais contra a base de produção em vez de
/// confiar na grafia usada na prosa dos documentos de análise.
/// </summary>
public static class EsquemaTabela
{
    public static IReadOnlyList<(string Nome, string Tipo, int Tamanho)> DescreverColunas(string dataSource, string tabela)
    {
        using var conn = VfpConexao.Abrir(dataSource);
        var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object?[] { null, null, tabela, null });
        var colunas = new List<(string, string, int)>();
        foreach (DataRow row in schema!.Rows)
        {
            colunas.Add((
                Convert.ToString(row["COLUMN_NAME"]) ?? "",
                Convert.ToString(row["DATA_TYPE"]) ?? "",
                row["CHARACTER_MAXIMUM_LENGTH"] is DBNull ? 0 : Convert.ToInt32(row["CHARACTER_MAXIMUM_LENGTH"])));
        }
        return colunas;
    }

    /// <summary>Colunas que não aceitam NULL — precisam de valor explícito em todo INSERT parcial.</summary>
    public static IReadOnlyList<string> ColunasObrigatorias(string dataSource, string tabela)
    {
        using var conn = VfpConexao.Abrir(dataSource);
        var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object?[] { null, null, tabela, null });
        var obrigatorias = new List<string>();
        foreach (DataRow row in schema!.Rows)
        {
            var nullable = row["IS_NULLABLE"] is DBNull ? true : Convert.ToBoolean(row["IS_NULLABLE"]);
            if (!nullable)
            {
                obrigatorias.Add(Convert.ToString(row["COLUMN_NAME"]) ?? "");
            }
        }
        return obrigatorias;
    }

    /// <summary>Lista os nomes de coluna do próprio rowset de schema — só para diagnóstico.</summary>
    public static IReadOnlyList<string> ColunasDoSchemaDeColunas(string dataSource, string tabela)
    {
        using var conn = VfpConexao.Abrir(dataSource);
        var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object?[] { null, null, tabela, null });
        return schema!.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
    }

    public static void Imprimir(string dataSource, string tabela)
    {
        Console.WriteLine($"--- {tabela} ({dataSource}) ---");
        try
        {
            foreach (var (nome, tipo, tamanho) in DescreverColunas(dataSource, tabela))
            {
                Console.WriteLine($"  {nome,-16} {tipo,-12} {tamanho}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FALHOU: {ex.GetType().Name}: {ex.Message}");
        }
        Console.WriteLine();
    }
}
