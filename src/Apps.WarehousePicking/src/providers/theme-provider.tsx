import { useMemo, useState, useSyncExternalStore } from "react"
import {
  FluentProvider,
  createDarkTheme,
  createLightTheme,
  type BrandVariants,
} from "@fluentui/react-components"
import { PcfProvider } from "@/providers/pcf-provider"
import { ThemeProviderContext, type Theme } from "@/providers/theme-context"

// Power Apps brand purple (#742774) at shade 80, the primary brand slot.
const powerAppsBrand: BrandVariants = {
  10: "#0B030B",
  20: "#1A081A",
  30: "#290D29",
  40: "#381238",
  50: "#471847",
  60: "#561D56",
  70: "#652265",
  80: "#742774",
  90: "#854085",
  100: "#965A96",
  110: "#A773A7",
  120: "#B88DB8",
  130: "#C8A6C8",
  140: "#D9BFD9",
  150: "#EAD9EA",
  160: "#FBF2FB",
}

const lightTheme = createLightTheme(powerAppsBrand)
const darkTheme = createDarkTheme(powerAppsBrand)

type ThemeProviderProps = {
  children: React.ReactNode
  defaultTheme?: Theme
  storageKey?: string
}

const darkQuery = window.matchMedia("(prefers-color-scheme: dark)")

// Tracks the OS color scheme live, so "system" follows it without a reload.
function usePrefersDark() {
  return useSyncExternalStore(
    (onChange) => {
      darkQuery.addEventListener("change", onChange)
      return () => darkQuery.removeEventListener("change", onChange)
    },
    () => darkQuery.matches
  )
}

export function ThemeProvider({
  children,
  defaultTheme = "system",
  storageKey = "app-theme",
}: ThemeProviderProps) {
  const [theme, setTheme] = useState<Theme>(
    () => (localStorage.getItem(storageKey) as Theme) || defaultTheme
  )
  const prefersDark = usePrefersDark()
  const isDark = theme === "dark" || (theme === "system" && prefersDark)
  const tokenTheme = isDark ? darkTheme : lightTheme

  const fluentDesignLanguage = useMemo(() => ({ isDarkTheme: isDark, tokenTheme }), [isDark, tokenTheme])

  const value = {
    theme,
    setTheme: (theme: Theme) => {
      localStorage.setItem(storageKey, theme)
      setTheme(theme)
    },
  }

  return (
    <ThemeProviderContext.Provider value={value}>
      <FluentProvider theme={tokenTheme} style={{ minHeight: "100dvh" }}>
        <PcfProvider fluentDesignLanguage={fluentDesignLanguage}>
          {children}
        </PcfProvider>
      </FluentProvider>
    </ThemeProviderContext.Provider>
  )
}
