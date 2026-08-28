import { BadRequestException, Body, Controller, Get, NotFoundException, Param, Post } from '@nestjs/common';
import { CacheService } from '../cache/cache.service';
import type { ClienteFisico, NegociacaoFisica, ProdutoFisico, TipoOperacaoFisico } from '../cache/cache.types';
import { ComandosService, ItemComando } from '../comandos/comandos.service';
import { NucleoService } from '../nucleo/nucleo.service';
import type { ResultadoFiscal } from '../nucleo/nucleo.types';
import type { CotarItemBody, CriarPedidoBody, DadosEntregaBody, PrecificarBody } from './pedidos.dto';

const hoje = () => new Date().toISOString().slice(0, 10);

@Controller()
export class PedidosController {
  constructor(
    private readonly cache: CacheService,
    private readonly nucleo: NucleoService,
    private readonly comandos: ComandosService,
  ) {}

  @Post('precificar')
  async precificar(@Body() body: PrecificarBody) {
    const cliente = this.cache.buscarCliente(body.codigoCliente);
    const produto = this.cache.buscarProduto(body.empresa, body.grupo, body.referencia);
    if (!cliente || !produto) throw new NotFoundException('Cliente ou produto não encontrado (ou ainda não sincronizado).');

    const tipoOperacao = body.tipoOperacao ? this.cache.buscarTipoOperacao(body.tipoOperacao) : null;

    const tetoTotal = cliente.pis + this.cache.buscarTetoColuna(body.empresa, produto.gradecol);
    const resultado = await this.nucleo.precificar({
      precoTabelaBase: produto.prcVenda,
      percentualAdicao: cliente.inss,
      compensacaoIcms: cliente.irrf,
      descontoDiretoria: cliente.iss,
      negociacao: this.negociacaoParaNucleo(body.codigoCliente, produto.grupo, produto.referencia, tipoOperacao),
      precoDigitado: body.precoDigitado,
      percentualTetoDesconto: tetoTotal,
      dataReferencia: hoje(),
    });
    // Desconto FISCAL é sempre 0 neste fluxo: o preço final já embute o
    // desconto (o VFP grava prc_venda cheio e desconto=0 — conferido em
    // pedidos reais). O percentualDesconto do resultado serve só para a
    // faixa de comissão, nunca para reduzir base de imposto.
    const fiscal = await this.calcularFiscalDoItem(
      body.empresa,
      cliente,
      produto,
      body.quantidade ?? 1,
      resultado.precoFinal,
      0,
      tipoOperacao,
    );

    return {
      produtoDescricao: produto.descricao,
      produtoGrupo: produto.grupo,
      produtoReferencia: produto.referencia,
      gradegrp: produto.gradegrp,
      unidade: produto.unidEmb,
      pesoUnitario: produto.pesoUnit,
      volumeUnitario: produto.volume,
      ...resultado,
      fiscal,
    };
  }

  /**
   * RF-075/RF-077: negociação vigente e autorizada substitui o preço — e o
   * tipo de preço vem do CFOP da operação ("E" exportação quando a natureza
   * começa com 7, senão "N"). O núcleo aplica a decisão L1 (preço-base
   * ajustado pelos fatores do cliente) e PULA o teto de desconto (RF-078,
   * fiel ao VFP).
   */
  private negociacaoParaNucleo(
    codigoCliente: string,
    grupo: string,
    referencia: string,
    tipoOperacao: TipoOperacaoFisico | null | undefined,
  ) {
    const avaliacao = this.avaliarNegociacao(codigoCliente, grupo, referencia, tipoOperacao);
    if (avaliacao.situacao === 'vencida') {
      // Decisão do usuário (25/08/2026): negociação vencida BLOQUEIA a venda do
      // produto — mais restritivo que o VFP (RF-076: só avisava e seguia).
      throw new BadRequestException(
        `Negociação de preço deste produto para o cliente venceu em ${avaliacao.dataValidade} — venda não permitida. Renove a negociação antes de vender.`,
      );
    }
    if (avaliacao.situacao !== 'vigente' || !avaliacao.registro?.dtVenc) return null;
    return { precoNegociado: avaliacao.registro.preco, dataValidade: avaliacao.registro.dtVenc.slice(0, 10), autorizada: true };
  }

