import { jsPDF } from 'jspdf'

// Reprodução do formulário de pedido "mod. PE" do ERP atual (referência:
// ped_pe.pdf fornecido pelo usuário em 24/08/2026) — Courier, caixas com
// borda, cabeçalho repetido em toda página, totais e rodapé só na última.
// Campos que a versão web ainda não coleta (prazo de entrega, frete,
// transportadora, observação) saem em branco — mesmas posições do original.

export interface DadosImpressaoPedido {
  empresa: {
    nome: string
    endereco: string
    bairro: string
    cep: string
    cidade: string
    uf: string
    cnpj: string
    inscricaoEstadual: string
    telefone: string
    fax: string
    email: string
  }
  referencia: string
  dataEmissao: string // dd/mm/aaaa
  dataEntrega: string // dd/mm/aaaa — "PRAZO DE ENTREGA" (pedido.data_ent)
  hora: string // hh:mm:ss
  tipoOperacao: string
  vendedor1Nome: string
  vendedor2Nome: string
  cliente: {
    codigo: string
    nome: string
    comprador: string
    endereco: string
    bairro: string
    tipoCliente: string
    cidade: string
    estado: string
    cep: string
    cnpj: string
    telefone: string
    inscricaoEstadual: string
  }
  condicaoPagamento: string
  itens: {
    codigo: string
    quantidade: number
    unidade: string
    descricao: string
    precoUnitario: number
    precoTotal: number
    percentualDesconto: number
    aliquotaIpi: number | null
    aliquotaIcm: number | null
    valorIpi: number | null
    valorIcmSt: number | null
    pesoUnitario: number
    volumeUnitario: number
  }[]
  totalProdutos: number
  desconto: number
  impostos: number
  totalPedido: number
}

const n2 = (v: number) => v.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

const ML = 8 // margem esquerda
const MR = 202 // borda direita
const LARG = MR - ML

