import { useEffect, useRef, useState } from 'react'
import type { DadosEntregaWe } from '../impressaoPedidoWe'

interface DialogoEntregaProps {
  aberto: boolean
  enderecoInicial: Omit<DadosEntregaWe, 'referencia' | 'observacao1' | 'observacao2'>
  onImprimir: (dados: DadosEntregaWe) => void
  onSalvar: (dados: DadosEntregaWe) => void
  /** Recebe os dados também — no VFP o que foi digitado fica gravado mesmo saindo (campos ligados à tabela). */
  onFechar: (dados: DadosEntregaWe) => void
}

/**
 * Réplica da tela "Dados Para Entrega" do app VFP (abre antes de imprimir o
 * modelo PED_WE): endereço pré-preenchido com o do cliente (editável),
 * referência e as duas observações do pedido. Esc fecha, como no original.
 */
export function DialogoEntrega({ aberto, enderecoInicial, onImprimir, onSalvar, onFechar }: DialogoEntregaProps) {
  const [endereco, setEndereco] = useState('')
  const [bairro, setBairro] = useState('')
  const [cidade, setCidade] = useState('')
  const [estado, setEstado] = useState('')
  const [referencia, setReferencia] = useState('')
  const [observacao1, setObservacao1] = useState('')
  const [observacao2, setObservacao2] = useState('')

  useEffect(() => {
    if (!aberto) return
    setEndereco(enderecoInicial.endereco)
    setBairro(enderecoInicial.bairro)
    setCidade(enderecoInicial.cidade)
    setEstado(enderecoInicial.estado)
    setReferencia('')
    setObservacao1('')
    setObservacao2('')
  }, [aberto, enderecoInicial])

  const dados = (): DadosEntregaWe => ({ endereco, bairro, cidade, estado, referencia, observacao1, observacao2 })
  const dadosRef = useRef(dados)
  dadosRef.current = dados

  useEffect(() => {
    if (!aberto) return
    const aoTeclar = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onFechar(dadosRef.current())
    }
    window.addEventListener('keydown', aoTeclar)
    return () => window.removeEventListener('keydown', aoTeclar)
  }, [aberto, onFechar])

  if (!aberto) return null

  return (
    <div className="modal-fundo" onClick={() => onFechar(dados())}>
      <div className="modal-caixa dialogo-entrega" onClick={(e) => e.stopPropagation()}>
        <div className="dialogo-entrega-titulo">Dados Para Entrega</div>
        <div className="dialogo-entrega-corpo">
          <label>
            Endereço:
            <input value={endereco} onChange={(e) => setEndereco(e.target.value)} />
          </label>
          <label>
            Bairro:
            <input value={bairro} onChange={(e) => setBairro(e.target.value)} />
          </label>
          <div className="dialogo-entrega-linha">
            <label className="campo-cidade">
              Cidade:
              <input value={cidade} onChange={(e) => setCidade(e.target.value)} />
            </label>
            <label className="campo-uf">
              Estado:
              <input value={estado} maxLength={2} onChange={(e) => setEstado(e.target.value.toUpperCase())} />
            </label>
          </div>
          <label>
            Referência:
            <textarea rows={3} value={referencia} onChange={(e) => setReferencia(e.target.value)} />
          </label>
          <label>
            1ª Observação do Pedido:
            <textarea rows={3} value={observacao1} onChange={(e) => setObservacao1(e.target.value)} />
          </label>
          <label>
            2ª Observação do Pedido:
            <textarea rows={3} value={observacao2} onChange={(e) => setObservacao2(e.target.value)} />
          </label>
        </div>
        <div className="dialogo-entrega-rodape">
          <span className="dica-esc">Esc - Sair</span>
          <div className="dialogo-entrega-botoes">
            <button type="button" className="primario" onClick={() => onImprimir(dados())}>Imprimir</button>
            <button type="button" onClick={() => onSalvar(dados())}>Salvar PDF</button>
            <button type="button" onClick={() => onFechar(dados())}>Sair</button>
          </div>
        </div>
      </div>
    </div>
  )
}
