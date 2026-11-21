/** A required environment variable (a test account is never written into the suite) */
export function required(name: string): string {
  const value = process.env[name];
  if (!value) throw new Error(`Set ${name} (see tests/e2e/README.md)`);
  return value;
}
