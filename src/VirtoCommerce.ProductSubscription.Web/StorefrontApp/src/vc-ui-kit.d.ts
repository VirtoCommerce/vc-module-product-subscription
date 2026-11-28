// The ui-kit declares its color types in a `declare global` block, so they are ambient in the
// host and unreachable through the facade — a types-only package cannot re-export a global.
// They are structural, so mirroring the two this plugin reads is enough.
declare global {
  type VcMainColorType = "primary" | "secondary" | "neutral" | "accent" | "info" | "success" | "warning" | "danger";
  type VcAlertColorType = "info" | "success" | "warning" | "danger";
}

export {};
