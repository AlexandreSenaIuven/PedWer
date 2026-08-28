using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

public sealed record CcustoDto(string Codigo, string Descricao);

/// <summary>`ccusto` — centro de custo do pedido (RF-158), tabela única.</summary>
public sealed class CcustoRepositorio
{
    public CcustoDto? BuscarPorCodigo(string codigo)
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand("SELECT c_custo, descricao FROM ccusto WHERE c_custo = ?", conn);
        cmd.Parameters.AddWithValue("@c_custo", codigo);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var lido = Str(reader, "c_custo");
            if (!ChaveExata(lido, codigo)) continue;

            return new CcustoDto(lido, Str(reader, "descricao"));
        }

        return null;
    }
}
