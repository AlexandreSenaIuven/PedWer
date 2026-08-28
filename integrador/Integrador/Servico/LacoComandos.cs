using Dominio;
using Integrador.Escrita;
using MotorRegras;

namespace Integrador.Servico;

/// <summary>
/// Busca (poll) comandos pendentes na API central e grava no VFP — nunca o
/// contrário. Reconstrói o mesmo `Dominio.Pedido` já testado a partir do
/// payload (que já chega com preço e desconto decididos pelo núcleo de
/// regras central) e reaproveita `MapeadorPedidoParaVfp` +
/// `PedidoDbfRepositorio` sem alteração — só troca de onde o payload vem.
/// `Pedido.Fechar` recalcula a comissão aqui de novo, com os mesmos dados
/// (tipo de vendedor + % desconto) que a central já usou — é a mesma função
/// pura, então o resultado é idêntico; é uma reconfirmação, não confiança
/// cega no que chegou pela rede.
/// </summary>
public sealed class LacoComandos(ApiCentralCliente api, TimeSpan intervalo)
{
    public async Task ExecutarParaSempreAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessarPendentesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] busca de comandos falhou: {ex.Message}");
            }

            await Task.Delay(intervalo, ct);
        }
    }

    public async Task ProcessarPendentesAsync()
    {
        var pendentes = await api.BuscarComandosPendentesAsync();
        foreach (var comando in pendentes)
        {
            await ProcessarUmAsync(comando);
        }
    }

    private async Task ProcessarUmAsync(ComandoPendenteDto comando)
    {
        try
        {
            if (string.Equals(comando.Tipo, "GravarEntrega", StringComparison.OrdinalIgnoreCase))
            {
                // Segundo comando do fluxo: "Dados Para Entrega" → cligeral.dbf (o pedido já existe).
                new CligeralRepositorio().Gravar(comando.ReferenciaExterna, comando.Entrega ?? new EntregaComandoDto(null, null, null, null, null, null, null));
                await api.ReportarResultadoAsync(comando.Id, new ResultadoComandoRequest(true, comando.ReferenciaExterna, null));
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] comando {comando.Id} dados de entrega gravados em cligeral ({comando.ReferenciaExterna}).");
                return;
            }

            var pedido = new Pedido(comando.TipoOperacao, comando.CodigoEmpresa, comando.CodigoCliente, DateOnly.Parse(comando.Data), comando.Autor)
            {
                ReferenciaExterna = comando.ReferenciaExterna,
                CondicaoPagamentoCodigo = comando.CondicaoPagamentoCodigo,
                DataEntrega = string.IsNullOrWhiteSpace(comando.DataEntrega) ? null : DateOnly.Parse(comando.DataEntrega),
            };

            foreach (var item in comando.Itens)
            {
                var origem = Enum.Parse<OrigemPreco>(item.Origem);
                var resultadoPreco = new ResultadoPrecoItem(item.PrecoTabelaAjustado, item.PrecoFinal, item.PercentualDesconto, origem);
                var itemPedido = pedido.AdicionarItem(item.Grupo, item.Referencia, item.Gradegrp, item.Quantidade, resultadoPreco);

                if (item.Fiscal is not null)
                {
                    itemPedido.Fiscal = new FiscalItemPedido(
                        item.Fiscal.AliquotaIpi,
                        item.Fiscal.AliquotaIcm,
                        item.Fiscal.ValorIpi,
                        item.Fiscal.ValorIcm,
                        item.Fiscal.ValorIcmSt,
                        item.Fiscal.BaseIcm,
                        item.Fiscal.BaseIcmSt,
                        item.Fiscal.ValorMercadoria,
                        item.Fiscal.Cst,
                        item.Fiscal.Cfop,
                        item.Fiscal.CstPis,
                        item.Fiscal.AliqPis,
                        item.Fiscal.CstCof,
                        item.Fiscal.AliqCof,
                        item.Fiscal.Unidade);
                }
            }

            pedido.Fechar(comando.VendedorCodigo1, comando.VendedorCodigo2, comando.TipoVendedorParaComissao);

            var linhas = MapeadorPedidoParaVfp.Mapear(pedido, esMov: "S", comprador: comando.Autor);
            new PedidoDbfRepositorio().GravarPedido(linhas);

            await api.ReportarResultadoAsync(comando.Id, new ResultadoComandoRequest(true, pedido.ReferenciaExterna, null));
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] comando {comando.Id} gravado ({pedido.ReferenciaExterna}).");
        }
        catch (Exception ex)
        {
            await api.ReportarResultadoAsync(comando.Id, new ResultadoComandoRequest(false, null, ex.Message));
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] comando {comando.Id} falhou: {ex.Message}");
        }
    }
}
