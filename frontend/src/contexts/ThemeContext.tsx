import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { isDarkTheme, themeOptions, type Theme } from './theme-options'

interface ThemeContextValue {
  theme: Theme
  setTheme: (theme: Theme) => void
  toggleTheme: () => void
  isDark: boolean
}

const ThemeContext = createContext<ThemeContextValue | null>(null)
const THEME_KEY = 'bookspace.theme'

function isTheme(value: string | null): value is Theme {
  return themeOptions.some((option) => option.id === value)
}

function initialTheme(): Theme {
  const stored = localStorage.getItem(THEME_KEY)
  if (isTheme(stored)) return stored
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<Theme>(initialTheme)

  const setTheme = (next: Theme) => {
    localStorage.setItem(THEME_KEY, next)
    setThemeState(next)
  }

  useEffect(() => {
    const root = document.documentElement
    const dark = isDarkTheme(theme)

    root.dataset.theme = theme
    root.classList.toggle('dark', dark)
    root.style.colorScheme = dark ? 'dark' : 'light'
  }, [theme])

  const value = useMemo(
    () => ({
      theme,
      setTheme,
      toggleTheme: () => setTheme(isDarkTheme(theme) ? 'light' : 'dark'),
      isDark: isDarkTheme(theme),
    }),
    [theme],
  )

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}

export function useTheme() {
  const context = useContext(ThemeContext)
  if (!context) throw new Error('useTheme phải được dùng trong ThemeProvider')
  return context
}
