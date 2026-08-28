using Integrador.Vfp;

namespace Integrador.Escrita;

/// <summary>
/// Grava as linhas físicas produzidas por `MapeadorPedidoParaVfp` em
/// `pedido.dbf`, dentro de uma única transação (RF-163) — o oposto do
/// caminho atual, que faz vários passes de escrita sem transação nenhuma
/// (Janelas 1 e 2 do doc de integração).
///
/// Usa `GravadorGenerico` porque, via VFPOLEDB, um INSERT com lista PARCIAL
/// de colunas falha — quase todo campo de `pedido.dbf` está marcado NOT
/// NULL no schema, e as colunas omitidas não caem no zero-valor do tipo como
/// um `APPEND BLANK` nativo faria. Confirmado por teste em 20/08/2026 contra
/// a cópia de testes (`testar-transacao`): `OleDbTransaction` funciona de
/// verdade (commit e rollback comprovados por leitura).
/// </summary>
public sealed class PedidoDbfRepositorio
{
    public void GravarPedido(IReadOnlyList<LinhaPedidoFisica> linhas)
    {
        if (linhas.Count == 0)
        {
            throw new ArgumentException("Pedido sem linhas para gravar — o mapeador já deveria ter rejeitado isso antes.", nameof(linhas));
        }

        using var conn = VfpConexao.Abrir(VfpConexao.Caminho("PEDIDO.DBC"));
        using var transacao = conn.BeginTransaction();

        try
        {
            foreach (var linha in linhas)
            {
                var linhasAfetadas = GravadorGenerico.InserirLinhaCompleta(conn, transacao, "pedido", ParaDicionario(linha));
                if (linhasAfetadas != 1)
                {
                    throw new InvalidOperationException(
                        $"INSERT do item {linha.MaterialTres} do pedido {linha.Codigo} afetou {linhasAfetadas} linha(s), esperado 1.");
                }
            }

            transacao.Commit();
        }
        catch
        {
            transacao.Rollback();
            throw;
        }
    }

    private static Dictionary<string, object?> ParaDicionario(LinhaPedidoFisica linha) => new()
    {
        ["codigo"] = linha.Codigo,
        ["es_mov"] = linha.EsMov,
        ["tipo_oper"] = linha.TipoOper,
        ["cod_cli"] = linha.CodCli,
        ["data_ped"] = linha.DataPed,
        ["c_custo"] = linha.CCusto,
        ["cod_vend"] = linha.CodVend,
        ["cod_vend1"] = linha.CodVend1,
        ["cond_pag"] = linha.CondPag,
        ["cod_empr"] = linha.CodEmpr,
        ["comprador"] = linha.Comprador,
        ["total_nota"] = linha.TotalNota,
        ["material03"] = linha.MaterialTres,
        ["grupo"] = linha.Grupo,
        ["referencia"] = linha.Referencia,
        ["qtd_itens"] = linha.QtdItens,
        ["prc_venda"] = linha.PrcVenda,
        ["qtd_larg"] = linha.QtdLarg,
        ["qtd_comp"] = linha.QtdComp,
        ["qtditenspe"] = linha.QtditensPe,
        ["cfop"] = linha.Cfop,
        ["cst"] = linha.Cst,
        ["cstpis"] = linha.CstPis,
        ["aliqpis"] = linha.AliqPis,
        ["cstcof"] = linha.CstCof,
        ["aliqcof"] = linha.AliqCof,
        ["ipi"] = linha.Ipi,
        ["icm"] = linha.Icm,
        ["icmret_m"] = linha.IcmretM,
        ["baseicm_rt"] = linha.BaseicmRt,
        ["unidade"] = linha.Unidade,
        ["total_ipi"] = linha.TotalIpi,
        ["base_icm"] = linha.BaseIcm,
        ["icms_ret"] = linha.IcmsRet,
        ["bc_retenc"] = linha.BcRetenc,
        ["total_icm"] = linha.TotalIcm,
        ["tot_merc"] = linha.TotMerc,
        ["hora_ini"] = linha.HoraIni,
        ["hora_fim"] = linha.HoraFim,
        ["data_ent"] = linha.DataEnt,
        ["csosn"] = linha.Csosn,
        ["qt_reserva"] = linha.QtReserva,
        ["posicao"] = linha.Posicao,
        ["notafis"] = linha.Notafis,
    };
}
