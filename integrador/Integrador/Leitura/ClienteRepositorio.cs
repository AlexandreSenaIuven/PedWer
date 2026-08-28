using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>Leitura de `clientes` (container fenicia, tabela única — §2/§3 do doc de integração).</summary>
public sealed class ClienteRepositorio
{
    private const string Colunas =
        "codigo, razao_soc, cgc, posicao, cod_vendor, cod_vend2, credito, csll, " +
        "inss, irrf, iss, pis, cofins, cond_pag, cod_empr, " +
        "estado, cgc_cpf, enquadra, suframa, incide_suf, incide_pis, desonera, " +
        "endereco, bairro, cidade, cep, telefone1, insc_esta, tipo_cli, comprador";

    public ClienteDto? BuscarPorCodigo(string codigo)
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM clientes WHERE codigo = ?", conn);
        cmd.Parameters.AddWithValue("@codigo", codigo);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var lido = Str(reader, "codigo");
            if (!ChaveExata(lido, codigo)) continue; // RF-105: nunca confiar em "=" puro contra char do VFP

            return Mapear(reader);
        }

        return null;
    }

    /// <summary>
    /// Grupo econômico pela raiz do CNPJ (RF-190) — mesmos 10 primeiros dígitos
    /// de `cgc`. Filtra em C# depois de trazer o candidato bruto porque não há
    /// índice sobre SUBSTR(cgc,1,10) na base (o doc de integração já registra
    /// isso como varredura completa de `clientes` — 13.086 registros).
    /// </summary>
    public IReadOnlyList<ClienteDto> BuscarGrupoEconomicoPorRaizCgc(string raizCgc10)
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM clientes", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<ClienteDto>();
        while (reader.Read())
        {
            var cgc = Str(reader, "cgc");
            if (cgc.Length < 10 || !cgc[..10].Equals(raizCgc10, StringComparison.Ordinal)) continue;

            resultado.Add(Mapear(reader));
        }

        return resultado;
    }

    /// <summary>
    /// Sem termo: amostra de clientes com crédito > 0 (atalho para a lista
    /// inicial da tela). Com termo: varre a base inteira filtrando por
    /// código ou razão social — é a busca por trás da tela de consulta com
    /// filtro que a UI abre para campos ligados a `clientes`.
    /// </summary>
    public IReadOnlyList<ClienteDto> Buscar(string? termo, int limite)
    {
        using var conn = VfpConexao.AbrirBase();
        var sql = string.IsNullOrWhiteSpace(termo)
            ? $"SELECT {Colunas} FROM clientes WHERE credito > 0"
            : $"SELECT {Colunas} FROM clientes";
        using var cmd = new OleDbCommand(sql, conn);
        using var reader = cmd.ExecuteReader();

        var termoNormalizado = termo?.Trim().ToUpperInvariant();
        var resultado = new List<ClienteDto>();
        while (reader.Read() && resultado.Count < limite)
        {
            var codigo = Str(reader, "codigo");
            var razaoSoc = Str(reader, "razao_soc");
            if (!string.IsNullOrEmpty(termoNormalizado) &&
                !codigo.ToUpperInvariant().Contains(termoNormalizado) &&
                !razaoSoc.ToUpperInvariant().Contains(termoNormalizado))
            {
                continue;
            }

            resultado.Add(Mapear(reader));
        }

        return resultado;
    }

    /// <summary>
    /// TODOS os clientes, sem filtro — usado pelo sync. O filtro `credito > 0`
    /// do Buscar sem termo era só um atalho de amostra para o CLI e acabou
    /// vazando para o sync, deixando 12.587 clientes invisíveis na web
    /// (bug corrigido em 24/08/2026).
    /// </summary>
    public IReadOnlyList<ClienteDto> ListarTodos()
    {
        using var conn = VfpConexao.AbrirBase();
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM clientes", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<ClienteDto>();
        while (reader.Read())
        {
            resultado.Add(Mapear(reader));
        }

        return resultado;
    }

    private static ClienteDto Mapear(OleDbDataReader reader) => new(
        Str(reader, "codigo"),
        Str(reader, "razao_soc"),
        Str(reader, "cgc"),
        Str(reader, "posicao"),
        Str(reader, "cod_vendor"),
        Str(reader, "cod_vend2"),
        Dec(reader, "credito"),
        Dec(reader, "csll"),
        Dec(reader, "inss"),
        Dec(reader, "irrf"),
        Dec(reader, "iss"),
        Dec(reader, "pis"),
        Dec(reader, "cofins"),
        Str(reader, "cond_pag"),
        Str(reader, "cod_empr"),
        Str(reader, "estado"),
        Str(reader, "cgc_cpf"),
        Str(reader, "enquadra"),
        Str(reader, "suframa"),
        Dec(reader, "incide_suf"),
        Dec(reader, "incide_pis"),
        Str(reader, "desonera"),
        Str(reader, "endereco"),
        Str(reader, "bairro"),
        Str(reader, "cidade"),
        Str(reader, "cep"),
        Str(reader, "telefone1"),
        Str(reader, "insc_esta"),
        Str(reader, "tipo_cli"),
        Str(reader, "comprador"));
}
