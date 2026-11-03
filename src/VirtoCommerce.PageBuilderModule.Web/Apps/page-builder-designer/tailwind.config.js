module.exports = {
    content: ['./src/**/*.{html,ts,scss}'],
    mode: 'jit',
    // darkMode: false, // or 'media' or 'class'
    theme: {
        extend: {},
    },
    variants: {
        extend: {},
    },
    plugins: [
        require('@tailwindcss/forms'),
        require('@tailwindcss/typography')
    ]
}
