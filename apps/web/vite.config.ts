import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Tailwind v4 cài bằng plugin Vite chính thức. Không có tailwind.config.js,
// không có postcss.config.js — toàn bộ theme khai báo trong CSS bằng @theme.
export default defineConfig({
  plugins: [react(), tailwindcss()],

  resolve: {
    alias: {
      // import.meta.dirname thay cho __dirname: hợp với config loader native của Vite 8,
      // và trả về đường dẫn đúng trên Windows (URL.pathname thì không).
      '@': `${import.meta.dirname}/src`,
    },
  },

  server: {
    port: 5173,
    // Gọi API qua proxy cùng gốc để cookie phiên SameSite=Lax hoạt động giống production.
    // Gọi thẳng http://localhost:8080 sẽ thành cross-site và cookie bị chặn.
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: false,
      },
      '/health': {
        target: 'http://localhost:8080',
        changeOrigin: false,
      },
    },
  },

  build: {
    outDir: 'dist',
    sourcemap: true,
    rollupOptions: {
      output: {
        // Tách vendor để lần deploy nào chỉ đổi code app thì trình duyệt còn dùng lại được cache.
        // Dạng hàm chứ không dạng object: Rollup trong Vite 8 chỉ nhận hàm ở đây.
        manualChunks(id) {
          if (!id.includes('node_modules')) {
            return undefined
          }
          if (id.includes('react-router') || id.includes('/react/') || id.includes('react-dom')) {
            return 'react'
          }
          if (id.includes('@tanstack')) {
            return 'query'
          }
          if (id.includes('lucide-react')) {
            return 'icons'
          }
          return undefined
        },
      },
    },
  },
})