  /**
   * As três guardas da negociação (RF-075/076): existe? está na vigência?
   * está autorizada (cod_valida=1)? Devolve a situação para a tela decidir
   * (preencher preço, avisar ou bloquear).
   */
  private avaliarNegociacao(
    codigoCliente: string,
    grupo: string,
    referencia: string,
    tipoOperacao: TipoOperacaoFisico | null | undefined,
  ): { situacao: 'nenhuma' | 'vigente' | 'vencida' | 'nao_autorizada'; registro: NegociacaoFisica | null; dataValidade: string } {
    const tipoPrc = (tipoOperacao?.natureza ?? '').startsWith('7') ? 'E' : 'N'; // RF-077
    const registro = this.cache.buscarNegociacaoRegistro(codigoCliente, grupo, referencia, tipoPrc);
    if (!registro || !registro.dtVenc) return { situacao: 'nenhuma', registro: null, dataValidade: '' };

    const validade = registro.dtVenc.slice(0, 10);
    const dataValidade = validade.split('-').reverse().join('/');
    if (validade < hoje()) return { situacao: 'vencida', registro, dataValidade };
    if (registro.codValida !== 1) return { situacao: 'nao_autorizada', registro, dataValidade };
    return { situacao: 'vigente', registro, dataValidade };
  }

  /**
   * Cotação do item no momento em que o produto é escolhido na tela: preço
   * de tabela já ajustado pelos fatores do cliente, e o preço negociado (se
   * houver) com o desconto que ele representa — para o campo "Valor
   * unitário" já vir preenchido certo, como no VFP (txtAddText.LostFocus).
   */
  @Post('cotar-item')
  async cotarItem(@Body() body: CotarItemBody) {
    const cliente = this.cache.buscarCliente(body.codigoCliente);
    const produto = this.cache.buscarProduto(body.empresa, body.grupo, body.referencia);
    if (!cliente || !produto) throw new NotFoundException('Cliente ou produto não encontrado (ou ainda não sincronizado).');

    const tipoOperacao = body.tipoOperacao ? this.cache.buscarTipoOperacao(body.tipoOperacao) : null;
    const negociacao = this.avaliarNegociacao(body.codigoCliente, produto.grupo, produto.referencia, tipoOperacao);

    // Preço de tabela ajustado: precifica "na tabela" (sem desconto → nunca bate no teto),
    // passando a negociação só quando vigente, para o núcleo aplicar a decisão L1.
    const cotacao = await this.nucleo.precificar({
      precoTabelaBase: produto.prcVenda,
      percentualAdicao: cliente.inss,
      compensacaoIcms: cliente.irrf,
      descontoDiretoria: cliente.iss,
      negociacao:
        negociacao.situacao === 'vigente' && negociacao.registro?.dtVenc
          ? { precoNegociado: negociacao.registro.preco, dataValidade: negociacao.registro.dtVenc.slice(0, 10), autorizada: true }
          : null,
      precoDigitado: produto.prcVenda,
      percentualTetoDesconto: 100,
      dataReferencia: hoje(),
    });

    return {
      precoTabelaAjustado: cotacao.precoTabelaAjustado,
      precoSugerido: cotacao.precoFinal,
      percentualDesconto: cotacao.percentualDesconto,
      origem: cotacao.origem,
      negociacao: {
        situacao: negociacao.situacao,
        preco: negociacao.registro?.preco ?? null,
        dataValidade: negociacao.dataValidade || null,
      },
    };
  }

