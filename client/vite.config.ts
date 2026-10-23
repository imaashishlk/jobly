import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

export default defineConfig(({ mode }) => {
  // Where `npm run dev` sends /api requests. The default is the API from the Docker stack
  // (JOBLY_API_PORT in docker/.env). Put VITE_API_PROXY=http://... in client/.env.local to point at another API.
  const env = loadEnv(mode, ".", "VITE_");
  const apiTarget = env.VITE_API_PROXY ?? "http://127.0.0.1:48217";

  return {
    plugins: [react()],
    server: {
      // The browser talks to the dev server only, which forwards /api to the API.
      // That way there is no CORS setup, and it matches how nginx works in Docker.
      proxy: { "/api": apiTarget },
    },
  };
});
