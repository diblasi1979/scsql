export const AUTH_TOKEN_STORAGE_KEY = 'novent-token'
export const LEGACY_AUTH_TOKEN_STORAGE_KEY = 'scsql-token'

export function getStoredAuthToken() {
  return localStorage.getItem(AUTH_TOKEN_STORAGE_KEY) ?? localStorage.getItem(LEGACY_AUTH_TOKEN_STORAGE_KEY) ?? ''
}

export function storeAuthToken(token: string) {
  localStorage.setItem(AUTH_TOKEN_STORAGE_KEY, token)
  localStorage.removeItem(LEGACY_AUTH_TOKEN_STORAGE_KEY)
}

export function clearStoredAuthToken() {
  localStorage.removeItem(AUTH_TOKEN_STORAGE_KEY)
  localStorage.removeItem(LEGACY_AUTH_TOKEN_STORAGE_KEY)
}