using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>
/// `tabcomis` — schema real confirmado em 20/08/2026: 5 campos NORMALIZADOS
/// (`negocio`, `desc`, `comis`, `figura`, `figura2`), uma linha por degrau —
/// não a tabela "larga" que a prosa dos documentos de análise sugeria ao
/// renderizar. 10 registros = 2 escadas × 5 degraus.
/// </summary>
public sealed class TabcomisRepositorio
{
    public IReadOnlyList<EscadaComissaoItemDto> CarregarEscadas()
    {
        using var conn = VfpConexao.Abrir(VfpConexao.Caminho("negocia.dbc"));
        using var cmd = new OleDbCommand("SELECT negocio, [desc], comis FROM tabcomis ORDER BY negocio, [desc]", conn);
        using var reader = cmd.ExecuteReader();

        var itens = new List<EscadaComissaoItemDto>();
        while (reader.Read())
        {
            itens.Add(new EscadaComissaoItemDto(
                Dec(reader, "negocio"),
                Dec(reader, "desc"),
                Dec(reader, "comis")));
        }

        return itens;
    }
}
