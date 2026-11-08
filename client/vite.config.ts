import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  // Fixed port: the API's CORS policy allows http://localhost:5173.
  server: { port: 5173, strictPort: true },
})
