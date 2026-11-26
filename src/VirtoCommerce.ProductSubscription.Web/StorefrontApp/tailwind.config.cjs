const path = require("path");
const hostPreset = require("@vc-frontend/core/tailwind-preset");

// The HOST's design system (colors via CSS custom properties, spacing, breakpoints),
// scanning ONLY this plugin's sources - utilities match the host 1:1.
const preset = hostPreset.default ?? hostPreset;

module.exports = {
  ...preset,
  content: [path.resolve(__dirname, "index.html"), path.resolve(__dirname, "src/**/*.{vue,js,ts}")],
};
