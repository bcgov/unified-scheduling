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
      tsconfig: './tsconfig.app.json',
    },
    input: {
      target: 'http://localhost:5000/openapi/v1.json',
    },
    hooks: {
      afterAllFilesWrite: 'npm run format',
    },
  },
  unifiedZod: {
    output: {
      client: 'zod',
      mode: 'tags-split',
      target: './src/api-access/generated',
      fileExtension: '.zod.ts',
      override: {
        zod: {
          strict: {
            query: true,
            param: true,
            header: true,
            body: true,
          },
          generate: {
            param: true,
            body: true,
            query: true,
            header: true,
          },
        },
      },
      tsconfig: './tsconfig.app.json',
    },
    input: {
      target: 'http://localhost:5000/openapi/v1.json',
    },
  },
});
