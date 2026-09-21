// Extracted from client.ts so the demo adapter can raise the same error type
// without importing the HTTP client at runtime.
export class ApiError extends Error {
  readonly status: number;
  readonly details: unknown;

  constructor(status: number, message: string, details: unknown) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.details = details;
  }

  get code(): string | undefined {
    return this.details !== null && typeof this.details === 'object' && 'code' in this.details
      && typeof this.details.code === 'string' ? this.details.code : undefined;
  }
}
