/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./App.{js,ts,tsx}', './src/**/*.{js,ts,jsx,tsx}'],
  presets: [require('nativewind/preset')],
  theme: {
    extend: {
      colors: {
        // Post-apocalyptic dark palette
        bg: {
          primary: '#0f0f0f',
          secondary: '#1a1a1a',
          card: '#1e1e1e',
          elevated: '#252525',
        },
        brand: {
          DEFAULT: '#c8a84b',  // worn gold
          dim: '#8a6f2e',
          glow: '#e8c86a',
        },
        danger: {
          DEFAULT: '#c0392b',
          glow: '#e74c3c',
        },
        success: {
          DEFAULT: '#27ae60',
          glow: '#2ecc71',
        },
        muted: '#6b6b6b',
        border: '#2e2e2e',
        // Resource colours
        resource: {
          money: '#f1c40f',
          wood: '#8b5e3c',
          metal: '#7f8c8d',
          fabric: '#9b59b6',
          parts: '#2980b9',
          food: '#27ae60',
        },
      },
      fontFamily: {
        mono: ['Courier New', 'monospace'],
      },
    },
  },
  plugins: [],
};
