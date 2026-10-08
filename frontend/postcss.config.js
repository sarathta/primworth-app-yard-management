// Tailwind exists in this build for one reason: the embedded mSYMS UI is written in it.
// The platform's own styles are hand-rolled CSS and must stay untouched — see
// src/pages/slab-yard/msyms/msyms.css for how the utilities are kept scoped.
export default {
  plugins: {
    '@tailwindcss/postcss': {},
    autoprefixer: {},
  },
};
