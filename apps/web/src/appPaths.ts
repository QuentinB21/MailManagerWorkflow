export const appHomePath = import.meta.env.BASE_URL

export function appPath(path: string) {
  return `${appHomePath}${path.replace(/^\/+/, '')}`
}
