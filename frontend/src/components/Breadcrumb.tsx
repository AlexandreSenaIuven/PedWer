export function Breadcrumb({ itens }: { itens: string[] }) {
  return (
    <nav className="breadcrumb">
      {itens.map((item, i) => (
        <span key={item}>
          {i > 0 && <span className="breadcrumb-sep">›</span>}
          <span className={i === itens.length - 1 ? 'breadcrumb-atual' : ''}>{item}</span>
        </span>
      ))}
    </nav>
  )
}
