import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { LIMITE_BUSCA } from '../api'

export interface ColunaBusca<T> {
  cabecalho: string
  render: (item: T) => React.ReactNode
}

interface BuscaModalProps<T> {
  titulo: string
  placeholder: string
  aberto: boolean
  onFechar: () => void
  onSelecionar: (item: T) => void
  buscar: (termo: string) => Promise<T[]>
  colunas: ColunaBusca<T>[]
  chave: (item: T) => string
  /**
   * Mínimo de caracteres digitados antes de disparar a busca (default 3 —
   * evita varrer tabelas grandes à toa). Passar 0 para tabelas pequenas de
   * referência (tipo de movimentação, forma de pagamento), cujos códigos têm
   * 2 caracteres e onde listar tudo de cara é o comportamento útil.
   */
  minCaracteres?: number
}

/**
 * Tela de consulta com filtro para campos ligados a tabelas do VFP — em vez
 * de um <select> simples, que não escala para catálogos grandes. A busca só
 * dispara depois de `minCaracteres` digitados e de uma pausa na digitação
 * (debounce de 400ms) — nada de consulta a cada tecla.
 */
export function BuscaModal<T>({
  titulo,
  placeholder,
  aberto,
  onFechar,
  onSelecionar,
  buscar,
  colunas,
  chave,
  minCaracteres = 3,
}: BuscaModalProps<T>) {
  const [termo, setTermo] = useState('')
  const [resultados, setResultados] = useState<T[]>([])
  const [carregando, setCarregando] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  const aguardandoDigitacao = termo.trim().length < minCaracteres

  // useLayoutEffect (síncrono, logo após o DOM montar), sem setTimeout: no
  // celular, o navegador só abre o teclado automaticamente se o .focus() cair
  // "perto o bastante" do toque que abriu a busca — um setTimeout coloca a
  // chamada tarde demais e o campo fica focado, mas sem teclado, exigindo um
  // segundo toque do usuário.
  useLayoutEffect(() => {
    if (!aberto) return
    setTermo('')
    setResultados([])
    inputRef.current?.focus()
  }, [aberto])

  useEffect(() => {
    if (!aberto) return
    if (termo.trim().length < minCaracteres) {
      setResultados([])
      setCarregando(false)
      return
    }
    setCarregando(true)
    const id = setTimeout(() => {
      buscar(termo.trim())
        .then(setResultados)
        .finally(() => setCarregando(false))
    }, 400)
    return () => clearTimeout(id)
  }, [termo, aberto, buscar, minCaracteres])

  if (!aberto) return null

  return (
    <div className="modal-fundo" onClick={onFechar}>
      <div className="modal-caixa" onClick={(e) => e.stopPropagation()}>
        <div className="modal-cabecalho">
          <h3>{titulo}</h3>
          <button type="button" className="modal-fechar" onClick={onFechar} aria-label="Fechar">✕</button>
        </div>
        <input
          ref={inputRef}
          className="modal-busca-input"
          placeholder={placeholder}
          value={termo}
          onChange={(e) => setTermo(e.target.value)}
        />
        <div className="modal-resultados">
          {aguardandoDigitacao && (
            <p className="vazio">Digite pelo menos {minCaracteres} caracteres para buscar.</p>
          )}
          {!aguardandoDigitacao && carregando && <p className="vazio">Buscando...</p>}
          {!aguardandoDigitacao && !carregando && resultados.length === 0 && <p className="vazio">Nenhum resultado.</p>}
          {!aguardandoDigitacao && !carregando && resultados.length >= LIMITE_BUSCA && (
            <p className="modal-aviso-limite">
              Mostrando os primeiros {LIMITE_BUSCA} resultados — digite mais para refinar a busca.
            </p>
          )}
          {!aguardandoDigitacao && !carregando && resultados.length > 0 && (
            <table>
              <thead>
                <tr>
                  {colunas.map((c) => <th key={c.cabecalho}>{c.cabecalho}</th>)}
                </tr>
              </thead>
              <tbody>
                {resultados.map((item) => (
                  <tr key={chave(item)} className="linha-selecionavel" onClick={() => { onSelecionar(item); onFechar() }}>
                    {colunas.map((c) => <td key={c.cabecalho}>{c.render(item)}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  )
}
