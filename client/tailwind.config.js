/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        primary: {
          50: '#e6f0fa', 100: '#cce0f5', 200: '#99c2eb', 300: '#66a3e0',
          400: '#3385db', 500: '#0066cc', 600: '#0052a3', 700: '#003d7a',
          800: '#002952', 900: '#001429', 950: '#000a14',
          DEFAULT: '#0066cc',
        },
        accent: { DEFAULT: '#00A4DC', secondary: '#FF6B35' },
        surface: {
          50: '#f8f9fa', 100: '#f1f3f5', 200: '#dee2e6', 300: '#ced4da',
          400: '#94a3b8', 500: '#6c757d', 600: '#495057', 700: '#343a40',
          800: '#1e293b', 900: '#0f172a', 950: '#020617',
        },
      },
      fontFamily: {
        sans: ['Inter', '-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'Roboto', 'sans-serif'],
        mono: ['JetBrains Mono', 'Fira Code', 'Consolas', 'monospace'],
      },
      borderRadius: { sm: '4px', DEFAULT: '6px', md: '8px', lg: '10px', xl: '12px' },
    },
  },
  plugins: [],
};
