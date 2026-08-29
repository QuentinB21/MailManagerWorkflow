import { legalPaths } from '../legal'

type Props = { className?: string }

export function LegalLinks({ className = '' }: Props) {
  return (
    <nav className={className} aria-label="Informations légales">
      <a href={legalPaths.legalNotice}>Mentions légales</a>
      <a href={legalPaths.privacy}>Confidentialité</a>
      <a href={legalPaths.terms}>Conditions d’utilisation</a>
      <a href={legalPaths.cookies}>Cookies</a>
    </nav>
  )
}