  /**
   * Monta o contexto do motor fiscal (Nucleo.Api /fiscal) a partir da cópia
   * sincronizada — cadsub/cadicm resolvidos aqui (porta de pesquisacadsub/
   * veicm3), o cálculo em si roda no núcleo. Se o motor rejeitar o cenário
   * (benefício/Suframa/DIFAL, exclusões deliberadas), devolve o motivo em
   * vez do valor — a tela mostra "imposto não calculado: <motivo>", nunca um
   * zero silencioso.
   */
  private async calcularFiscalDoItem(
    empresa: string,
    cliente: ClienteFisico,
    produto: ProdutoFisico,
    quantidade: number,
    precoFinal: number,
    percentualDesconto: number,
    tipoOperacao?: TipoOperacaoFisico | null,
  ): Promise<(ResultadoFiscal & { aliquotaIpi: number; aliquotaIcm: number }) | { naoCalculado: string }> {
    const empresaInfo = this.cache.buscarEmpresa(empresa);
    if (!empresaInfo) return { naoCalculado: 'Dados da empresa ainda não sincronizados.' };

    const cadsub = this.cache.buscarCadsub(empresa, produto.grupo, produto.referencia, cliente.estado, cliente.enquadra, produto.ncm);
    const cadicm = this.cache.buscarCadicm(empresa, cliente.estado, cliente.enquadra);

    try {
      const r: ResultadoFiscal = await this.nucleo.calcularFiscal({
        quantidade,
        precoUnitario: precoFinal,
        percentualDesconto,
        aliquotaIpi: produto.ipi,
        indIpi: produto.indIpi,
        clienteEstado: cliente.estado,
        clienteCgcCpf: cliente.cgcCpf,
        clienteEnquadra: cliente.enquadra,
        clienteSuframa: cliente.suframa,
        clienteIncideSuf: cliente.incideSuf,
        clienteIncidePis: cliente.incidePis,
        clienteDesonera: cliente.desonera,
        produtoCodProc: produto.codProc,
        produtoNcm: produto.ncm,
        produtoValIcm: produto.valIcm,
        produtoValIcmE: 0,
        produtoValIcmSu: produto.valIcmSu,
        produtoDespSubst: produto.despSubst,
        produtoIcmReduc: 0,
        produtoPermIcm: produto.permIcm,
        produtoLegislacao: produto.legislacao,
        produtoTemBeneficio: produto.beneficio.trim().length > 0,
        tipoNatureza: tipoOperacao?.natureza ?? '',
        tipoPisCofins: tipoOperacao?.pisCofins ?? '',
        tipoNfcE: tipoOperacao?.nfcE ?? '',
        tipoCodContro: tipoOperacao?.codContro ?? 1,
        empresaEstado: empresaInfo.ufEmpr,
        empresaIcmEstado: empresaInfo.icmEstado,
        cadicm: cadicm
          ? {
              valIcm: cadicm.valIcm,
              valIcmE: cadicm.valIcmE,
              valIcmSu: cadicm.valIcmSu,
              subsTrib: cadicm.subsTrib,
              aliqInter: cadicm.aliqInter,
              aliqFcp: cadicm.aliqFcp,
              incideFcp: cadicm.incideFcp,
            }
          : null,
        cadsub: cadsub
          ? {
              valIcm: cadsub.valIcm,
              valIcmSu: cadsub.valIcmSu,
              icmProprio: cadsub.icmproprio,
              reducBase: cadsub.reducbase,
              prcmedio: cadsub.prcmedio,
              fcp: cadsub.fcp,
              subsTrib: cadsub.subsTrib,
            }
          : null,
      });

      return { ...r, aliquotaIpi: produto.ipi, aliquotaIcm: r.aliquotaIcmResolvida };
    } catch (e) {
      // FiscalException do núcleo (cenário deliberadamente não suportado) vira
      // um "não calculado" explícito, nunca zero.
      return { naoCalculado: (e as Error).message };
    }
  }

