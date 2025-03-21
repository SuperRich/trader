export const config = {
  // Default to localhost in development, can be overridden by environment variables
  apiBaseUrl: process.env.NODE_ENV === 'development' 
    ? 'https://localhost:7001' 
    : (process.env.NEXT_PUBLIC_API_URL || process.env.NEXT_PUBLIC_API_BASE_URL || 'http://217.154.57.29:8080'),
};
