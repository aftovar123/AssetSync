import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// In development the app calls /api/... and Vite forwards it to the API
// running locally, so the browser never needs CORS. In production the API
// address comes from VITE_API_BASE_URL at build time.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5188',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test-setup.ts',
  },
})
