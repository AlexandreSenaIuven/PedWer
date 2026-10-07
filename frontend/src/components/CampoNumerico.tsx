import { useEffect, useState } from 'react'

interface Props {
  valor: number
  onChange: (valor: number) => void
  disabled?: boolean
  title?: string
  className?: string
}

// Campo numérico que seleciona todo o conteúdo ao receber foco (digitar "1"
// substitui o "0" em vez de virar "01") e aceita vírgula ou ponto decimal.
// Guarda o texto digitado à parte para não brigar com o usuário no meio da
// digitação ("1," ou "1.0"), e só ressincroniza quando o valor externo muda.
export function CampoNumerico({ valor, onChange, disabled, title, className }: Props) {
  const [texto, setTexto] = useState(String(valor))

  useEffect(() => {
    setTexto((atual) => (converter(atual) === valor ? atual : String(valor)))
  }, [valor])

  return (
    <input
      type="text"
      inputMode="decimal"
      className={className}
      value={texto}
      disabled={disabled}
      title={title}
      onFocus={(e) => e.target.select()}
      onChange={(e) => {
        const t = e.target.value
        if (!/^\d*[.,]?\d*$/.test(t)) return
        setTexto(t)
        onChange(converter(t))
      }}
    />
  )
}

function converter(texto: string): number {
  const n = Number(texto.replace(',', '.'))
  return Number.isFinite(n) ? n : 0
}
