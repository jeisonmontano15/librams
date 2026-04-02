/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        ink:    { DEFAULT: "#1a1714", 50: "#f7f5f2", 100: "#e8e4de", 200: "#d1c9bf", 300: "#b3a898", 400: "#8f7f6d", 500: "#6b5e4f", 600: "#4e4339", 700: "#362e27", 800: "#221e1a", 900: "#1a1714" },
        paper:  { DEFAULT: "#f7f5f2", light: "#fdfcfa" },
        ochre:  { DEFAULT: "#c9622a", light: "#f5e6d8", dark: "#a04e20" },
        sage:   { DEFAULT: "#4a7c6a", light: "#d8ebe5", dark: "#2e5c4e" },
        slate:  { DEFAULT: "#5a6775", light: "#e8edf2", dark: "#3d4b57" },
      },
      fontFamily: {
        serif:  ['"Playfair Display"', 'Georgia', 'serif'],
        sans:   ['"DM Sans"', 'system-ui', 'sans-serif'],
        mono:   ['"JetBrains Mono"', 'monospace'],
      },
      borderRadius: { book: '2px 6px 6px 2px' },
      boxShadow: {
        book: '2px 0 0 0 #1a1714, 4px 0 8px -2px rgba(0,0,0,0.3)',
        card: '0 1px 3px rgba(26,23,20,0.08), 0 1px 2px rgba(26,23,20,0.04)',
      },
    },
  },
  plugins: [],
};
