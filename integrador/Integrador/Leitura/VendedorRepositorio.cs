using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

public sealed class VendedorRepositorio
{
    public VendedorDto? BuscarPorCodigo(string codVend)
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand("SELECT cod_vend, nome, tipo_vend FROM vendedor WHERE cod_vend = ?", conn);
        cmd.Parameters.AddWithValue("@cod_vend", codVend);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var lido = Str(reader, "cod_vend");
            if (!ChaveExata(lido, codVend)) continue;

            return new VendedorDto(lido, Str(reader, "nome"), Str(reader, "tipo_vend"));
        }

        return null;
    }

    /// <summary>Tabela pequena (193 registros medidos) — sync completo a cada ciclo.</summary>
    public IReadOnlyList<VendedorDto> ListarTodos()
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand("SELECT cod_vend, nome, tipo_vend FROM vendedor", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<VendedorDto>();
        while (reader.Read())
        {
            resultado.Add(new VendedorDto(Str(reader, "cod_vend"), Str(reader, "nome"), Str(reader, "tipo_vend")));
        }

        return resultado;
    }
}
