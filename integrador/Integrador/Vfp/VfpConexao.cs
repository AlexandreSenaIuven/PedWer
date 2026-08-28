using System.Data.OleDb;

namespace Integrador.Vfp;

/// <summary>
/// Único ponto que sabe montar uma connection string VFPOLEDB. Todo o resto
/// do console depende só disto — nunca monta a string na mão em outro lugar.
///
/// `PastaBase` NÃO é mais fixo em código (era `Z:\BASES_CLIENTES\WER`, o
/// caminho da base de testes deste servidor) — cada instalação (cada
/// cliente) tem sua própria pasta de base. Vem da variável de ambiente
/// `PEDWER_PASTA_BASE`, lida uma vez em `Program.cs` no início da execução.
/// </summary>
public static class VfpConexao
{
    private static string? _pastaBase;

    /// <summary>
    /// Configurado uma vez, no início de `Program.cs`, a partir de
    /// `PEDWER_PASTA_BASE`. Ler antes de configurar é erro de programação
    /// (não silenciosamente cai num caminho de outro cliente).
    /// </summary>
    public static string PastaBase
    {
        get => _pastaBase ?? throw new InvalidOperationException(
            "VfpConexao.PastaBase não foi configurado — defina a variável de ambiente PEDWER_PASTA_BASE.");
        set => _pastaBase = value;
    }

    /// <summary>
    /// Pseudocódigo de empresa para o "estoque principal" (RF-021/menuwer.scx:
    /// `wc_empr` vazio = "empresa base"). Não é uma pasta `fenwinXX` — é a
    /// cópia de `cadmat`/`cadsub`/`cadicm`/`tabcol` na RAIZ da base (25.264
    /// produtos, confirmado 25/08/2026), a mesma que `cligeral`/`caduser`
    /// usam. Ao gravar o pedido, este código vira `cod_empr` EM BRANCO — a
    /// tradução acontece só em `MapeadorPedidoParaVfp`, nunca aqui.
    /// </summary>
    public const string CodigoPrincipal = "PRINCIPAL";

    public static string PastaEmpresa(string codigoEmpresa) =>
        Path.Combine(PastaBase, $"fenwin{codigoEmpresa}");

    public static string Caminho(string relativo) => Path.Combine(PastaBase, relativo);

    public static OleDbConnection Abrir(string dataSource)
    {
        var conn = new OleDbConnection(
            $"Provider=VFPOLEDB;Data Source={dataSource};Exclusive=No;Collating Sequence=general;");
        conn.Open();
        return conn;
    }

    /// <summary>Abre a pasta-base em modo tabela livre — usar para tabelas globais/únicas.</summary>
    public static OleDbConnection AbrirBase() => Abrir(PastaBase);

    /// <summary>
    /// Abre a pasta da empresa — usar para tabelas por empresa (cadmat, cadmov, ...).
    /// <see cref="CodigoPrincipal"/> é o único código que foge da pasta `fenwinXX`
    /// e cai na raiz — é assim que o "estoque principal" fica coberto pelos
    /// mesmos repositórios de leitura por empresa, sem duplicar SQL.
    /// </summary>
    public static OleDbConnection AbrirEmpresa(string codigoEmpresa) =>
        codigoEmpresa == CodigoPrincipal ? AbrirBase() : Abrir(PastaEmpresa(codigoEmpresa));
}
