import { useEffect, useRef, useState } from 'react'
import * as pdfjsLib from 'pdfjs-dist'
import pdfWorkerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url'

pdfjsLib.GlobalWorkerOptions.workerSrc = pdfWorkerUrl

interface VisualizadorPdfProps {
  pdfBytes: ArrayBuffer | null
  titulo: string
  onBaixar: () => void
  onFechar: () => void
}

/**
 * Renderiza o PDF DENTRO da aplicação, desenhando cada página num canvas
 * (pdf.js) — não depende do visualizador de PDF do navegador, que pode nem
 * existir (ex.: navegador embutido do VS Code) e aí qualquer blob/iframe de
 * PDF vira download. Imprimir monta um iframe oculto com as páginas como
 * imagem e chama print() nele.
 */
// Tempo além do qual desistimos de esperar o pdf.js (worker preso, rede
// lenta/instável — visto em celular, nunca em desktop) e mostramos erro em
// vez de deixar "Carregando..." girando pra sempre sem explicação.
const TEMPO_LIMITE_MS = 10_000

export function VisualizadorPdf({ pdfBytes, titulo, onBaixar, onFechar }: VisualizadorPdfProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const canvasesRef = useRef<HTMLCanvasElement[]>([])
  const [carregando, setCarregando] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    if (!pdfBytes) return
    let cancelado = false
    setCarregando(true)
    setErro(null)

    const finalizarComErro = (mensagem: string) => {
      if (cancelado) return
      cancelado = true
      clearTimeout(tempoLimite)
      setErro(mensagem)
      setCarregando(false)
    }

    const tempoLimite = setTimeout(
      () => finalizarComErro('A pré-visualização está demorando demais para carregar.'),
      TEMPO_LIMITE_MS,
    )

    ;(async () => {
      try {
        // pdf.js toma posse do buffer — passa uma cópia para não invalidar o original
        const pdf = await pdfjsLib.getDocument({ data: pdfBytes.slice(0) }).promise
        const container = containerRef.current
        if (!container || cancelado) return
        container.innerHTML = ''
        canvasesRef.current = []

        for (let numero = 1; numero <= pdf.numPages; numero++) {
          const pagina = await pdf.getPage(numero)
          const viewport = pagina.getViewport({ scale: 2 }) // escala 2 ≈ 144dpi — nítido na tela e na impressão
          const canvas = document.createElement('canvas')
          canvas.width = viewport.width
          canvas.height = viewport.height
          canvas.className = 'visualizador-pagina'
          await pagina.render({ canvas, viewport }).promise
          if (cancelado) return
          container.appendChild(canvas)
          canvasesRef.current.push(canvas)
        }
        if (!cancelado) {
          clearTimeout(tempoLimite)
          setCarregando(false)
        }
      } catch {
        finalizarComErro('Não foi possível gerar a pré-visualização.')
      }
    })()

    return () => {
      cancelado = true
      clearTimeout(tempoLimite)
    }
  }, [pdfBytes])

  if (!pdfBytes) return null

  const imprimir = () => {
    const imagens = canvasesRef.current
      .map((c) => `<img src="${c.toDataURL('image/png')}" style="width:100%;display:block;page-break-after:always">`)
      .join('')

    const frame = document.createElement('iframe')
    frame.style.position = 'fixed'
    frame.style.right = '0'
    frame.style.bottom = '0'
    frame.style.width = '0'
    frame.style.height = '0'
    frame.style.border = '0'
    document.body.appendChild(frame)

    const docFrame = frame.contentDocument
    if (!docFrame) return
    docFrame.open()
    docFrame.write(
      `<html><head><title>${titulo}</title><style>@page{size:A4;margin:0}body{margin:0}</style></head><body>${imagens}</body></html>`,
    )
    docFrame.close()
    frame.contentWindow?.focus()
    frame.contentWindow?.print()
    setTimeout(() => frame.remove(), 60_000)
  }

  return (
    <div className="visualizador-fundo">
      <div className="visualizador-topo">
        <span className="visualizador-titulo">{titulo}</span>
        <div className="visualizador-botoes">
          <button type="button" className={carregando || erro ? '' : 'primario'} disabled={carregando || !!erro} onClick={imprimir}>
            {carregando ? 'Carregando...' : 'Imprimir'}
          </button>
          {/* Não depende da pré-visualização (pdf.js) — download direto via
              jsPDF. Destacado enquanto ela carrega/falha, porque é o caminho
              que funciona independente do que travar no visualizador. */}
          <button type="button" className={carregando || erro ? 'primario' : ''} onClick={onBaixar}>
            Baixar PDF
          </button>
          <button type="button" onClick={onFechar}>Fechar</button>
        </div>
      </div>
      {erro && (
        <div className="visualizador-erro">
          <p>{erro}</p>
          <p>Use "Baixar PDF" para abrir no visualizador do aparelho.</p>
        </div>
      )}
      <div ref={containerRef} className="visualizador-corpo" style={erro ? { display: 'none' } : undefined} />
    </div>
  )
}
