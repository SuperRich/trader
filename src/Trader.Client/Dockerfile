FROM node:18-alpine AS base
WORKDIR /app

# Install dependencies only when needed
FROM base AS deps
COPY package.json package-lock.json* ./
RUN npm ci

# Rebuild the source code only when needed
FROM base AS builder
COPY --from=deps /app/node_modules ./node_modules
COPY . .

# Ensure public directory exists
RUN mkdir -p public

# Next.js collects anonymous telemetry data about general usage.
# Learn more here: https://nextjs.org/telemetry
ENV NEXT_TELEMETRY_DISABLED 1
ENV NODE_ENV production

# Debug the environment before building
RUN echo "Node version: $(node -v)" && \
    echo "NPM version: $(npm -v)" && \
    echo "Directory listing:" && \
    ls -la && \
    echo "Package.json:" && \
    cat package.json

# Build Next.js with verbose output and check for errors
RUN npm run build --verbose || (echo "BUILD FAILED" && exit 1)

# Check if build succeeded and what directories were created
RUN echo "After build - directory listing:" && \
    ls -la && \
    if [ -d ".next" ]; then echo ".next exists"; else echo ".next MISSING"; fi && \
    if [ -d "build" ]; then echo "build exists"; else echo "build MISSING"; fi

# Production image, copy all the files and run next
FROM base AS runner

# Install curl for healthcheck
RUN apk add --no-cache curl

# Set environment variables
ENV NODE_ENV production
ENV NEXT_TELEMETRY_DISABLED 1
ENV HOST 0.0.0.0
ENV PORT 3000

# Create non-root user
RUN addgroup --system --gid 1001 nodejs && \
    adduser --system --uid 1001 nextjs

# Set the correct permissions
RUN mkdir -p /app && chown -R nextjs:nodejs /app

WORKDIR /app

# Create necessary directories to prevent errors
RUN mkdir -p .next public

# Copy everything from builder
COPY --from=builder --chown=nextjs:nodejs /app/ ./

# Switch to non-root user
USER nextjs

# Expose port
EXPOSE 3000

# Start the application - use HOST 0.0.0.0 to bind to all interfaces
CMD ["sh", "-c", "npm start -- -H 0.0.0.0"]

# Add health check
HEALTHCHECK --interval=30s --timeout=30s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:3000 || exit 1