  @Get('credito/:codigo')
  async credito(@Param('codigo') codigo: string) {
    const cliente = this.cache.buscarCliente(codigo);
    if (!cliente) throw new NotFoundException(`Cliente ${codigo} não encontrado (ou ainda não sincronizado).`);
    if (cliente.cgc.length < 10) throw new BadRequestException('CGC do cliente muito curto para formar raiz de grupo econômico.');

    const raizCgc = cliente.cgc.slice(0, 10);
    const grupo = this.cache.buscarGrupoEconomico(raizCgc);
    const matriz = grupo.find((c) => c.posicao === 'M') ?? cliente;
    const titulos = this.cache
      .titulosAbertosDoGrupo(grupo.map((c) => c.codigo))
      .filter((t) => t.dtVencim)
      .map((t) => ({ valor: t.valor, dataVencimento: t.dtVencim as string }));

    const resultado = await this.nucleo.avaliarCredito({
      titulosAbertos: titulos,
      limiteMatriz: matriz.credito,
      diasToleranciaAtraso: cliente.cofins,
      valorPedidoAtual: 0,
      dataReferencia: hoje(),
    });

    return {
      matrizCodigo: matriz.codigo,
      matrizNome: matriz.razaoSoc,
      limite: matriz.credito,
      saldoDevedorGrupo: resultado.saldoDevedorGrupo,
      status: resultado.status,
    };
  }

  @Post('pedidos')
  async criar(@Body() body: CriarPedidoBody) {
    const cliente = this.cache.buscarCliente(body.codigoCliente);
    if (!cliente) throw new NotFoundException(`Cliente ${body.codigoCliente} não encontrado (ou ainda não sincronizado).`);

    const tipoOp = this.cache.buscarTipoOperacao(body.tipoOperacao);
    let gradegrpComum: string | null = null;
    const itensComando: ItemComando[] = [];

    for (const item of body.itens) {
      const produto = this.cache.buscarProduto(body.codigoEmpresa, item.grupo, item.referencia);
      if (!produto) throw new BadRequestException(`Produto ${item.grupo}|${item.referencia} não encontrado.`);

      if (gradegrpComum && gradegrpComum !== produto.gradegrp) {
        throw new BadRequestException(
          `Produto do grupo de negócio '${produto.gradegrp}' não pode conviver com itens do grupo '${gradegrpComum}' no mesmo pedido (RF-142).`,
        );
      }
      gradegrpComum = produto.gradegrp;

      const tetoTotal = cliente.pis + this.cache.buscarTetoColuna(body.codigoEmpresa, produto.gradecol);
      const resultadoPreco = await this.nucleo.precificar({
        precoTabelaBase: produto.prcVenda,
        percentualAdicao: cliente.inss,
        compensacaoIcms: cliente.irrf,
        descontoDiretoria: cliente.iss,
        negociacao: this.negociacaoParaNucleo(body.codigoCliente, produto.grupo, produto.referencia, tipoOp),
        precoDigitado: item.precoDigitado,
        percentualTetoDesconto: tetoTotal,
        dataReferencia: body.data,
      });

      // desconto fiscal 0 — o preço final já embute o desconto (ver nota no precificar)
      const fiscal = await this.calcularFiscalDoItem(
        body.codigoEmpresa,
        cliente,
        produto,
        item.quantidade,
        resultadoPreco.precoFinal,
        0,
        tipoOp,
      );

      // pesquisacadsub.PRG:49-73 — CST/CFOP preferem o cadsub quando existe
      // (variante ST, ex.: cst 010 / cfop 5401); fallback: cadmat.cod_proc e
      // tipos.natureza (RF-058, regime normal).
      const cadsubItem = this.cache.buscarCadsub(
        body.codigoEmpresa,
        produto.grupo,
        produto.referencia,
        cliente.estado,
        cliente.enquadra,
        produto.ncm,
      );
      const cstItem = cadsubItem?.cstIcms?.trim() ? cadsubItem.cstIcms : produto.codProc;
      const cfopItem = cadsubItem?.cfop?.trim() ? cadsubItem.cfop : (tipoOp?.natureza ?? '');

      itensComando.push({
        grupo: item.grupo,
        referencia: item.referencia,
        gradegrp: produto.gradegrp,
        quantidade: item.quantidade,
        precoTabelaAjustado: resultadoPreco.precoTabelaAjustado,
        precoFinal: resultadoPreco.precoFinal,
        percentualDesconto: resultadoPreco.percentualDesconto,
        origem: resultadoPreco.origem,
        fiscal:
          'naoCalculado' in fiscal
            ? null
            : {
                aliquotaIpi: fiscal.aliquotaIpi,
                aliquotaIcm: fiscal.aliquotaIcm,
                valorIpi: fiscal.valorIpi,
                valorIcm: fiscal.valorIcm,
                valorIcmSt: fiscal.valorIcmSt,
                baseIcm: fiscal.baseIcm,
                baseIcmSt: fiscal.baseIcmSt,
                valorMercadoria: fiscal.valorMercadoria,
                cst: cstItem,
                cfop: cfopItem,
                cstPis: produto.cstPis,
                aliqPis: produto.aliqPis,
                cstCof: produto.cstCof,
                aliqCof: produto.aliqCof,
                unidade: produto.unidEmb,
              },
      });
    }

    const vendedor = this.cache.buscarVendedor(body.vendedorCodigo1);
    const tipoVendedorParaComissao = vendedor?.tipoVend ?? 'V';

    // Numeração: a regra real dos prefixos ainda não foi respondida pelo
    // negócio (§9-Q4 dos documentos) — placeholder reconhecível, não a
    // numeração definitiva. `codigo` em pedido.dbf é C(10).
    const referenciaExterna = `WB${Date.now().toString().slice(-8)}`;

    const comando = this.comandos.criar({
      tipoOperacao: body.tipoOperacao,
      codigoEmpresa: body.codigoEmpresa,
      codigoCliente: body.codigoCliente,
      data: body.data,
      dataEntrega: body.dataEntrega,
      autor: body.autor,
      referenciaExterna,
      condicaoPagamentoCodigo: body.condicaoPagamentoCodigo,
      vendedorCodigo1: body.vendedorCodigo1,
      vendedorCodigo2: body.vendedorCodigo2,
      tipoVendedorParaComissao,
      itens: itensComando,
    });

    return { comandoId: comando.id, status: comando.status, referenciaExterna: comando.referenciaExterna };
  }

