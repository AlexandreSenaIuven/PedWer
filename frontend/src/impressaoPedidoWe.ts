import { jsPDF } from 'jspdf'
import type { DadosImpressaoPedido } from './impressaoPedido'

// Reprodução do formulário de pedido "PED_WE" do ERP atual (referência:
// ped_we.pdf, 24/08/2026). Diferenças do mod. PE: preço unitário com 4
// casas, colunas de alíquota IPI/ICM por item, desconto com 4 casas, linhas
// com grade, "Pág.: n / total", totais com PESO BRUTO / QTD. CAIXAS /
// PERC. VEND / CUBAGEM / VALOR IPI / VALOR TOTAL ST / VALOR FRETE, e os
// dados de entrega/observações vindos da tela "Dados Para Entrega".

export interface DadosEntregaWe {
  endereco: string
  bairro: string
  cidade: string
  estado: string
  referencia: string
  observacao1: string
  observacao2: string
}

const n2 = (v: number) => v.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const n4 = (v: number) => v.toLocaleString('pt-BR', { minimumFractionDigits: 4, maximumFractionDigits: 4 })

const ML = 8
const MR = 202
const LARG = MR - ML

// colunas da tabela de itens (bordas verticais)
const COLS = [8, 16, 40, 52, 60, 128, 146, 166, 176, 184, 202]

