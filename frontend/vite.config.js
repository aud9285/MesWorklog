import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
    server: {
      port: 5173,
      proxy: {
        // 백엔드 launchSettings.json의 http 프로필 포트(5201).
        // Docker로 띄울 땐 8080이라 프로필 바뀌면 여기도 같이 바꿔야 함.
        '/api': 'http://localhost:5201',
      }
    },
})
