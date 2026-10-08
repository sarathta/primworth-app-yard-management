import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// No Tailwind here on purpose: the platform's own styles are hand-rolled CSS (src/styles), and
// an application built with those classes looks native the moment it is integrated.
export default defineConfig({
  plugins: [react()],
  resolve: {
    // '@/…' → src/…, the alias the platform's frontend also supports.
    alias: { '@': '/src' },
  },
  server: {
    port: 5175,
    strictPort: true,
    // app.manifest.json lives one level up, next to the backend; allow Vite to serve it.
    fs: { allow: ['..'] },
  },
});