export function gerarPdfPedidoWe(d: DadosImpressaoPedido, entrega: DadosEntregaWe): jsPDF {
  const doc = new jsPDF({ unit: 'mm', format: 'a4' })
  doc.setFont('courier', 'normal')
  doc.setLineWidth(0.25)

  const Y_MAX_ITENS = 250
  let pagina = 0
  let y = 0
  const dataImpressao = new Date().toLocaleDateString('pt-BR')

  const texto = (t: string, x: number, yy: number, opts?: { bold?: boolean; size?: number; right?: boolean }) => {
    doc.setFont('courier', opts?.bold ? 'bold' : 'normal')
    doc.setFontSize(opts?.size ?? 8)
    doc.text(t, x, yy, opts?.right ? { align: 'right' } : undefined)
  }

  const novaPagina = () => {
    pagina += 1
    if (pagina > 1) doc.addPage()

    texto('PED_WE', MR, 6, { size: 6.5, right: true })

    // ── cabeçalho: empresa | dados do pedido ──
    doc.rect(ML, 8, LARG, 30)
    doc.line(130, 8, 130, 38)
    texto(d.empresa.nome, ML + 2, 13, { bold: true, size: 9 })
    texto(`RUA: ${d.empresa.endereco} - ${d.empresa.bairro}`, ML + 2, 18)
    texto(`CEP: ${d.empresa.cep} - ${d.empresa.cidade} - ${d.empresa.uf}`, ML + 2, 22.5)
    texto(`CNPJ: ${d.empresa.cnpj} - I.E.: ${d.empresa.inscricaoEstadual}`, ML + 2, 27)
    texto(`TEL.: ${d.empresa.telefone}${d.empresa.fax ? ` - FAX: ${d.empresa.fax}` : ''}`, ML + 2, 31.5)
    texto(`E-MAIL: ${d.empresa.email}`, ML + 2, 36)

    texto('PEDIDO..: ', 132, 13, { size: 9 })
    texto(d.referencia, 152, 13, { bold: true, size: 10 })
    texto(`EMISSÃO..: ${d.dataEmissao}`, 132, 18.5)
    texto(`IMPRESSÃO: ${dataImpressao}`, 132, 23)
    texto(`HORA.....: ${d.hora}`, 132, 27.5)
    texto(`TIPO.....: ${d.tipoOperacao}`, 132, 32)

    // ── vendedor (só o 1º neste modelo) ──
    doc.rect(ML, 40, LARG, 7)
    texto('VENDEDOR..: ', ML + 2, 44.7)
    texto(d.vendedor1Nome, ML + 26, 44.7, { bold: true })

    // ── bloco do cliente ──
    doc.rect(ML, 49, LARG, 22)
    texto(`CLIENTE...: ${d.cliente.nome}`, ML + 2, 54)
    texto(`CÓD.CLI..: ${d.cliente.codigo}`, 110, 54)
    texto(`COMPRADOR: ${d.cliente.comprador}`, 155, 54)
    texto(`ENDEREÇO..: ${d.cliente.endereco}`, ML + 2, 58.7)
    texto(`BAIRRO...: ${d.cliente.bairro}`, 110, 58.7)
    texto(`CIDADE....: ${d.cliente.cidade} - ${d.cliente.estado}`, ML + 2, 63.4)
    texto(`CEP:${d.cliente.cep}`, 95, 63.4)
    texto(`CNPJ.....: ${d.cliente.cnpj}`, 130, 63.4)
    texto(`TELEFONE..: ${d.cliente.telefone}`, ML + 2, 68.1)
    texto(`I.E......: ${d.cliente.inscricaoEstadual}`, 110, 68.1)

    // ── cabeçalho da tabela ──
    doc.rect(ML, 73, LARG, 6)
    for (const x of COLS.slice(1, -1)) doc.line(x, 73, x, 79)
    texto('IT', 10, 77, { bold: true })
    texto('CÓDIGO', 22, 77, { bold: true })
    texto('QTDE', 43, 77, { bold: true })
    texto('UND', 53, 77, { bold: true })
    texto('DESCRIÇÃO DO PRODUTO', 75, 77, { bold: true })
    texto('P. UNIT.', 144, 77, { bold: true, right: true })
    texto('PRC. TOTAL', 164, 77, { bold: true, right: true })
    texto('IPI', 168, 77, { bold: true })
    texto('ICM', 178, 77, { bold: true })
    texto('DESCONTO', 200, 77, { bold: true, right: true })

    y = 79
  }

  const rodapePagina = () => {
    texto(`Pág.: ${pagina} / {total}`, MR, 290, { size: 7, right: true })
  }

  const quebrarDescricao = (descricao: string): string[] => {
    const larguraMax = 46
    if (descricao.length <= larguraMax) return [descricao]
    const linhas: string[] = []
    let atual = descricao
    while (atual.length > larguraMax) {
      let corte = atual.lastIndexOf(' ', larguraMax)
      if (corte <= 0) corte = larguraMax
      linhas.push(atual.slice(0, corte))
      atual = atual.slice(corte).trimStart()
    }
    if (atual) linhas.push(atual)
    return linhas
  }

  novaPagina()

  d.itens.forEach((item, indice) => {
    const linhasDesc = quebrarDescricao(item.descricao)
    const alturaLinha = 3.6
    const alturaItem = Math.max(6, linhasDesc.length * alturaLinha + 2.4)

    if (y + alturaItem > Y_MAX_ITENS) {
      rodapePagina()
      novaPagina()
    }

    // grade da linha
    doc.rect(ML, y, LARG, alturaItem)
    for (const x of COLS.slice(1, -1)) doc.line(x, y, x, y + alturaItem)

    const yTexto = y + 4
    texto(String(indice + 1).padStart(2), 10, yTexto)
    texto(item.codigo, 17, yTexto)
    texto(String(item.quantidade), 50, yTexto, { right: true })
    texto(item.unidade, 53, yTexto)
    linhasDesc.forEach((linha, i) => texto(linha, 61, yTexto + i * alturaLinha))
    texto(n4(item.precoUnitario), 144, yTexto, { right: true })
    texto(n2(item.precoTotal), 164, yTexto, { right: true })
    texto(item.aliquotaIpi !== null ? n2(item.aliquotaIpi) : '-', 167, yTexto)
    texto(item.aliquotaIcm !== null ? String(item.aliquotaIcm) : '-', 177, yTexto)
    texto(`${n4(item.percentualDesconto)}%`, 200, yTexto, { right: true })

    y += alturaItem
  })

  // ── última página: totais em duas colunas + rodapé ──
  if (y > Y_MAX_ITENS - 70) {
    rodapePagina()
    novaPagina()
  }

  const pesoBruto = d.itens.reduce((s, i) => s + i.pesoUnitario * i.quantidade, 0)
  const qtdCaixas = d.itens.reduce((s, i) => s + i.quantidade, 0)
  const cubagem = d.itens.reduce((s, i) => s + i.volumeUnitario * i.quantidade, 0)
  const valorIpi = d.itens.reduce((s, i) => s + (i.valorIpi ?? 0), 0)
  const valorSt = d.itens.reduce((s, i) => s + (i.valorIcmSt ?? 0), 0)

  y += 5
  const L = 4.4
  texto('PESO BRUTO......:', ML + 4, y)
  texto(n2(pesoBruto), 75, y, { right: true })
  texto('TOTAL DOS PRODUTOS:', 145, y, { right: true })
  texto(n2(d.totalProdutos), 180, y, { right: true })
  y += L
  texto('QTD. CAIXAS.....:', ML + 4, y)
  texto(String(qtdCaixas), 75, y, { right: true })
  texto('VALOR IPI.........:', 145, y, { right: true })
  texto(n2(valorIpi), 180, y, { right: true })
  y += L
  texto('PERC. VEND......:', ML + 4, y)
  texto(n4(0), 75, y, { right: true })
  texto('VALOR TOTAL ST....:', 145, y, { right: true })
  texto(n2(valorSt), 180, y, { right: true })
  y += L
  texto('CUBAGEM.........:', ML + 4, y)
  texto(n2(cubagem), 75, y, { right: true })
  texto('VALOR FRETE.......:', 145, y, { right: true })
  texto(n2(0), 180, y, { right: true })
  y += L
  texto('TOTAL DO PEDIDO...:', 145, y, { right: true, bold: true })
  texto(n2(d.totalPedido), 180, y, { right: true, bold: true })

  y += L + 2
  doc.line(ML, y, MR, y)
  y += L
  texto(`PRAZO DE ENTREGA: ${d.dataEntrega}`, ML + 2, y)
  y += L
  texto(`COND. PAGAMENTO.: ${d.condicaoPagamento}`, ML + 2, y)
  y += L
  texto(
    `LOCAL DE ENTREGA: ${entrega.endereco} ${entrega.bairro} ${entrega.cidade} ${entrega.estado}`,
    ML + 2,
    y,
  )
  y += L
  texto(`REFERENCIA......: ${entrega.referencia}`, ML + 2, y)
  y += L
  texto('TIPO DE FRETE...:', ML + 2, y)
  y += L
  texto('TRANSPORTADOR...:', ML + 2, y)
  y += L
  texto('ENDEREÇO........:', ML + 2, y)

  y += L * 1.5
  texto('OBSERVAÇÃO:', ML + 2, y)
  y += L
  if (entrega.observacao1) {
    doc.setFontSize(7.5)
    const linhas1 = doc.splitTextToSize(entrega.observacao1, 180) as string[]
    linhas1.forEach((linha) => {
      texto(linha, ML + 2, y, { size: 7.5 })
      y += 3.6
    })
  }
  y += L
  if (entrega.observacao2) {
    const linhas2 = doc.splitTextToSize(entrega.observacao2, 180) as string[]
    linhas2.forEach((linha) => {
      texto(linha, ML + 2, y, { size: 7.5 })
      y += 3.6
    })
    y += L
  }

  // ── assinaturas ──
  const yA = Math.max(y + 10, 262)
  const blocos: [string, number, number][] = [
    ['Crédito Liberado por', ML + 4, 66],
    ['Separado por', 78, 136],
    ['Emitido por', 148, 200],
  ]
  for (const [rotulo, x1, x2] of blocos) {
    doc.line(x1, yA, x2, yA)
    texto(rotulo, (x1 + x2) / 2 - rotulo.length, yA + 4)
  }

  rodapePagina()

  // segunda passada: carimba o total de páginas em "Pág.: n / {total}"
  const total = doc.getNumberOfPages()
  for (let p = 1; p <= total; p++) {
    doc.setPage(p)
    // cobre o placeholder e reescreve com o total real
    doc.setFillColor(255, 255, 255)
    doc.rect(MR - 30, 286, 30, 6, 'F')
    texto(`Pág.: ${p} / ${total}`, MR, 290, { size: 7, right: true })
  }

  return doc
}

export function salvarPdfPedidoWe(dados: DadosImpressaoPedido, entrega: DadosEntregaWe) {
  gerarPdfPedidoWe(dados, entrega).save(`pedido-we-${dados.referencia}.pdf`)
}

/**
 * Gera o PDF como bytes para a aplicação RENDERIZAR ela mesma (pdf.js →
 * canvas). Nem aba nova nem iframe com blob: nos dois casos, navegador sem
 * visualizador de PDF (ex.: o embutido do VS Code) transforma em download —
 * problema relatado duas vezes em 24/08/2026.
 */
export function gerarBytesPdfPedidoWe(dados: DadosImpressaoPedido, entrega: DadosEntregaWe): ArrayBuffer {
  return gerarPdfPedidoWe(dados, entrega).output('arraybuffer')
}
