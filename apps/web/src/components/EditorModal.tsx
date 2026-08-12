import { useEffect, useId, useRef, type MouseEvent, type ReactNode } from 'react'

type Props = {
  eyebrow: string
  title: string
  onClose: () => void
  children: ReactNode
  wide?: boolean
}

export function EditorModal({ eyebrow, title, onClose, children, wide = false }: Props) {
  const titleId = useId()
  const dialogRef = useRef<HTMLElement>(null)
  const onCloseRef = useRef(onClose)
  onCloseRef.current = onClose

  useEffect(() => {
    const previousOverflow = document.body.style.overflow
    const previouslyFocused = document.activeElement as HTMLElement | null
    document.body.style.overflow = 'hidden'
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onCloseRef.current()
      if (event.key === 'Tab') {
        const focusable = dialogRef.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])')
        if (!focusable?.length) return
        const first = focusable[0]
        const last = focusable[focusable.length - 1]
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault()
          last.focus()
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault()
          first.focus()
        }
      }
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', closeOnEscape)
      previouslyFocused?.focus()
    }
  }, [])

  function closeFromBackdrop(event: MouseEvent<HTMLDivElement>) {
    if (event.target === event.currentTarget) onClose()
  }

  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={closeFromBackdrop}>
      <section ref={dialogRef} className={wide ? 'editor-modal wide' : 'editor-modal'} role="dialog" aria-modal="true" aria-labelledby={titleId}>
        <header className="modal-header">
          <div><p className="overline">{eyebrow}</p><h2 id={titleId}>{title}</h2></div>
          <button className="modal-close" type="button" onClick={onClose} aria-label="Fermer">×</button>
        </header>
        <div className="modal-content">{children}</div>
      </section>
    </div>
  )
}
