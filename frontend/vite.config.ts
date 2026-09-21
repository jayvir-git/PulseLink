import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')
  const demo = (env.VITE_DEMO ?? process.env.VITE_DEMO) === '1'

  return {
    plugins: [react()],
    // A literal, not an env lookup: the ordinary build must be able to drop the
    // demo adapter and its fixture rather than ship them as dead code.
    define: {
      __DEMO__: JSON.stringify(demo),
    },
    server: {
      port: 5173,
      proxy: {
        '/api': 'http://localhost:5080',
        '/health': 'http://localhost:5080',
      },
    },
  }
})