export function gerarPdfPedido(d: DadosImpressaoPedido): jsPDF {
  const doc = new jsPDF({ unit: 'mm', format: 'a4' })
  doc.setFont('courier', 'normal')
  doc.setLineWidth(0.25)

  const ALTURA_LINHA = 4.6
  const Y_MAX_ITENS = 240 // abaixo disso quebra página (deixa espaço p/ assinaturas)
  let pagina = 0
  let y = 0

  const texto = (t: string, x: number, yy: number, opts?: { bold?: boolean; size?: number; right?: boolean }) => {
    doc.setFont('courier', opts?.bold ? 'bold' : 'normal')
    doc.setFontSize(opts?.size ?? 8)
    doc.text(t, x, yy, opts?.right ? { align: 'right' } : undefined)
  }

  const novaPagina = () => {
    pagina += 1
    if (pagina > 1) doc.addPage()

    texto('mod. PE', MR, 6, { size: 6.5, right: true })

    // ── caixa do cabeçalho: empresa | pedido/emissão/hora ──
    doc.rect(ML, 8, LARG, 30)
    doc.line(130, 8, 130, 38)
    texto(d.empresa.nome, ML + 2, 13, { bold: true, size: 9 })
    texto(`RUA: ${d.empresa.endereco} - ${d.empresa.bairro}`, ML + 2, 18)
    texto(`CEP: ${d.empresa.cep} - ${d.empresa.cidade} - ${d.empresa.uf}`, ML + 2, 22.5)
    texto(`CNPJ: ${d.empresa.cnpj} - I.E.: ${d.empresa.inscricaoEstadual}`, ML + 2, 27)
    texto(`TEL.: ${d.empresa.telefone}${d.empresa.fax ? ` - FAX: ${d.empresa.fax}` : ''}`, ML + 2, 31.5)
    texto(`E-MAIL: ${d.empresa.email}`, ML + 2, 36)

    texto('PEDIDO: ', 132, 15, { size: 9 })
    texto(d.referencia, 148, 15, { bold: true, size: 10 })
    texto(`EMISSÃO: ${d.dataEmissao}`, 132, 24)
    texto(`HORA...: ${d.hora}`, 132, 30.5)
    texto(`PÁG.: ${pagina}`, 175, 30.5)

    // ── faixa dos vendedores ──
    doc.rect(ML, 40, LARG, 7)
    texto('VENDEDOR: ', ML + 2, 44.7)
    texto(d.vendedor1Nome, ML + 22, 44.7, { bold: true })
    texto(`VEND. EXTER: ${d.vendedor2Nome}`, 110, 44.7)

    // ── bloco do cliente ──
    doc.rect(ML, 49, LARG, 22)
    texto(`CLIENTE...: ${d.cliente.nome}`, ML + 2, 54)
    texto(`COD. CLI.: ${d.cliente.codigo}`, 105, 54)
    texto(`COMPRADOR: ${d.cliente.comprador}`, 150, 54)
    texto(`ENDEREÇO..: ${d.cliente.endereco}`, ML + 2, 58.7)
    texto(`BAIRRO.: ${d.cliente.bairro}`, 105, 58.7)
    texto(`Tipo Clie.: ${d.cliente.tipoCliente}`, 160, 58.7)
    texto(`CIDADE....: ${d.cliente.cidade} - ${d.cliente.estado}`, ML + 2, 63.4)
    texto(`CEP: ${d.cliente.cep}`, 95, 63.4)
    texto(`CNPJ...: ${d.cliente.cnpj}`, 130, 63.4)
    texto(`TELEFONE..: ${d.cliente.telefone}`, ML + 2, 68.1)
    texto(`I.E....: ${d.cliente.inscricaoEstadual}`, 105, 68.1)
    texto('PED.CLI:', 160, 68.1)

    // ── cabeçalho da tabela de itens ──
    doc.rect(ML, 73, LARG, 6)
    texto('IT', ML + 1, 77, { bold: true })
    texto('CODIGO', 18, 77, { bold: true })
    texto('QTDE', 47, 77, { bold: true })
    texto('UND', 58, 77, { bold: true })
    texto('DESCRIÇÃO DO PRODUTO', 88, 77, { bold: true })
    texto('P. UNIT.', 152, 77, { bold: true, right: true })
    texto('PRECO TOTAL', 180, 77, { bold: true, right: true })
    texto('DESC.', 201, 77, { bold: true, right: true })

    y = 83
  }

  const assinaturas = () => {
    const yA = 276
    const blocos: [string, number, number][] = [
      ['Crédito liberado por', ML + 4, 66],
      ['Separado por', 78, 136],
      ['Emitido por', 148, 200],
    ]
    for (const [rotulo, x1, x2] of blocos) {
      doc.line(x1, yA, x2, yA)
      texto(rotulo, (x1 + x2) / 2 - rotulo.length, yA + 4)
    }
    texto('Esse Pedido foi emitido pelo ERP da Falcora. www.falcora.com.br', 105 - 32, 285, { size: 7 })
  }

  novaPagina()

  d.itens.forEach((item, indice) => {
    if (y > Y_MAX_ITENS) {
      assinaturas()
      novaPagina()
    }
    texto(String(indice + 1).padStart(2), ML + 1, y)
    texto(item.codigo, 18, y)
    texto(String(item.quantidade), 52, y, { right: true })
    texto(item.unidade, 58, y)
    texto(item.descricao.slice(0, 48), 64, y)
    texto(n2(item.precoUnitario), 152, y, { right: true })
    texto(n2(item.precoTotal), 180, y, { right: true })
    texto(`${n2(item.percentualDesconto)}%`, 201, y, { right: true })
    y += ALTURA_LINHA
  })

  // ── totais e rodapé (só na última página; quebra antes se não couber) ──
  if (y > Y_MAX_ITENS - 40) {
    assinaturas()
    novaPagina()
  }

  y += 3
  texto('TOTAL DOS PRODUTOS:', 150, y, { right: true })
  texto(n2(d.totalProdutos), 180, y, { right: true })
  y += ALTURA_LINHA
  texto('DESCONTO..........:', 150, y, { right: true })
  texto(n2(d.desconto), 180, y, { right: true })
  y += ALTURA_LINHA
  if (d.impostos > 0) {
    texto('IMPOSTOS (IPI+ST).:', 150, y, { right: true })
    texto(n2(d.impostos), 180, y, { right: true })
    y += ALTURA_LINHA
  }
  texto('TOTAL DO PEDIDO...:', 150, y, { right: true, bold: true })
  texto(n2(d.totalPedido), 180, y, { right: true, bold: true })

  y += ALTURA_LINHA * 2
  texto(`PRAZO DE ENTREGA: ${d.dataEntrega}`, ML + 2, y)
  y += ALTURA_LINHA
  texto(`COND. PAGAMENTO : ${d.condicaoPagamento}`, ML + 2, y)
  y += ALTURA_LINHA
  texto(
    `LOCAL DE ENTREGA: ${d.cliente.endereco} ${d.cliente.bairro} ${d.cliente.cidade} ${d.cliente.estado}`,
    ML + 2,
    y,
  )
  y += ALTURA_LINHA
  texto('TIPO DE FRETE   :', ML + 2, y)
  y += ALTURA_LINHA
  texto('TRANSPORTADOR   :', ML + 2, y)
  y += ALTURA_LINHA * 2
  texto('OBSERVAÇÃO:', ML + 2, y)

  assinaturas()

  return doc
}

export function salvarPdfPedido(dados: DadosImpressaoPedido) {
  gerarPdfPedido(dados).save(`pedido-${dados.referencia}.pdf`)
}

export function imprimirPdfPedido(dados: DadosImpressaoPedido) {
  const doc = gerarPdfPedido(dados)
  doc.autoPrint()
  window.open(doc.output('bloburl'), '_blank')
}