  /**
   * "Dados Para Entrega" (tela pós-gravação, modelo WE) → `cligeral.dbf`,
   * como o form ped_ph do ERP faz. Vira um segundo comando para o console,
   * porque o pedido já foi gravado quando a tela aparece.
   */
  @Post('pedidos/:comandoId/entrega')
  gravarEntrega(@Param('comandoId') comandoId: string, @Body() body: DadosEntregaBody) {
    const original = this.comandos.buscarPorId(comandoId);
    if (original.status !== 'Gravado') {
      throw new BadRequestException('Os dados de entrega só podem ser gravados depois que o pedido estiver gravado.');
    }
    const comando = this.comandos.criar({
      tipo: 'GravarEntrega',
      tipoOperacao: original.tipoOperacao,
      codigoEmpresa: original.codigoEmpresa,
      codigoCliente: original.codigoCliente,
      data: original.data,
      dataEntrega: original.dataEntrega,
      autor: original.autor,
      referenciaExterna: original.referenciaExterna,
      condicaoPagamentoCodigo: original.condicaoPagamentoCodigo,
      vendedorCodigo1: original.vendedorCodigo1,
      vendedorCodigo2: original.vendedorCodigo2,
      tipoVendedorParaComissao: original.tipoVendedorParaComissao,
      itens: [],
      entrega: { ...body },
    });
    return { comandoId: comando.id, status: comando.status };
  }

  @Get('pedidos/:comandoId/status')
  status(@Param('comandoId') comandoId: string) {
    const comando = this.comandos.buscarPorId(comandoId);
    return { status: comando.status, referenciaExterna: comando.referenciaExterna, erro: comando.erro };
  }
}
