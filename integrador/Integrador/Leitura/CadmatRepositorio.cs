using System.Data.OleDb;
using Integrador.Vfp;
using static Integrador.Leitura.LeitorHelpers;

namespace Integrador.Leitura;

/// <summary>
/// `cadmat` é POR EMPRESA (RF-021/RF-003) — nunca ler sem o prefixo da
/// empresa corrente. Confirmado: 31.555 registros em fenwin02, 22.499 em
/// fenwin03. A cópia na raiz da base (25.264 registros) é uma TERCEIRA
/// cópia — o catálogo do "estoque principal" (`VfpConexao.CodigoPrincipal`,
/// 25/08/2026) — não uma amostra das outras duas.
/// </summary>
public sealed class CadmatRepositorio
{
    private const string Colunas =
        "grupo, referencia, descricao, prc_venda, gradecol, gradegrp, qtd_pedida, qtd_fpedid, " +
        "ipi, ind_ipi, cod_proc, ncm, val_icm, val_icm_su, desp_subst, perm_icm, legislacao, beneficio, unid_emb, peso_unit, volume, " +
        "cstpis, aliqpis, cstcof, aliqcof, caracter";

    public CadmatDto? BuscarPorChave(string codigoEmpresa, string grupo, string referencia)
    {
        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM cadmat WHERE grupo = ? AND referencia = ?", conn);
        cmd.Parameters.AddWithValue("@grupo", grupo);
        cmd.Parameters.AddWithValue("@referencia", referencia);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var grupoLido = Str(reader, "grupo");
            var referenciaLida = Str(reader, "referencia");
            if (!ChaveExata(grupoLido, grupo) || !ChaveExata(referenciaLida, referencia)) continue;

            return Mapear(reader);
        }

        return null;
    }

    /// <summary>
    /// Sem termo: amostra de produtos com preço de tabela > 0. Com termo:
    /// varre a empresa inteira filtrando por grupo, referência ou descrição
    /// — é a busca por trás da tela de consulta com filtro para produto.
    /// </summary>
    public IReadOnlyList<CadmatDto> Buscar(string codigoEmpresa, string? termo, int limite)
    {
        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        var sql = string.IsNullOrWhiteSpace(termo)
            ? $"SELECT {Colunas} FROM cadmat WHERE prc_venda > 0"
            : $"SELECT {Colunas} FROM cadmat";
        using var cmd = new OleDbCommand(sql, conn);
        using var reader = cmd.ExecuteReader();

        var termoNormalizado = termo?.Trim().ToUpperInvariant();
        var resultado = new List<CadmatDto>();
        while (reader.Read() && resultado.Count < limite)
        {
            var grupo = Str(reader, "grupo");
            var referencia = Str(reader, "referencia");
            var descricao = Str(reader, "descricao");
            if (!string.IsNullOrEmpty(termoNormalizado) &&
                !grupo.ToUpperInvariant().Contains(termoNormalizado) &&
                !referencia.ToUpperInvariant().Contains(termoNormalizado) &&
                !descricao.ToUpperInvariant().Contains(termoNormalizado))
            {
                continue;
            }

            resultado.Add(Mapear(reader));
        }

        return resultado;
    }

    /// <summary>
    /// TODOS os produtos da empresa, sem filtro — usado pelo sync. Produto
    /// com preço zero também entra: melhor aparecer na busca e ser rejeitado
    /// com "PRECO_TABELA_ZERO" ao precificar do que sumir sem explicação
    /// (é como a tela do VFP se comporta — aceita qualquer código digitado).
    /// </summary>
    public IReadOnlyList<CadmatDto> ListarTodos(string codigoEmpresa)
    {
        using var conn = VfpConexao.AbrirEmpresa(codigoEmpresa);
        using var cmd = new OleDbCommand($"SELECT {Colunas} FROM cadmat", conn);
        using var reader = cmd.ExecuteReader();

        var resultado = new List<CadmatDto>();
        while (reader.Read())
        {
            resultado.Add(Mapear(reader));
        }

        return resultado;
    }

    private static CadmatDto Mapear(OleDbDataReader reader) => new(
        Str(reader, "grupo"),
        Str(reader, "referencia"),
        Str(reader, "descricao"),
        Dec(reader, "prc_venda"),
        Str(reader, "gradecol"),
        Str(reader, "gradegrp"),
        Dec(reader, "qtd_pedida"),
        Dec(reader, "qtd_fpedid"),
        Dec(reader, "ipi"),
        Str(reader, "ind_ipi"),
        Str(reader, "cod_proc"),
        Str(reader, "ncm"),
        Dec(reader, "val_icm"),
        Dec(reader, "val_icm_su"),
        Dec(reader, "desp_subst"),
        Str(reader, "perm_icm"),
        Str(reader, "legislacao"),
        Str(reader, "beneficio"),
        Str(reader, "unid_emb"),
        Dec(reader, "peso_unit"),
        Dec(reader, "volume"),
        Str(reader, "cstpis"),
        Dec(reader, "aliqpis"),
        Str(reader, "cstcof"),
        Dec(reader, "aliqcof"),
        Str(reader, "caracter"));

    /// <summary>
    /// Botão "Saldo Geral" — saldo de estoque do produto em cada empresa
    /// real (uma pasta por empresa, sem JOIN entre elas). Não inclui
    /// `VfpConexao.CodigoPrincipal`: é catálogo da raiz, não um estoque
    /// físico a mais.
    /// </summary>
    public IReadOnlyList<SaldoEmpresaDto> ConsultarSaldo(IReadOnlyList<string> empresas, string grupo, string referencia)
    {
        var resultado = new List<SaldoEmpresaDto>();
        foreach (var empresa in empresas)
        {
            using var conn = VfpConexao.AbrirEmpresa(empresa);
            using var cmd = new OleDbCommand("SELECT grupo, referencia, qtdreal, qt_reserva FROM cadmat WHERE grupo = ? AND referencia = ?", conn);
            cmd.Parameters.AddWithValue("@grupo", grupo);
            cmd.Parameters.AddWithValue("@referencia", referencia);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!ChaveExata(Str(reader, "grupo"), grupo) || !ChaveExata(Str(reader, "referencia"), referencia)) continue; // RF-105
                resultado.Add(new SaldoEmpresaDto(empresa, Dec(reader, "qtdreal"), Dec(reader, "qt_reserva")));
                break;
            }
        }
        return resultado;
    }
}
