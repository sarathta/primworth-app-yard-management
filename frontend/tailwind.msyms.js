/** @type {import('tailwindcss').Config} */
//
// Tailwind config for the embedded mSYMS UI ONLY.
//
// Two things make it safe to have Tailwind in a build whose host application does not use
// it:
//
//   1. `content` is limited to the msyms folder, so the only utilities generated are the
//      ones those screens actually reference.
//   2. `important` scopes every generated utility under `.msyms-root`, so a rule becomes
//      `.msyms-root .bg-background { ... }` and physically cannot apply to platform markup
//      even if a class name happened to collide.
//
// Preflight — Tailwind's global element reset — is excluded separately in msyms.css by
// importing theme and utilities without the base layer. Preflight would restyle every
// button, heading and list in the platform.
//
// The colour palette is copied verbatim from mSYMS's own tailwind.config.js so the UI
// renders exactly as it did standalone.
export default {
  content: ['./src/pages/yard/msyms/**/*.{js,ts,jsx,tsx}'],
  important: '.msyms-root',
  theme: {
    extend: {
      colors: {
        background: '#0B0C10',
        primary: '#1F2833',
        secondary: '#93969a',
        accent: '#098446',
        accentHover: '#45A29E',
        success: '#2E8B57',
        danger: '#DC143C',
        warning: '#FF8C00',
      },
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
