interface CampoBuscaProps {
  valor: string
  placeholder: string
  disabled?: boolean
  onAbrir: () => void
}

/** Campo com aparência de input, mas que abre a tela de consulta com filtro em vez de digitar direto. */
export function CampoBusca({ valor, placeholder, disabled, onAbrir }: CampoBuscaProps) {
  return (
    <button type="button" className="campo-busca" disabled={disabled} onClick={onAbrir} title={valor || undefined}>
      <span className={`campo-busca-texto ${valor ? '' : 'campo-busca-placeholder'}`}>{valor || placeholder}</span>
      <span className="campo-busca-icone" aria-hidden>🔍</span>
    </button>
  )
}
