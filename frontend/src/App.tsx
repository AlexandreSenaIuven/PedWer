import { Fragment, useEffect, useState } from 'react'
import './App.css'
import {
  api,
  type ClienteResumo,
  type CotacaoItem,
  type EmpresaCompleta,
  type ItemCompra,
  type ItemGiro,
  type ProdutoResumo,
  type ReferenciaSimples,
  type ResultadoCredito,
  type SaldoEmpresa,
  type UsuarioLogado,
} from './api'
import { Breadcrumb } from './components/Breadcrumb'
import { BuscaModal } from './components/BuscaModal'
import { CampoBusca } from './components/CampoBusca'
import { Login } from './components/Login'
import { TopBar } from './components/TopBar'
import { DialogoEntrega } from './components/DialogoEntrega'
import { Giro } from './components/Giro'
import { SaldoGeral } from './components/SaldoGeral'
import { UltimasCompras } from './components/UltimasCompras'
import { VisualizadorPdf } from './components/VisualizadorPdf'
// O modelo PE (impressaoPedido.gerarPdfPedido) fica no código, mas fora da
// UI por decisão do usuário (24/08/2026): por ora só o modelo WE é usado.
import type { DadosImpressaoPedido } from './impressaoPedido'
import { gerarBytesPdfPedidoWe, salvarPdfPedidoWe, type DadosEntregaWe } from './impressaoPedidoWe'
import type { EstadoPedido, ItemPedido } from './tipos'

// Fallback até a primeira sincronização de `tabplan` chegar — substituído
// pelos nomes reais via GET /empresas assim que disponíveis.
const EMPRESAS_FALLBACK = [
  { codigo: '02', nome: 'Empresa 02' },
  { codigo: '03', nome: 'Empresa 03' },
]

// "Estoque principal" (RF-021/wc_empr vazio) — não vem do tabplan, é o
// cadmat/cadsub/cadicm da RAIZ da base. Ao fechar o pedido, este código vira
// cod_empr EM BRANCO (tradução acontece só no console, ver VfpConexao.CodigoPrincipal).
const CODIGO_PRINCIPAL = 'PRINCIPAL'
const EMPRESA_PRINCIPAL = { codigo: CODIGO_PRINCIPAL, nome: 'PRINCIPAL (estoque principal)' }

const CHAVE_USUARIO_ARMAZENADO = 'pedwerweb.usuario'
const CHAVE_EMPRESA_ARMAZENADA = 'pedwerweb.empresa'

function usuarioArmazenado(): UsuarioLogado | null {
  try {
    const bruto = localStorage.getItem(CHAVE_USUARIO_ARMAZENADO)
    return bruto ? (JSON.parse(bruto) as UsuarioLogado) : null
  } catch {
    return null
  }
}

function empresaArmazenada(): string | null {
  try {
    return localStorage.getItem(CHAVE_EMPRESA_ARMAZENADA)
  } catch {
    return null
  }
}

