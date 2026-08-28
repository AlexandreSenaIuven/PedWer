using Dominio;
using Integrador.Vfp;

namespace Integrador.Escrita;

/// <summary>
/// Traduz o agregado novo (`Pedido`/`ItemPedido`, nomes de negócio) para as
/// linhas físicas que `pedido.dbf` espera (nomes do VFP, cabeçalho repetido
/// por item) — a metade de ida do contrato interno API↔console (decisão
/// L10). Função pura: não abre conexão, não grava nada. A gravação de fato
/// (Fase 5) é quem chama isto e depois passa o resultado para o VFPOLEDB —
/// com autorização própria, por tocar produção.
/// </summary>
public static class MapeadorPedidoParaVfp
{
    public static IReadOnlyList<LinhaPedidoFisica> Mapear(Pedido pedido, string esMov, string comprador)
    {
        if (pedido.Estado != EstadoPedido.Fechado)
        {
            // Antes do fechamento os vendedores e a comissão de cada item ainda não existem
            // (decisão L2) — mapear um rascunho produziria uma linha física inventada.
            throw new MapeamentoException(
                "PEDIDO_NAO_FECHADO",
                $"Só é possível mapear um pedido Fechado para o formato físico do VFP; estado atual: {pedido.Estado}.");
        }

        if (pedido.ReferenciaExterna is null)
        {
            // RF-102/§9-Q4 dos documentos: a regra real dos prefixos do código ainda não foi
            // respondida pelo negócio. Preferimos falhar explicitamente a inventar um `codigo`
            // que colida com o espaço alfanumérico já em uso (Cenário C da análise).
            throw new MapeamentoException(
                "SEM_REFERENCIA_EXTERNA",
                "Pedido não tem ReferenciaExterna definida — a regra do código do pedido no VFP ainda não foi decidida pelo negócio.");
        }

        var itensAtivos = pedido.ItensAtivos.ToList();
        var totalItensAtivos = itensAtivos.Count;

        // Totais fiscais do pedido — o VFP repete os totais de capa em cada
        // linha (fecha_nota); aqui eles são calculados uma vez e repetidos.
        var totalProdutos = itensAtivos.Sum(i => i.PrecoFinal * i.Quantidade);
        var totalIpi = itensAtivos.Sum(i => i.Fiscal?.ValorIpi ?? 0m);
        var totalSt = itensAtivos.Sum(i => i.Fiscal?.ValorIcmSt ?? 0m);
        var baseIcm = itensAtivos.Sum(i => i.Fiscal?.BaseIcm ?? 0m);
        var bcRetenc = itensAtivos.Sum(i => i.Fiscal?.BaseIcmSt ?? 0m);
        var totalIcm = itensAtivos.Sum(i => i.Fiscal?.ValorIcm ?? 0m);
        var totMerc = itensAtivos.Sum(i => i.Fiscal?.ValorMercadoria ?? i.PrecoFinal * i.Quantidade);
        var horaAgora = DateTime.Now.ToString("HH:mm:ss");

        return itensAtivos.Select(item =>
        {
            if (item.PercentualComissao is null)
            {
                // Não deveria acontecer para um pedido Fechado (Pedido.Fechar resolve todos os
                // itens ativos) — é uma checagem de defesa, não um caminho esperado.
                throw new MapeamentoException(
                    "ITEM_SEM_COMISSAO_RESOLVIDA",
                    $"Item {item.Numero} está ativo num pedido fechado mas não tem comissão resolvida.");
            }

            return new LinhaPedidoFisica(
                Codigo: pedido.ReferenciaExterna,
                EsMov: esMov,
                TipoOper: pedido.TipoOperacao,
                CodCli: pedido.CodigoCliente,
                DataPed: pedido.Data.ToDateTime(TimeOnly.MinValue),
                CCusto: pedido.CentroCustoCodigo,
                CodVend: pedido.VendedorCodigo1!,
                CodVend1: pedido.VendedorCodigo2!,
                CondPag: pedido.CondicaoPagamentoCodigo ?? "",
                // "PRINCIPAL" (estoque principal, RF-021/wc_empr) grava cod_empr
                // EM BRANCO — é o pseudocódigo usado só para achar o cadmat/cadsub
                // da raiz (VfpConexao.CodigoPrincipal), nunca um cod_empr real.
                CodEmpr: pedido.CodigoEmpresa == VfpConexao.CodigoPrincipal ? "" : pedido.CodigoEmpresa,
                Comprador: comprador,
                // total_nota inclui impostos, como o VFP (ex.: ped_we — produtos + IPI + ST)
                TotalNota: totalProdutos + totalIpi + totalSt,
                MaterialTres: item.Numero.ToString(), // sem zeros à esquerda (ajuste 24/08/2026)
                Grupo: item.ProdutoGrupo,
                Referencia: item.ProdutoReferencia,
                QtdItens: item.Quantidade,
                PrcVenda: item.PrecoFinal,
                QtdLarg: item.PrecoTabelaAjustado,
                QtdComp: item.PercentualComissao.Value,
                QtditensPe: totalItensAtivos)
            {
                Cfop = item.Fiscal?.Cfop ?? "",
                Cst = item.Fiscal?.Cst ?? "",
                CstPis = item.Fiscal?.CstPis ?? "",
                AliqPis = item.Fiscal?.AliqPis ?? 0m,
                CstCof = item.Fiscal?.CstCof ?? "",
                AliqCof = item.Fiscal?.AliqCof ?? 0m,
                Ipi = item.Fiscal?.AliquotaIpi ?? 0m,
                Icm = item.Fiscal?.AliquotaIcm ?? 0m,
                IcmretM = item.Fiscal?.ValorIcmSt ?? 0m,
                BaseicmRt = item.Fiscal?.BaseIcmSt ?? 0m,
                Unidade = item.Fiscal?.Unidade ?? "",
                TotalIpi = totalIpi,
                BaseIcm = baseIcm,
                IcmsRet = totalSt,
                BcRetenc = bcRetenc,
                TotalIcm = totalIcm,
                TotMerc = totMerc,
                HoraIni = horaAgora,
                HoraFim = horaAgora,
                DataEnt = pedido.DataEntrega?.ToDateTime(TimeOnly.MinValue),
            };
        }).ToList();
    }
}
