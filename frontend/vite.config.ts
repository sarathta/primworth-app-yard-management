import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';

function wasmMime(): Plugin {
  const apply = (req: { url?: string }, res: { setHeader: (name: string, value: unknown) => unknown }, next: () => void) => {
    const url = req.url ?? '';
    const path = url.split('?')[0];
    const query = url.slice(path.length);
    if (path.endsWith('.wasm') && !/[?&](url|init)(?:&|=|$)/.test(query)) {
      const setHeader = res.setHeader.bind(res);
      res.setHeader = (name: string, value: unknown) => {
        if (String(name).toLowerCase() === 'content-type') return setHeader('Content-Type', 'application/wasm');
        return setHeader(name, value);
      };
    }
    next();
  };
  return {
    name: 'wasm-mime',
    configureServer(server) { server.middlewares.use(apply as never); },
    configurePreviewServer(server) { server.middlewares.use(apply as never); },
  };
}

// No Tailwind here on purpose: the platform's own styles are hand-rolled CSS (src/styles), and
// an application built with those classes looks native the moment it is integrated.
export default defineConfig({
  plugins: [react(), wasmMime()],
  assetsInclude: ['**/*.wasm'],
  optimizeDeps: {
    exclude: ['@gorules/zen-engine-wasm'],
  },
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
