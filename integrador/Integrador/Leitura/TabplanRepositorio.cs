using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>`tabplan` — cadastro das empresas (3 registros medidos): nome, UF, alíquota interna de ICMS e dados de impressão.</summary>
public sealed class TabplanRepositorio
{
    public IReadOnlyList<TabplanDto> ListarTodos()
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand(
            "SELECT cod_empr, nome_empr, uf_empr, icm_estado, cgc, inscempr, ender_e, bairro, cidade_e, cep_e, telefone, fax, email FROM tabplan",
            conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<TabplanDto>();
        while (reader.Read())
        {
            resultado.Add(new TabplanDto(
                Str(reader, "cod_empr"),
                Str(reader, "nome_empr"),
                Str(reader, "uf_empr"),
                Dec(reader, "icm_estado"),
                Str(reader, "cgc"),
                Str(reader, "inscempr"),
                Str(reader, "ender_e"),
                Str(reader, "bairro"),
                Str(reader, "cidade_e"),
                Str(reader, "cep_e"),
                Str(reader, "telefone"),
                Str(reader, "fax"),
                Str(reader, "email")));
        }

        return resultado;
    }
}
