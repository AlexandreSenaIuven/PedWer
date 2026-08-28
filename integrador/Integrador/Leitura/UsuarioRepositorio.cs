using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>Leitura de `caduser` (tabela livre na raiz da base) — login web.</summary>
public sealed class UsuarioRepositorio
{
    /// <summary>Tabela pequena — sync completo a cada ciclo, como `vendedor`.</summary>
    public IReadOnlyList<UsuarioDto> ListarTodos()
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand("SELECT identific, senha, nome, cod_vend, inativo FROM caduser", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<UsuarioDto>();
        while (reader.Read())
        {
            resultado.Add(new UsuarioDto(
                Str(reader, "identific"),
                Str(reader, "senha"),
                Str(reader, "nome"),
                Str(reader, "cod_vend"),
                Logico(reader, "inativo")));
        }

        return resultado;
    }
}
