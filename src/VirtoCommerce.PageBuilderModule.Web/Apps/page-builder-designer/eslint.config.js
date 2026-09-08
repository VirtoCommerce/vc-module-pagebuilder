// @ts-check
const { FlatCompat } = require('@eslint/eslintrc');
const js = require('@eslint/js');

const compat = new FlatCompat({
    baseDirectory: __dirname,
    recommendedConfig: js.configs.recommended,
});

/** @type {import('eslint').Linter.Config[]} */
module.exports = [
    {
        ignores: ['dist/**/*'],
    },
    ...compat.config({
        overrides: [
            {
                files: ['*.ts'],
                extends: [
                    'plugin:@angular-eslint/recommended',
                    'plugin:@angular-eslint/template/process-inline-templates',
                ],
                parserOptions: {
                    // the root tsconfig.json only holds references and includes no files at all,
                    // so every project has to be listed by the config that actually covers its sources
                    project: [
                        'tsconfig.app.json',
                        'tsconfig.spec.json',
                        'projects/ngv-markdown/tsconfig.lib.json',
                        'projects/ngv-markdown/tsconfig.spec.json',
                        'projects/ngv-datepicker/tsconfig.lib.json',
                    ],
                },
                rules: {
                    '@angular-eslint/no-output-on-prefix': 'off',
                    '@angular-eslint/no-input-rename': 'off',
                },
            },
            {
                files: ['*.html'],
                extends: ['plugin:@angular-eslint/template/recommended'],
                rules: {},
            },
        ],
    }),
];
