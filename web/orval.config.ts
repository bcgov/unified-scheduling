import { defineConfig } from 'orval';

export default defineConfig({
  unified: {
    output: {
      mode: 'tags-split',
      target: './src/api-access/generated',
      schemas: './src/api-access/generated/models',
      override: {
        fetch: {
          includeHttpResponseReturnType: false,
        },
        mutator: {
          path: './src/api-access/useFetchAPI.ts',
          name: 'useFetchAPI',
        },
      },
      mock: true,
    },
    input: {
      target: 'http://localhost:5000/openapi/v1.json',
    },
    hooks: {
      afterAllFilesWrite: 'npm run format',
    },
  },
});