function App() {
  const [conectado, setConectado] = useState<boolean | null>(null)
  const [usuario, setUsuario] = useState<UsuarioLogado | null>(usuarioArmazenado)
  const [empresas, setEmpresas] = useState(EMPRESAS_FALLBACK)
  const [empresasCompletas, setEmpresasCompletas] = useState<EmpresaCompleta[]>([])
  const [empresa, setEmpresa] = useState(() => empresaArmazenada() ?? EMPRESAS_FALLBACK[0].codigo)
  const empresasComPrincipal = [...empresas, EMPRESA_PRINCIPAL]
  const [impressao, setImpressao] = useState<DadosImpressaoPedido | null>(null)
  const [entregaAberta, setEntregaAberta] = useState(false)
  const [pdfVisualizacao, setPdfVisualizacao] = useState<{ bytes: ArrayBuffer; entrega: DadosEntregaWe } | null>(null)

  const [estadoPedido, setEstadoPedido] = useState<EstadoPedido>('Rascunho')
  const [data, setData] = useState(new Date().toISOString().slice(0, 10))
  const [dataEntrega, setDataEntrega] = useState(new Date().toISOString().slice(0, 10)) // pedido.data_ent — "PRAZO DE ENTREGA" no formulário
  const [tipoOperacao, setTipoOperacao] = useState<ReferenciaSimples | null>(null)
  const [tipoBuscaAberta, setTipoBuscaAberta] = useState(false)
  const [cliente, setCliente] = useState<ClienteResumo | null>(null)
  const [condicaoPagamento, setCondicaoPagamento] = useState<ReferenciaSimples | null>(null)
  const [condicaoBuscaAberta, setCondicaoBuscaAberta] = useState(false)
  const [clienteBuscaAberta, setClienteBuscaAberta] = useState(false)

  const [itens, setItens] = useState<ItemPedido[]>([])
  const [linhaNegocioGrupo, setLinhaNegocioGrupo] = useState<string | null>(null)
  const [itemEditando, setItemEditando] = useState<number | null>(null)
  const [edQuantidade, setEdQuantidade] = useState(0)
  const [edPreco, setEdPreco] = useState(0)
  const [edDesconto, setEdDesconto] = useState(0)
  const [edCarregando, setEdCarregando] = useState(false)
  const [edErro, setEdErro] = useState<string | null>(null)
  const [credito, setCredito] = useState<ResultadoCredito | null>(null)
  const [ultimasComprasAberta, setUltimasComprasAberta] = useState(false)
  const [ultimasComprasCarregando, setUltimasComprasCarregando] = useState(false)
  const [ultimasComprasErro, setUltimasComprasErro] = useState<string | null>(null)
  const [ultimasCompras, setUltimasCompras] = useState<ItemCompra[] | null>(null)

  const [giroAberto, setGiroAberto] = useState(false)
  const [giroProdutoNome, setGiroProdutoNome] = useState('')
  const [giroCarregando, setGiroCarregando] = useState(false)
  const [giroErro, setGiroErro] = useState<string | null>(null)
  const [giro, setGiro] = useState<ItemGiro[] | null>(null)

  const [saldoGeralAberto, setSaldoGeralAberto] = useState(false)
  const [saldoGeralProdutoNome, setSaldoGeralProdutoNome] = useState('')
  const [saldoGeralCarregando, setSaldoGeralCarregando] = useState(false)
  const [saldoGeralErro, setSaldoGeralErro] = useState<string | null>(null)
  const [saldoGeral, setSaldoGeral] = useState<SaldoEmpresa[] | null>(null)

  const [produtoItem, setProdutoItem] = useState<ProdutoResumo | null>(null)
  const [produtoBuscaAberta, setProdutoBuscaAberta] = useState(false)
  const [quantidade, setQuantidade] = useState(1)
  const [precoDigitado, setPrecoDigitado] = useState(0)
  const [descPercentual, setDescPercentual] = useState(0)
  const [cotacao, setCotacao] = useState<CotacaoItem | null>(null)
  const [erroItem, setErroItem] = useState<string | null>(null)
  const [carregandoItem, setCarregandoItem] = useState(false)

  const [vendedor1, setVendedor1] = useState<ReferenciaSimples | null>(null)
  const [vendedor2, setVendedor2] = useState<ReferenciaSimples | null>(null)
  const [vendedorBuscaAlvo, setVendedorBuscaAlvo] = useState<1 | 2 | null>(null)
  const [referenciaFechada, setReferenciaFechada] = useState<string | null>(null)
  const [comandoIdAtual, setComandoIdAtual] = useState<string | null>(null)
  const [erroFechamento, setErroFechamento] = useState<string | null>(null)

  useEffect(() => {
    api
      .buscarClientes()
      .then(() => setConectado(true))
      .catch(() => setConectado(false))
    // "PED" (Pedido de Venda) como ponto de partida — o usuário pode trocar pela busca.
    api
      .buscarTiposOperacao('PED')
      .then((lista) => setTipoOperacao(lista.find((t) => t.codigo === 'PED') ?? lista[0] ?? null))
      .catch(() => {})
    api
      .listarEmpresas()
      .then((lista) => {
        // empresas sem UF (01 na base) não têm dado fiscal aproveitável — mostra só as com UF
        const validas = lista.filter((e) => e.uf)
        if (validas.length > 0) {
          setEmpresas(validas.map((e) => ({ codigo: e.codigo, nome: e.nome })))
          setEmpresasCompletas(validas)
        }
      })
      .catch(() => {})
  }, [])

  useEffect(() => {
    if (!cliente) {
      setCredito(null)
      return
    }
    if (cliente.vendedorCodigo1) {
      api
        .buscarVendedor(cliente.vendedorCodigo1)
        .then((v) => setVendedor1({ codigo: v.codigo, descricao: v.nome }))
        .catch(() => setVendedor1({ codigo: cliente.vendedorCodigo1, descricao: '' }))
    } else {
      setVendedor1(null)
    }
    if (cliente.vendedorCodigo2) {
      api
        .buscarVendedor(cliente.vendedorCodigo2)
        .then((v) => setVendedor2({ codigo: v.codigo, descricao: v.nome }))
        .catch(() => setVendedor2({ codigo: cliente.vendedorCodigo2, descricao: '' }))
    } else {
      setVendedor2(null)
    }
    if (cliente.condicaoPagamentoCodigo) {
      api
        .buscarCondicoesPagamento(cliente.condicaoPagamentoCodigo)
        .then((lista) => setCondicaoPagamento(lista.find((c) => c.codigo === cliente.condicaoPagamentoCodigo) ?? null))
        .catch(() => {})
    }
    api.consultarCredito(cliente.codigo).then(setCredito).catch(() => setCredito(null))
  }, [cliente])

  const cabecalhoSelado = itens.length > 0 // RF-023: cabeçalho trava depois do primeiro item
  const totalNota = itens.reduce((soma, i) => soma + i.precoFinal * i.quantidade, 0)
  const totalIpi = itens.reduce((soma, i) => soma + (i.valorIpi ?? 0), 0)
  const totalSt = itens.reduce((soma, i) => soma + (i.valorIcmSt ?? 0), 0)
  const totalImpostos = totalIpi + totalSt
  const itensSemFiscal = itens.filter((i) => i.fiscalNaoCalculado)

  // Cotação na escolha do produto (como o VFP em txtAddText.LostFocus): preço
  // de tabela já ajustado ao cliente; se houver negociação vigente, o preço e
  // o % de desconto vêm dela; vencida → alerta e bloqueia o item.
  async function selecionarProduto(produto: ProdutoResumo) {
    setProdutoItem(produto)
    setErroItem(null)
    setCotacao(null)
    setDescPercentual(0)
    setPrecoDigitado(produto.precoTabela)

    if (!cliente) {
      setErroItem('Escolha o cliente antes do produto — o preço depende do cadastro dele.')
      return
    }

    try {
      const c = await api.cotarItem({
        empresa,
        codigoCliente: cliente.codigo,
        grupo: produto.grupo,
        referencia: produto.referencia,
        tipoOperacao: tipoOperacao?.codigo,
      })
      setCotacao(c)
      setPrecoDigitado(c.precoSugerido)
      setDescPercentual(Math.round(c.percentualDesconto * 100) / 100)
      if (c.negociacao.situacao === 'vencida') {
        setErroItem(`Negociação de preço deste produto venceu em ${c.negociacao.dataValidade} — venda não permitida. Renove a negociação antes de vender.`)
      }
    } catch (e) {
      setErroItem((e as Error).message)
    }
  }

  const precoTabelaAtual = cotacao?.precoTabelaAjustado ?? produtoItem?.precoTabela ?? 0
  const negociacaoVigente = cotacao?.negociacao.situacao === 'vigente'
  const negociacaoVencida = cotacao?.negociacao.situacao === 'vencida'

  function alterarPreco(preco: number) {
    setPrecoDigitado(preco)
    if (precoTabelaAtual > 0) {
      setDescPercentual(Math.round(((precoTabelaAtual - preco) / precoTabelaAtual) * 100 * 100) / 100)
    }
  }

  async function adicionarItem() {
    setErroItem(null)
    if (!cliente) {
      setErroItem('Escolha o cliente antes de adicionar itens.')
      return
    }
    if (!produtoItem) {
      setErroItem('Escolha um produto.')
      return
    }
    if (quantidade <= 0) {
      setErroItem('Quantidade deve ser maior que zero (RF-088).')
      return
    }
    if (negociacaoVencida) {
      setErroItem(`Negociação de preço deste produto venceu em ${cotacao?.negociacao.dataValidade} — venda não permitida.`)
      return
    }
    if (linhaNegocioGrupo && linhaNegocioGrupo !== produtoItem.gradegrp) {
      setErroItem(`Produto do grupo de negócio '${produtoItem.gradegrp}' não pode conviver com itens do grupo '${linhaNegocioGrupo}' no mesmo pedido (RF-142).`)
      return
    }

    setCarregandoItem(true)
    try {
      const resultado = await api.precificar({
        empresa,
        codigoCliente: cliente.codigo,
        grupo: produtoItem.grupo,
        referencia: produtoItem.referencia,
        precoDigitado,
        quantidade,
        tipoOperacao: tipoOperacao?.codigo,
      })
      const fiscalOk = 'valorIpi' in resultado.fiscal ? resultado.fiscal : null
      const novoItem: ItemPedido = {
        numero: itens.length + 1,
        produtoGrupo: resultado.produtoGrupo,
        produtoReferencia: resultado.produtoReferencia,
        produtoDescricao: resultado.produtoDescricao,
        quantidade,
        precoTabelaAjustado: resultado.precoTabelaAjustado,
        precoFinal: resultado.precoFinal,
        percentualDesconto: resultado.percentualDesconto,
        origemPreco: resultado.origemPreco,
        unidade: resultado.unidade,
        pesoUnitario: resultado.pesoUnitario,
        volumeUnitario: resultado.volumeUnitario,
        valorIpi: fiscalOk ? fiscalOk.valorIpi : null,
        valorIcmSt: fiscalOk ? fiscalOk.valorIcmSt : null,
        aliquotaIpi: fiscalOk ? fiscalOk.aliquotaIpi : null,
        aliquotaIcm: fiscalOk ? fiscalOk.aliquotaIcm : null,
        fiscalNaoCalculado: 'naoCalculado' in resultado.fiscal ? resultado.fiscal.naoCalculado : undefined,
      }
      setItens([...itens, novoItem])
      setLinhaNegocioGrupo(resultado.gradegrp)
      setProdutoItem(null)
      setCotacao(null)
      setQuantidade(1)
      setDescPercentual(0)
    } catch (e) {
      setErroItem((e as Error).message)
    } finally {
      setCarregandoItem(false)
    }
  }

  function excluirItem(numero: number) {
    const restantes = itens.filter((i) => i.numero !== numero)
    setItens(restantes)
    if (restantes.length === 0) setLinhaNegocioGrupo(null)
  }

  // Botão "Últimas Compras" do ped_wer.scx original — consulta sob demanda
  // (fila de comando, mesmo padrão de criar pedido), nunca um dado
  // sincronizado (vem de `cadmov`, 900k+ linhas por empresa).
  async function abrirUltimasCompras() {
    if (!cliente) return
    setUltimasComprasAberta(true)
    setUltimasComprasCarregando(true)
    setUltimasComprasErro(null)
    setUltimasCompras(null)
    try {
      const { comandoId } = await api.consultarUltimasCompras(empresa, cliente.codigo)
      const intervalo = setInterval(async () => {
        const status = await api.statusUltimasCompras(comandoId)
        if (status.status === 'Gravado') {
          clearInterval(intervalo)
          setUltimasCompras(status.ultimasCompras ?? [])
          setUltimasComprasCarregando(false)
        } else if (status.status === 'Erro') {
          clearInterval(intervalo)
          setUltimasComprasErro(status.erro ?? 'Não foi possível consultar as últimas compras.')
          setUltimasComprasCarregando(false)
        }
      }, 1000)
    } catch (e) {
      setUltimasComprasErro((e as Error).message)
      setUltimasComprasCarregando(false)
    }
  }

  // Botão "Giro" — mesmo padrão de "Últimas Compras", filtrado por produto
  // em vez de cliente (RF §"botões de Giro e de Saldo Geral", 02/09/2026).
  async function abrirGiro(produto: ProdutoResumo) {
    setGiroProdutoNome(`${produto.grupo}|${produto.referencia} — ${produto.descricao}`)
    setGiroAberto(true)
    setGiroCarregando(true)
    setGiroErro(null)
    setGiro(null)
    try {
      const { comandoId } = await api.consultarGiro(empresa, produto.grupo, produto.referencia)
      const intervalo = setInterval(async () => {
        const status = await api.statusGiro(comandoId)
        if (status.status === 'Gravado') {
          clearInterval(intervalo)
          setGiro(status.giro ?? [])
          setGiroCarregando(false)
        } else if (status.status === 'Erro') {
          clearInterval(intervalo)
          setGiroErro(status.erro ?? 'Não foi possível consultar o giro.')
          setGiroCarregando(false)
        }
      }, 1000)
    } catch (e) {
      setGiroErro((e as Error).message)
      setGiroCarregando(false)
    }
  }

  // Botão "Saldo Geral" — saldo do produto em cada empresa real (nunca a
  // "PRINCIPAL" sintética, por isso `empresas` e não `empresasComPrincipal`).
  async function abrirSaldoGeral(produto: ProdutoResumo) {
    setSaldoGeralProdutoNome(`${produto.grupo}|${produto.referencia} — ${produto.descricao}`)
    setSaldoGeralAberto(true)
    setSaldoGeralCarregando(true)
    setSaldoGeralErro(null)
    setSaldoGeral(null)
    try {
      const { comandoId } = await api.consultarSaldoGeral(
        empresa,
        produto.grupo,
        produto.referencia,
        empresas.map((emp) => emp.codigo),
      )
      const intervalo = setInterval(async () => {
        const status = await api.statusSaldoGeral(comandoId)
        if (status.status === 'Gravado') {
          clearInterval(intervalo)
          setSaldoGeral(status.saldoGeral ?? [])
          setSaldoGeralCarregando(false)
        } else if (status.status === 'Erro') {
          clearInterval(intervalo)
          setSaldoGeralErro(status.erro ?? 'Não foi possível consultar o saldo geral.')
          setSaldoGeralCarregando(false)
        }
      }, 1000)
    } catch (e) {
      setSaldoGeralErro((e as Error).message)
      setSaldoGeralCarregando(false)
    }
  }

  function iniciarEdicaoItem(item: ItemPedido) {
    setItemEditando(item.numero)
    setEdQuantidade(item.quantidade)
    setEdPreco(item.precoFinal)
    setEdDesconto(item.percentualDesconto)
    setEdErro(null)
  }

  function cancelarEdicaoItem() {
    setItemEditando(null)
    setEdErro(null)
  }

  // Preço ↔ desconto% têm mão dupla, igual na inclusão do item — usando o
  // preço de tabela já ajustado que ficou gravado no item (não precisa
  // recotar cliente+produto de novo, já foi feito quando o item entrou).
  function edAlterarPreco(preco: number) {
    setEdPreco(preco)
    const tabela = itens.find((i) => i.numero === itemEditando)?.precoTabelaAjustado ?? 0
    if (tabela > 0) setEdDesconto(Math.round(((tabela - preco) / tabela) * 100 * 100) / 100)
  }

  // Recalcula preço final e fiscal (IPI/ICMS-ST) contra a API — nunca só
  // aritmética local, porque as regras de teto de desconto, negociação e
  // motor fiscal (veicm3/totitem2) vivem lá, não aqui.
  async function salvarEdicaoItem() {
    const item = itens.find((i) => i.numero === itemEditando)
    if (!item || !cliente) return
    if (edQuantidade <= 0) {
      setEdErro('Quantidade deve ser maior que zero (RF-088).')
      return
    }
    setEdCarregando(true)
    setEdErro(null)
    try {
      const resultado = await api.precificar({
        empresa,
        codigoCliente: cliente.codigo,
        grupo: item.produtoGrupo,
        referencia: item.produtoReferencia,
        precoDigitado: edPreco,
        quantidade: edQuantidade,
        tipoOperacao: tipoOperacao?.codigo,
      })
      const fiscalOk = 'valorIpi' in resultado.fiscal ? resultado.fiscal : null
      setItens(
        itens.map((i) =>
          i.numero !== item.numero
            ? i
            : {
                ...i,
                quantidade: edQuantidade,
                precoTabelaAjustado: resultado.precoTabelaAjustado,
                precoFinal: resultado.precoFinal,
                percentualDesconto: resultado.percentualDesconto,
                origemPreco: resultado.origemPreco,
                valorIpi: fiscalOk ? fiscalOk.valorIpi : null,
                valorIcmSt: fiscalOk ? fiscalOk.valorIcmSt : null,
                aliquotaIpi: fiscalOk ? fiscalOk.aliquotaIpi : null,
                aliquotaIcm: fiscalOk ? fiscalOk.aliquotaIcm : null,
                fiscalNaoCalculado: 'naoCalculado' in resultado.fiscal ? resultado.fiscal.naoCalculado : undefined,
              },
        ),
      )
      setItemEditando(null)
    } catch (e) {
      setEdErro((e as Error).message)
    } finally {
      setEdCarregando(false)
    }
  }

  async function fecharPedido() {
    if (!cliente || !tipoOperacao) return
    setErroFechamento(null)
    try {
      const resultado = await api.criarPedido({
        tipoOperacao: tipoOperacao.codigo,
        codigoEmpresa: empresa,
        codigoCliente: cliente.codigo,
        data,
        dataEntrega,
        autor: 'demo-web',
        condicaoPagamentoCodigo: condicaoPagamento?.codigo,
        vendedorCodigo1: vendedor1?.codigo ?? '',
        vendedorCodigo2: vendedor2?.codigo ?? '',
        itens: itens.map((i) => ({
          grupo: i.produtoGrupo,
          referencia: i.produtoReferencia,
          quantidade: i.quantidade,
          precoDigitado: i.precoFinal,
        })),
      })
      setReferenciaFechada(resultado.referenciaExterna)
      setComandoIdAtual(resultado.comandoId)
      setEstadoPedido('Processando')
      aguardarConfirmacao(resultado.comandoId)
    } catch (e) {
      setErroFechamento((e as Error).message)
    }
  }

  // O console nunca recebe conexão — só busca comandos pendentes de tempos em
  // tempos (hoje a cada 2s). Por isso a tela pergunta "já foi gravado?" em
  // vez de receber a confirmação na hora.
  function aguardarConfirmacao(comandoId: string) {
    const intervalo = setInterval(async () => {
      const status = await api.statusPedido(comandoId)
      if (status.status === 'Gravado') {
        clearInterval(intervalo)
        await montarImpressao(status.referenciaExterna)
        setEstadoPedido('Fechado')
        // Abre direto a tela de entrega (Salvar/Imprimir, modelo WE) — a
        // tela só volta para um novo pedido depois que o usuário escolher.
        setEntregaAberta(true)
      } else if (status.status === 'Erro') {
        clearInterval(intervalo)
        setEstadoPedido('Rascunho')
        setErroFechamento(status.erro ?? 'O console não conseguiu gravar o pedido.')
      }
    }, 1000)
  }

  // "Dados Para Entrega" → cligeral.dbf, via segundo comando ao console. Não
  // bloqueia a impressão; falha só é registrada no console do navegador.
  function gravarDadosEntrega(dados: DadosEntregaWe) {
    if (!comandoIdAtual) return
    api.gravarEntrega(comandoIdAtual, dados).catch((e) => console.error('Falha ao gravar dados de entrega:', e))
  }

  // Congela tudo que o layout "mod. PE" precisa ANTES de a tela limpar.
  async function montarImpressao(referencia: string) {
    if (!cliente) return
    const empresaInfo = empresasCompletas.find((e) => e.codigo === empresa)
    const agora = new Date()

    setImpressao({
      empresa: {
        nome: empresaInfo?.nome ?? '',
        endereco: empresaInfo?.endereco ?? '',
        bairro: empresaInfo?.bairro ?? '',
        cep: empresaInfo?.cep ?? '',
        cidade: empresaInfo?.cidade ?? '',
        uf: empresaInfo?.uf ?? '',
        cnpj: empresaInfo?.cnpj ?? '',
        inscricaoEstadual: empresaInfo?.inscricaoEstadual ?? '',
        telefone: empresaInfo?.telefone ?? '',
        fax: empresaInfo?.fax ?? '',
        email: empresaInfo?.email ?? '',
      },
      referencia,
      dataEmissao: new Date(data + 'T00:00:00').toLocaleDateString('pt-BR'),
      dataEntrega: dataEntrega ? new Date(dataEntrega + 'T00:00:00').toLocaleDateString('pt-BR') : '',
      hora: agora.toLocaleTimeString('pt-BR'),
      tipoOperacao: tipoOperacao?.codigo ?? '',
      vendedor1Nome: vendedor1?.descricao ?? '',
      vendedor2Nome: vendedor2?.descricao ?? '',
      cliente: {
        codigo: cliente.codigo,
        nome: cliente.nome,
        comprador: cliente.comprador,
        endereco: cliente.endereco,
        bairro: cliente.bairro,
        tipoCliente: cliente.tipoCliente,
        cidade: cliente.cidade,
        estado: cliente.estado,
        cep: cliente.cep,
        cnpj: cliente.cnpj,
        telefone: cliente.telefone,
        inscricaoEstadual: cliente.inscricaoEstadual,
      },
      condicaoPagamento: condicaoPagamento ? `${condicaoPagamento.codigo} — ${condicaoPagamento.descricao}` : '',
      itens: itens.map((i) => ({
        codigo: `${i.produtoGrupo}${i.produtoReferencia}`,
        quantidade: i.quantidade,
        unidade: i.unidade,
        descricao: i.produtoDescricao,
        precoUnitario: i.precoFinal,
        precoTotal: i.precoFinal * i.quantidade,
        percentualDesconto: i.percentualDesconto,
        aliquotaIpi: i.aliquotaIpi,
        aliquotaIcm: i.aliquotaIcm,
        valorIpi: i.valorIpi,
        valorIcmSt: i.valorIcmSt,
        pesoUnitario: i.pesoUnitario,
        volumeUnitario: i.volumeUnitario,
      })),
      totalProdutos: totalNota,
      desconto: 0,
      impostos: totalImpostos,
      totalPedido: totalNota + totalImpostos,
    })
  }

  function novoPedido() {
    setEstadoPedido('Rascunho')
    setData(new Date().toISOString().slice(0, 10))
    setDataEntrega(new Date().toISOString().slice(0, 10))
    setItens([])
    setLinhaNegocioGrupo(null)
    setReferenciaFechada(null)
    setErroFechamento(null)
    setCliente(null)
    setCondicaoPagamento(null)
    setVendedor1(null)
    setVendedor2(null)
    setProdutoItem(null)
    setQuantidade(1)
    setPrecoDigitado(0)
    setDescPercentual(0)
    setErroItem(null)
    setImpressao(null)
    setEntregaAberta(false)
    setPdfVisualizacao(null)
    setCotacao(null)
  }

  if (conectado === null) {
    return <div className="pagina-carregando">Conectando à API...</div>
  }

  if (conectado === false) {
    return (
      <div className="pagina-carregando">
        <div className="faixa-erro">
          Não foi possível conectar à API. Ela está rodando?
        </div>
      </div>
    )
  }

  if (!usuario) {
    return (
      <Login
        empresas={empresasComPrincipal}
        onEntrar={(logado, empresaCodigo) => {
          localStorage.setItem(CHAVE_USUARIO_ARMAZENADO, JSON.stringify(logado))
          localStorage.setItem(CHAVE_EMPRESA_ARMAZENADA, empresaCodigo)
          setUsuario(logado)
          setEmpresa(empresaCodigo)
        }}
      />
    )
  }

  return (
    <div className="app">
      <TopBar
        empresas={empresasComPrincipal}
        empresaSelecionada={empresa}
        usuario={usuario}
        onSair={() => {
          localStorage.removeItem(CHAVE_USUARIO_ARMAZENADO)
          setUsuario(null)
        }}
      />

      <main className="conteudo">
        <Breadcrumb itens={['Vendas', 'Pedidos', referenciaFechada ?? 'Novo pedido']} />

        <div className="cabecalho-titulo">
          <h1>{referenciaFechada ? `Pedido ${referenciaFechada}` : 'Novo pedido de venda'}</h1>
          <span className="badge">
            {estadoPedido === 'Rascunho' ? 'Em edição' : estadoPedido === 'Processando' ? 'Processando' : 'Fechado'}
          </span>
        </div>
        <p className="meta">Emissão {new Date(data + 'T00:00:00').toLocaleDateString('pt-BR')}</p>

        <div className="layout">
          <div className="coluna-principal">
            <section className="cartao">
              <h2>Dados do pedido {cabecalhoSelado && <span className="selo">selado (RF-023)</span>}</h2>
              <div className="grade-campos grade-cabecalho">
                <label className="campo-tipo">
                  Tipo
                  <CampoBusca
                    valor={tipoOperacao ? `${tipoOperacao.codigo} — ${tipoOperacao.descricao}` : ''}
                    placeholder="Buscar tipo..."
                    disabled={cabecalhoSelado}
                    onAbrir={() => setTipoBuscaAberta(true)}
                  />
                </label>
                <label className="campo-cliente">
                  Cliente *
                  <CampoBusca
                    valor={cliente ? `${cliente.codigo} — ${cliente.nome}` : ''}
                    placeholder="Buscar por código ou nome..."
                    disabled={cabecalhoSelado}
                    onAbrir={() => setClienteBuscaAberta(true)}
                  />
                </label>
                <label>
                  Data
                  <input type="date" disabled={cabecalhoSelado} value={data} onChange={(e) => setData(e.target.value)} />
                </label>
                <label>
                  Entrega prevista
                  <input type="date" value={dataEntrega} onChange={(e) => setDataEntrega(e.target.value)} />
                </label>
              </div>
              {credito && (
                <p className={`credito credito-${credito.status}`}>
                  Crédito do grupo ({credito.matrizNome}): limite {credito.limite.toFixed(2)}, devedor {credito.saldoDevedorGrupo.toFixed(2)} — {credito.status}
                </p>
              )}
              {cliente && (
                <button type="button" className="link" onClick={abrirUltimasCompras}>
                  Últimas compras
                </button>
              )}
            </section>

            <section className="cartao">
              <div className="cartao-cabecalho-com-badge">
                <h2>Itens do pedido</h2>
                <span className="badge-contagem">{itens.length} {itens.length === 1 ? 'item' : 'itens'}</span>
              </div>

              {estadoPedido === 'Rascunho' && (
                <>
                  <label className="campo-produto-linha">
                    Produto
                    <CampoBusca
                      valor={produtoItem ? `${produtoItem.grupo}|${produtoItem.referencia} — ${produtoItem.descricao}` : ''}
                      placeholder="Buscar por código ou descrição..."
                      onAbrir={() => setProdutoBuscaAberta(true)}
                    />
                  </label>
                  {negociacaoVigente && (
                    <p className="aviso-negociacao aviso-negociacao-ok">
                      Preço negociado para este cliente (válido até {cotacao?.negociacao.dataValidade}) — desconto de {descPercentual.toFixed(2)}% aplicado automaticamente.
                    </p>
                  )}
                  {cotacao?.negociacao.situacao === 'nao_autorizada' && (
                    <p className="aviso-negociacao aviso-negociacao-alerta">
                      Existe negociação para este produto, mas não está autorizada — aplicado o preço de tabela.
                    </p>
                  )}
                  <div className="grade-campos grade-item">
                    <label>
                      Quantidade
                      <input type="number" min={0} step="1" value={quantidade} onChange={(e) => setQuantidade(Number(e.target.value))} />
                    </label>
                    <label>
                      Valor unitário
                      <input type="number" min={0} step="0.01" value={precoDigitado} disabled={negociacaoVigente} title={negociacaoVigente ? 'Preço definido pela negociação' : undefined} onChange={(e) => alterarPreco(Number(e.target.value))} />
                    </label>
                    <button type="button" className="primario" disabled={carregandoItem || negociacaoVencida} onClick={adicionarItem}>
                      {carregandoItem ? 'Calculando...' : '+ Adicionar'}
                    </button>
                  </div>
                  {erroItem && <p className="erro">{erroItem}</p>}
                </>
              )}

              {itens.length === 0 ? (
                <p className="vazio">Nenhum item ainda.</p>
              ) : (
                <div className="tabela-scroll">
                  <table className="tabela-itens">
                    <thead>
                      <tr>
                        <th>#</th><th>Produto</th><th>Qtd</th><th>Preço tabela</th><th>Preço final</th>
                        <th>% desc.</th><th>IPI</th><th>ICMS-ST</th><th></th>
                      </tr>
                    </thead>
                    <tbody>
                      {itens.map((i) => {
                        const editando = itemEditando === i.numero
                        return (
                          <Fragment key={i.numero}>
                            <tr>
                              <td>{i.numero}</td>
                              <td>{i.produtoGrupo}|{i.produtoReferencia} — {i.produtoDescricao}</td>
                              <td>
                                {editando ? (
                                  <input
                                    type="number"
                                    min={0}
                                    step="1"
                                    className="input-linha"
                                    value={edQuantidade}
                                    onChange={(e) => setEdQuantidade(Number(e.target.value))}
                                  />
                                ) : (
                                  i.quantidade
                                )}
                              </td>
                              <td>{i.precoTabelaAjustado.toFixed(2)}</td>
                              <td>
                                {editando ? (
                                  <input
                                    type="number"
                                    min={0}
                                    step="0.01"
                                    className="input-linha"
                                    value={edPreco}
                                    onChange={(e) => edAlterarPreco(Number(e.target.value))}
                                  />
                                ) : (
                                  i.precoFinal.toFixed(2)
                                )}
                              </td>
                              <td>{editando ? `${edDesconto.toFixed(2)}%` : `${i.percentualDesconto.toFixed(2)}%`}</td>
                              <td title={i.fiscalNaoCalculado}>{i.valorIpi !== null ? i.valorIpi.toFixed(2) : '⚠'}</td>
                              <td title={i.fiscalNaoCalculado}>{i.valorIcmSt !== null ? i.valorIcmSt.toFixed(2) : '⚠'}</td>
                              <td>
                                {estadoPedido === 'Rascunho' && (
                                  <div className="acoes-item">
                                    {editando ? (
                                      <>
                                        <button type="button" className="link" title="Salvar" disabled={edCarregando} onClick={salvarEdicaoItem}>
                                          {edCarregando ? '…' : '✓'}
                                        </button>
                                        <button type="button" className="link" title="Cancelar" onClick={cancelarEdicaoItem}>✕</button>
                                      </>
                                    ) : (
                                      <>
                                        <button type="button" className="link" title="Editar" onClick={() => iniciarEdicaoItem(i)}>✎</button>
                                        <button type="button" className="link" title="Excluir" onClick={() => excluirItem(i.numero)}>🗑</button>
                                      </>
                                    )}
                                  </div>
                                )}
                              </td>
                            </tr>
                            {editando && edErro && (
                              <tr>
                                <td colSpan={9} className="erro">{edErro}</td>
                              </tr>
                            )}
                          </Fragment>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </section>

            {estadoPedido === 'Processando' && (
              <section className="cartao processando">
                <h2>Processando</h2>
                <p>Aguardando o console gravar em <code>pedido.dbf</code> (referência {referenciaFechada})...</p>
              </section>
            )}

            {estadoPedido === 'Fechado' && (
              <section className="cartao confirmacao">
                <h2>Pedido gravado em pedido.dbf</h2>
                <p>Referência: <strong>{referenciaFechada}</strong> — escolha Imprimir ou Salvar na janela de entrega.</p>
              </section>
            )}
          </div>

          <div className="coluna-lateral">
          <aside className="cartao cartao-totais">
            <h3>Totais</h3>
            <label className="campo-condicao">
              Forma de pagamento
              <CampoBusca
                valor={condicaoPagamento ? `${condicaoPagamento.codigo} — ${condicaoPagamento.descricao}` : ''}
                placeholder="Buscar..."
                disabled={estadoPedido !== 'Rascunho'}
                onAbrir={() => setCondicaoBuscaAberta(true)}
              />
            </label>
            <div className="linha-total">
              <span>Total dos produtos</span>
              <span className="valor-total">{totalNota.toFixed(2)}</span>
            </div>
            <div className="linha-total">
              <span>IPI</span>
              <span className="valor-total">{totalIpi.toFixed(2)}</span>
            </div>
            <div className="linha-total">
              <span>ICMS-ST</span>
              <span className="valor-total">{totalSt.toFixed(2)}</span>
            </div>
            <div className="linha-total">
              <span>Frete e despesas</span>
              <span className="valor-total">0,00</span>
            </div>
            <div className="linha-total linha-total-final">
              <span>Total do pedido</span>
              <span className="valor-total">{(totalNota + totalImpostos).toFixed(2)}</span>
            </div>
            {itensSemFiscal.length > 0 && (
              <p className="erro">
                ⚠ {itensSemFiscal.length} item(ns) sem imposto calculado: {itensSemFiscal[0].fiscalNaoCalculado}
              </p>
            )}
            <p className="nota-totais">Frete ainda não é calculado. ICMS próprio está embutido no preço (não soma).</p>
          </aside>

          {estadoPedido === 'Rascunho' && itens.length > 0 && (
            <section className="cartao">
              <h2>Fechamento</h2>
              <div className="grade-campos">
                <label>
                  Vendedor 1
                  <CampoBusca
                    valor={vendedor1 ? `${vendedor1.codigo} — ${vendedor1.descricao}` : ''}
                    placeholder="Buscar vendedor..."
                    onAbrir={() => setVendedorBuscaAlvo(1)}
                  />
                </label>
                <label>
                  Vendedor 2
                  <CampoBusca
                    valor={vendedor2 ? `${vendedor2.codigo} — ${vendedor2.descricao}` : ''}
                    placeholder="Buscar vendedor..."
                    onAbrir={() => setVendedorBuscaAlvo(2)}
                  />
                </label>
              </div>
              {erroFechamento && <p className="erro">{erroFechamento}</p>}
              <button type="button" className="primario" onClick={fecharPedido}>Fechar pedido</button>
            </section>
          )}
          </div>
        </div>
      </main>

      <BuscaModal<ClienteResumo>
        titulo="Buscar cliente"
        placeholder="Código ou nome do cliente..."
        aberto={clienteBuscaAberta}
        onFechar={() => setClienteBuscaAberta(false)}
        onSelecionar={setCliente}
        buscar={(termo) => api.buscarClientes(termo, usuario.vendedorCodigo)}
        chave={(c) => c.codigo}
        colunas={[
          { cabecalho: 'Código', render: (c) => c.codigo },
          { cabecalho: 'Nome', render: (c) => c.nome },
          { cabecalho: 'Crédito', render: (c) => c.credito.toFixed(2) },
        ]}
      />

      <BuscaModal<ProdutoResumo>
        titulo="Buscar produto"
        placeholder="Código ou descrição do produto..."
        aberto={produtoBuscaAberta}
        onFechar={() => setProdutoBuscaAberta(false)}
        onSelecionar={selecionarProduto}
        buscar={(termo) => api.buscarProdutos(empresa, termo)}
        chave={(p) => `${p.grupo}|${p.referencia}`}
        colunas={[
          { cabecalho: 'Código', render: (p) => `${p.grupo}|${p.referencia}` },
          { cabecalho: 'Descrição', render: (p) => p.descricao },
          { cabecalho: 'Preço tabela', render: (p) => p.precoTabela.toFixed(2) },
          {
            cabecalho: 'Giro',
            render: (p) => (
              <button type="button" className="link" onClick={(e) => { e.stopPropagation(); abrirGiro(p) }}>
                Giro
              </button>
            ),
          },
          {
            cabecalho: 'Saldo geral',
            render: (p) => (
              <button type="button" className="link" onClick={(e) => { e.stopPropagation(); abrirSaldoGeral(p) }}>
                Saldo geral
              </button>
            ),
          },
        ]}
      />

      <BuscaModal<ReferenciaSimples>
        titulo="Buscar tipo de movimentação"
        placeholder="Código ou descrição..."
        minCaracteres={0}
        aberto={tipoBuscaAberta}
        onFechar={() => setTipoBuscaAberta(false)}
        onSelecionar={setTipoOperacao}
        buscar={(termo) => api.buscarTiposOperacao(termo)}
        chave={(t) => t.codigo}
        colunas={[
          { cabecalho: 'Código', render: (t) => t.codigo },
          { cabecalho: 'Descrição', render: (t) => t.descricao },
        ]}
      />

      <BuscaModal<ReferenciaSimples>
        titulo="Buscar forma de pagamento"
        placeholder="Código ou descrição..."
        minCaracteres={0}
        aberto={condicaoBuscaAberta}
        onFechar={() => setCondicaoBuscaAberta(false)}
        onSelecionar={setCondicaoPagamento}
        buscar={(termo) => api.buscarCondicoesPagamento(termo)}
        chave={(c) => c.codigo}
        colunas={[
          { cabecalho: 'Código', render: (c) => c.codigo },
          { cabecalho: 'Descrição', render: (c) => c.descricao },
        ]}
      />

      <BuscaModal<ReferenciaSimples>
        titulo={`Buscar vendedor ${vendedorBuscaAlvo ?? ''}`}
        placeholder="Código ou nome do vendedor..."
        minCaracteres={0}
        aberto={vendedorBuscaAlvo !== null}
        onFechar={() => setVendedorBuscaAlvo(null)}
        onSelecionar={(v) => (vendedorBuscaAlvo === 1 ? setVendedor1(v) : setVendedor2(v))}
        buscar={(termo) => api.buscarVendedores(termo)}
        chave={(v) => v.codigo}
        colunas={[
          { cabecalho: 'Código', render: (v) => v.codigo },
          { cabecalho: 'Nome', render: (v) => v.descricao },
        ]}
      />

      {impressao && (
        <DialogoEntrega
          aberto={entregaAberta}
          enderecoInicial={{
            endereco: impressao.cliente.endereco,
            bairro: impressao.cliente.bairro,
            cidade: impressao.cliente.cidade,
            estado: impressao.cliente.estado,
          }}
          onImprimir={(dados) => {
            gravarDadosEntrega(dados)
            // Abre o PDF DENTRO da aplicação (renderizado em canvas) — a
            // tela só volta para um novo pedido quando o visualizador fechar.
            setEntregaAberta(false)
            setPdfVisualizacao({ bytes: gerarBytesPdfPedidoWe(impressao, dados), entrega: dados })
          }}
          onSalvar={(dados) => {
            gravarDadosEntrega(dados)
            salvarPdfPedidoWe(impressao, dados)
            novoPedido()
          }}
          onFechar={(dados) => {
            gravarDadosEntrega(dados) // como no VFP, o que foi digitado fica gravado mesmo saindo sem imprimir
            novoPedido()
          }}
        />
      )}

      {impressao && (
        <VisualizadorPdf
          pdfBytes={pdfVisualizacao?.bytes ?? null}
          titulo={`Pedido ${impressao.referencia}`}
          onBaixar={() => pdfVisualizacao && salvarPdfPedidoWe(impressao, pdfVisualizacao.entrega)}
          onFechar={novoPedido}
        />
      )}

      <UltimasCompras
        aberto={ultimasComprasAberta}
        clienteNome={cliente ? `${cliente.codigo} — ${cliente.nome}` : ''}
        carregando={ultimasComprasCarregando}
        erro={ultimasComprasErro}
        compras={ultimasCompras}
        onFechar={() => setUltimasComprasAberta(false)}
      />

      <Giro
        aberto={giroAberto}
        produtoNome={giroProdutoNome}
        carregando={giroCarregando}
        erro={giroErro}
        giro={giro}
        onFechar={() => setGiroAberto(false)}
      />

      <SaldoGeral
        aberto={saldoGeralAberto}
        produtoNome={saldoGeralProdutoNome}
        carregando={saldoGeralCarregando}
        erro={saldoGeralErro}
        saldos={saldoGeral}
        onFechar={() => setSaldoGeralAberto(false)}
      />
    </div>
  )
}

export default App
