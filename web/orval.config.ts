import { defineConfig } from 'orval';
import { faker } from '@faker-js/faker';

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
        transformer: (verb) => {
          if (verb?.queryParams?.schema) {
            // Add MaybeRef to imports if not already there
            // Check if MaybeRef import already exists
            const hasMaybeRefImport = verb?.queryParams.schema.imports.some(
              (imp) => imp.name === 'MaybeRef' && imp.importPath === 'vue',
            );

            // Add MaybeRef import if not present
            if (!hasMaybeRefImport) {
              verb?.queryParams.schema.imports.push({
                name: 'MaybeRef',
                importPath: 'vue',
                default: false,
                values: false,
                syntheticDefaultImport: false,
                namespaceImport: false,
              });
            }

            // Keep original type shape, rename export, then add MaybeRef alias with original name
            const typeName = verb.queryParams.schema.name;
            const originalTypeName = `Original${typeName}`;
            const renamedModel = verb.queryParams.schema.model.replace(
              `export type ${typeName} =`,
              `export type ${originalTypeName} =`,
            );

            verb.queryParams.schema.model = `${renamedModel}\nexport type ${typeName} = MaybeRef<${originalTypeName}>;\n`;
          }

          return verb;
        },
        mock: {
          properties: {
            '/firstName/': () => faker.person.firstName(),
            '/lastName/': () => faker.person.lastName(),
            '/email/': () => faker.internet.email(),
          },
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
